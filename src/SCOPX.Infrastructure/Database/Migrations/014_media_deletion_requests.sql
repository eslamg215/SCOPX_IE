-- SCOPX.I.E migration 014: user-facing media deletion requests (DRAFT).
-- Requests are deliberately separate from the guarded execution queue.
-- No approval-to-queue action, worker, scheduler, or object deletion is enabled.
BEGIN;

CREATE TABLE public.media_deletion_requests (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    hospital_id uuid NOT NULL,
    media_id uuid NOT NULL,
    status varchar(20) NOT NULL DEFAULT 'submitted'
      CHECK (status IN ('submitted','under_review','approved','rejected','cancelled')),
    reason varchar(1000) NOT NULL
      CHECK (length(trim(reason)) BETWEEN 1 AND 1000),
    requested_by_user_id uuid NOT NULL REFERENCES public.users(id) ON DELETE RESTRICT,
    requested_at timestamptz NOT NULL DEFAULT transaction_timestamp(),
    reviewer_user_id uuid REFERENCES public.users(id) ON DELETE RESTRICT,
    decision_reason varchar(1000),
    decided_at timestamptz,
    FOREIGN KEY (hospital_id, media_id)
      REFERENCES public.media_assets(hospital_id, id) ON DELETE RESTRICT,
    UNIQUE (hospital_id, id),
    CHECK (
      (status IN ('submitted','under_review') AND decided_at IS NULL)
      OR (status IN ('approved','rejected','cancelled') AND decided_at IS NOT NULL)
    ),
    CHECK (
      (status = 'submitted' AND reviewer_user_id IS NULL AND decision_reason IS NULL AND decided_at IS NULL)
      OR (status = 'under_review' AND reviewer_user_id IS NOT NULL AND decision_reason IS NULL AND decided_at IS NULL)
      OR (status IN ('approved','rejected','cancelled')
          AND reviewer_user_id IS NOT NULL
          AND decision_reason IS NOT NULL
          AND length(trim(decision_reason)) BETWEEN 1 AND 1000
          AND decided_at IS NOT NULL)
    )
);

-- A request remains open through approval until a later, separately reviewed
-- enqueue workflow is implemented. This prevents duplicate approvals meanwhile.
CREATE UNIQUE INDEX media_deletion_requests_one_open_per_asset_idx
    ON public.media_deletion_requests(hospital_id, media_id)
    WHERE status IN ('submitted','under_review','approved');

CREATE INDEX media_deletion_requests_inbox_idx
    ON public.media_deletion_requests(hospital_id, status, requested_at DESC);

CREATE TABLE public.media_deletion_request_events (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    hospital_id uuid NOT NULL,
    request_id uuid NOT NULL,
    media_id uuid NOT NULL,
    from_status varchar(20),
    to_status varchar(20) NOT NULL
      CHECK (to_status IN ('submitted','under_review','approved','rejected','cancelled')),
    actor_user_id uuid NOT NULL REFERENCES public.users(id) ON DELETE RESTRICT,
    occurred_at timestamptz NOT NULL DEFAULT transaction_timestamp(),
    reason varchar(1000) NOT NULL
      CHECK (length(trim(reason)) BETWEEN 1 AND 1000),
    FOREIGN KEY (hospital_id, request_id)
      REFERENCES public.media_deletion_requests(hospital_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (hospital_id, media_id)
      REFERENCES public.media_assets(hospital_id, id) ON DELETE RESTRICT
);
CREATE INDEX media_deletion_request_events_request_idx
    ON public.media_deletion_request_events(hospital_id, request_id, occurred_at, id);

-- Enforce requester identity, active tenant membership, immutable request facts,
-- four-eyes approval, required decision reasons, and legal state transitions.
CREATE OR REPLACE FUNCTION public.scopx_guard_media_deletion_request()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, public
AS $$
DECLARE actor_id uuid;
DECLARE actor_is_member boolean;
BEGIN
    BEGIN
      actor_id := nullif(current_setting('scopx.user_id', true), '')::uuid;
    EXCEPTION WHEN invalid_text_representation THEN
      actor_id := NULL;
    END;
    IF actor_id IS NULL THEN
      RAISE EXCEPTION 'media deletion request requires an authenticated tenant actor'
        USING ERRCODE = '42501';
    END IF;

    SELECT EXISTS (
      SELECT 1 FROM public.hospital_memberships
       WHERE hospital_id = NEW.hospital_id
         AND user_id = actor_id
         AND status = 'active'
    ) INTO actor_is_member;
    IF NOT actor_is_member THEN
      RAISE EXCEPTION 'actor has no active membership in request tenant'
        USING ERRCODE = '42501';
    END IF;

    IF TG_OP = 'INSERT' THEN
      IF NEW.status <> 'submitted'
         OR NEW.requested_by_user_id <> actor_id
         OR NEW.reviewer_user_id IS NOT NULL
         OR NEW.decision_reason IS NOT NULL
         OR NEW.decided_at IS NOT NULL THEN
        RAISE EXCEPTION 'new deletion requests must be submitted by their requester'
          USING ERRCODE = '23514';
      END IF;
      NEW.requested_at := transaction_timestamp();
      RETURN NEW;
    END IF;

    IF NEW.id IS DISTINCT FROM OLD.id
       OR NEW.hospital_id IS DISTINCT FROM OLD.hospital_id
       OR NEW.media_id IS DISTINCT FROM OLD.media_id
       OR NEW.reason IS DISTINCT FROM OLD.reason
       OR NEW.requested_by_user_id IS DISTINCT FROM OLD.requested_by_user_id
       OR NEW.requested_at IS DISTINCT FROM OLD.requested_at THEN
      RAISE EXCEPTION 'media deletion request facts are immutable'
        USING ERRCODE = '55000';
    END IF;

    IF NEW.status = OLD.status THEN
      RAISE EXCEPTION 'media deletion request updates must change state'
        USING ERRCODE = '23514';
    END IF;

    IF OLD.status = 'submitted' AND NEW.status = 'under_review' THEN
      IF actor_id = OLD.requested_by_user_id THEN
        RAISE EXCEPTION 'requester cannot review their own request'
          USING ERRCODE = '42501';
      END IF;
      NEW.reviewer_user_id := actor_id;
      NEW.decision_reason := NULL;
      NEW.decided_at := NULL;
    ELSIF OLD.status = 'under_review' AND NEW.status IN ('approved','rejected') THEN
      IF actor_id = OLD.requested_by_user_id THEN
        RAISE EXCEPTION 'requester cannot approve or reject their own request'
          USING ERRCODE = '42501';
      END IF;
      IF OLD.reviewer_user_id IS DISTINCT FROM actor_id THEN
        RAISE EXCEPTION 'only the assigned reviewer may decide this request'
          USING ERRCODE = '42501';
      END IF;
      IF NEW.decision_reason IS NULL OR length(trim(NEW.decision_reason)) NOT BETWEEN 1 AND 1000 THEN
        RAISE EXCEPTION 'approval or rejection requires a decision reason'
          USING ERRCODE = '23514';
      END IF;
      NEW.reviewer_user_id := actor_id;
      NEW.decided_at := transaction_timestamp();
    ELSIF OLD.status IN ('submitted','under_review') AND NEW.status = 'cancelled' THEN
      IF actor_id <> OLD.requested_by_user_id THEN
        RAISE EXCEPTION 'only the requester may cancel this draft workflow request'
          USING ERRCODE = '42501';
      END IF;
      NEW.reviewer_user_id := actor_id;
      IF NEW.decision_reason IS NULL OR length(trim(NEW.decision_reason)) NOT BETWEEN 1 AND 1000 THEN
        RAISE EXCEPTION 'cancellation requires a reason'
          USING ERRCODE = '23514';
      END IF;
      NEW.decided_at := transaction_timestamp();
    ELSE
      RAISE EXCEPTION 'invalid media deletion request state transition: % -> %', OLD.status, NEW.status
        USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER media_deletion_requests_guard
BEFORE INSERT OR UPDATE ON public.media_deletion_requests
FOR EACH ROW EXECUTE FUNCTION public.scopx_guard_media_deletion_request();

-- Events are emitted by a SECURITY DEFINER trigger; the runtime role cannot
-- forge, update, delete, or truncate the history table directly.
CREATE OR REPLACE FUNCTION public.scopx_record_media_deletion_request_event()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, public
AS $$
DECLARE actor_id uuid;
DECLARE event_reason text;
BEGIN
    actor_id := nullif(current_setting('scopx.user_id', true), '')::uuid;
    IF TG_OP = 'INSERT' THEN
      event_reason := NEW.reason;
      INSERT INTO public.media_deletion_request_events
        (hospital_id, request_id, media_id, from_status, to_status, actor_user_id, reason)
      VALUES
        (NEW.hospital_id, NEW.id, NEW.media_id, NULL, NEW.status, actor_id, event_reason);
      RETURN NEW;
    END IF;

    event_reason := NEW.decision_reason;
    IF event_reason IS NULL THEN
      event_reason := 'Request moved to under_review';
    END IF;
    INSERT INTO public.media_deletion_request_events
      (hospital_id, request_id, media_id, from_status, to_status, actor_user_id, reason)
    VALUES
      (NEW.hospital_id, NEW.id, NEW.media_id, OLD.status, NEW.status, actor_id, event_reason);
    RETURN NEW;
END;
$$;

CREATE TRIGGER media_deletion_requests_event_insert
AFTER INSERT ON public.media_deletion_requests
FOR EACH ROW EXECUTE FUNCTION public.scopx_record_media_deletion_request_event();
CREATE TRIGGER media_deletion_requests_event_update
AFTER UPDATE OF status ON public.media_deletion_requests
FOR EACH ROW EXECUTE FUNCTION public.scopx_record_media_deletion_request_event();

-- Trigger functions are callable only through their installed triggers.
-- Revoking PUBLIC execution prevents future accidental direct invocation of
-- SECURITY DEFINER entry points if their signatures are ever changed.
REVOKE ALL ON FUNCTION public.scopx_guard_media_deletion_request() FROM PUBLIC;
REVOKE ALL ON FUNCTION public.scopx_record_media_deletion_request_event() FROM PUBLIC;

CREATE OR REPLACE FUNCTION public.scopx_reject_media_deletion_request_event_mutation()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'media deletion request events are append-only' USING ERRCODE = '55000';
END;
$$;
REVOKE ALL ON FUNCTION public.scopx_reject_media_deletion_request_event_mutation() FROM PUBLIC;
CREATE TRIGGER media_deletion_request_events_immutable
BEFORE UPDATE OR DELETE ON public.media_deletion_request_events
FOR EACH ROW EXECUTE FUNCTION public.scopx_reject_media_deletion_request_event_mutation();
CREATE TRIGGER media_deletion_request_events_no_truncate
BEFORE TRUNCATE ON public.media_deletion_request_events
FOR EACH STATEMENT EXECUTE FUNCTION public.scopx_reject_media_deletion_request_event_mutation();

-- Tenant isolation. Event INSERT is intentionally not exposed to the runtime role.
DO $$
DECLARE tbl text;
BEGIN
  FOREACH tbl IN ARRAY ARRAY['media_deletion_requests','media_deletion_request_events']
  LOOP
    EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', tbl);
    EXECUTE format('ALTER TABLE public.%I FORCE ROW LEVEL SECURITY', tbl);
    EXECUTE format(
      'CREATE POLICY tenant_scope_select ON public.%I FOR SELECT USING (hospital_id = public.scopx_current_hospital_id())',
      tbl);
    EXECUTE format(
      'CREATE POLICY tenant_scope_insert ON public.%I FOR INSERT WITH CHECK (hospital_id = public.scopx_current_hospital_id())',
      tbl);
    EXECUTE format(
      'CREATE POLICY tenant_scope_update ON public.%I FOR UPDATE USING (hospital_id = public.scopx_current_hospital_id()) WITH CHECK (hospital_id = public.scopx_current_hospital_id())',
      tbl);
  END LOOP;
END $$;

COMMIT;
