-- SCOPX.I.E migration 011: medical-media retention and deletion lifecycle (DRAFT).
-- Schema foundation only. No worker, scheduler, object deletion, or API route is enabled.
-- Retention periods are intentionally NOT defaulted: each hospital must configure a
-- reviewed policy appropriate to applicable law and institutional policy.
BEGIN;

ALTER TABLE public.media_assets
    ADD COLUMN IF NOT EXISTS retention_until timestamptz,
    ADD COLUMN IF NOT EXISTS deletion_state varchar(20) NOT NULL DEFAULT 'active'
        CHECK (deletion_state IN ('active','queued','deleting','deleted','deletion_failed')),
    ADD COLUMN IF NOT EXISTS deletion_requested_at timestamptz,
    ADD COLUMN IF NOT EXISTS deleted_at timestamptz,
    ADD COLUMN IF NOT EXISTS deletion_reason varchar(1000);

ALTER TABLE public.media_assets
    ADD CONSTRAINT media_assets_deletion_timestamps_check
    CHECK (
      (deletion_state = 'active' AND deleted_at IS NULL)
      OR (deletion_state = 'queued' AND deleted_at IS NULL AND deletion_requested_at IS NOT NULL)
      OR (deletion_state = 'deleting' AND deleted_at IS NULL AND deletion_requested_at IS NOT NULL)
      OR (deletion_state = 'deleted' AND deleted_at IS NOT NULL AND deletion_requested_at IS NOT NULL)
      OR (deletion_state = 'deletion_failed' AND deleted_at IS NULL AND deletion_requested_at IS NOT NULL)
    ) NOT VALID;
ALTER TABLE public.media_assets
    VALIDATE CONSTRAINT media_assets_deletion_timestamps_check;

CREATE TABLE public.media_retention_policies (
    hospital_id uuid PRIMARY KEY REFERENCES public.hospitals(id) ON DELETE RESTRICT,
    enabled boolean NOT NULL DEFAULT false,
    retention_days integer,
    policy_reference varchar(200),
    updated_by_user_id uuid REFERENCES public.users(id) ON DELETE RESTRICT,
    updated_at timestamptz NOT NULL DEFAULT now(),
    CHECK (
      (enabled = false AND retention_days IS NULL)
      OR (enabled = true AND retention_days BETWEEN 1 AND 36500
          AND policy_reference IS NOT NULL AND length(trim(policy_reference)) BETWEEN 1 AND 200)
    )
);

CREATE TABLE public.media_legal_holds (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    hospital_id uuid NOT NULL,
    media_id uuid NOT NULL,
    hold_reason varchar(1000) NOT NULL CHECK (length(trim(hold_reason)) BETWEEN 1 AND 1000),
    placed_by_user_id uuid NOT NULL REFERENCES public.users(id) ON DELETE RESTRICT,
    placed_at timestamptz NOT NULL DEFAULT now(),
    released_by_user_id uuid REFERENCES public.users(id) ON DELETE RESTRICT,
    released_at timestamptz,
    release_reason varchar(1000),
    FOREIGN KEY (hospital_id, media_id)
      REFERENCES public.media_assets(hospital_id, id) ON DELETE RESTRICT,
    UNIQUE (hospital_id, id),
    CHECK (
      (released_at IS NULL AND released_by_user_id IS NULL AND release_reason IS NULL)
      OR (released_at IS NOT NULL AND released_by_user_id IS NOT NULL
          AND release_reason IS NOT NULL AND length(trim(release_reason)) BETWEEN 1 AND 1000)
    )
);
CREATE INDEX media_legal_holds_active_idx
    ON public.media_legal_holds(hospital_id, media_id) WHERE released_at IS NULL;

CREATE TABLE public.media_deletion_queue (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    hospital_id uuid NOT NULL,
    media_id uuid NOT NULL,
    status varchar(20) NOT NULL DEFAULT 'pending'
      CHECK (status IN ('pending','processing','failed','completed','cancelled')),
    reason varchar(1000) NOT NULL CHECK (length(trim(reason)) BETWEEN 1 AND 1000),
    requested_by_user_id uuid NOT NULL REFERENCES public.users(id) ON DELETE RESTRICT,
    requested_at timestamptz NOT NULL DEFAULT now(),
    not_before timestamptz NOT NULL,
    attempt_count integer NOT NULL DEFAULT 0 CHECK (attempt_count >= 0),
    last_error_code varchar(100),
    completed_at timestamptz,
    FOREIGN KEY (hospital_id, media_id)
      REFERENCES public.media_assets(hospital_id, id) ON DELETE RESTRICT,
    UNIQUE (hospital_id, id),
    CHECK (
      (status = 'completed' AND completed_at IS NOT NULL)
      OR (status <> 'completed' AND completed_at IS NULL)
    )
);
CREATE INDEX media_deletion_queue_due_idx
    ON public.media_deletion_queue(not_before, requested_at)
    WHERE status IN ('pending','failed');

CREATE TABLE public.media_deletion_events (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    hospital_id uuid NOT NULL REFERENCES public.hospitals(id) ON DELETE RESTRICT,
    media_id uuid NOT NULL,
    queue_id uuid,
    event_type varchar(40) NOT NULL
      CHECK (event_type IN ('requested','blocked_by_hold','started','failed','object_deleted','metadata_tombstoned','cancelled')),
    actor_user_id uuid REFERENCES public.users(id) ON DELETE RESTRICT,
    occurred_at timestamptz NOT NULL DEFAULT now(),
    details jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (pg_column_size(details) <= 8192),
    FOREIGN KEY (hospital_id, media_id)
      REFERENCES public.media_assets(hospital_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (hospital_id, queue_id)
      REFERENCES public.media_deletion_queue(hospital_id, id) ON DELETE RESTRICT
);
CREATE INDEX media_deletion_events_media_idx
    ON public.media_deletion_events(hospital_id, media_id, occurred_at DESC);

-- A queue item cannot be created or moved into processing while a legal hold is active
-- or before an explicit retention deadline. This is a database guard, not a worker.
CREATE OR REPLACE FUNCTION public.scopx_guard_media_deletion_queue()
RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE asset_retention_until timestamptz;
DECLARE asset_state text;
DECLARE active_hold boolean;
BEGIN
    SELECT retention_until, deletion_state
      INTO asset_retention_until, asset_state
      FROM public.media_assets
     WHERE hospital_id = NEW.hospital_id AND id = NEW.media_id
     FOR UPDATE;

    IF NOT FOUND THEN
      RAISE EXCEPTION 'media asset not found in tenant' USING ERRCODE = '23503';
    END IF;

    SELECT EXISTS (
      SELECT 1 FROM public.media_legal_holds
       WHERE hospital_id = NEW.hospital_id AND media_id = NEW.media_id
         AND released_at IS NULL
    ) INTO active_hold;

    IF NEW.status IN ('pending','processing') THEN
      IF active_hold THEN
        RAISE EXCEPTION 'media deletion blocked by active legal hold' USING ERRCODE = '55000';
      END IF;
      IF asset_retention_until IS NULL OR asset_retention_until > now()
         OR NEW.not_before < asset_retention_until THEN
        RAISE EXCEPTION 'media retention period has not elapsed' USING ERRCODE = '55000';
      END IF;
      IF asset_state IN ('deleted','deleting') THEN
        RAISE EXCEPTION 'media asset is already deleted or being deleted' USING ERRCODE = '55000';
      END IF;
    END IF;
    RETURN NEW;
END $$;

CREATE TRIGGER media_deletion_queue_guard
BEFORE INSERT OR UPDATE OF status, not_before, media_id, hospital_id
ON public.media_deletion_queue
FOR EACH ROW EXECUTE FUNCTION public.scopx_guard_media_deletion_queue();

-- Preserve the deletion event trail; metadata is tombstoned, never hard-deleted here.
CREATE OR REPLACE FUNCTION public.scopx_reject_media_deletion_event_mutation()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'media deletion events are append-only' USING ERRCODE = '55000';
END $$;
CREATE TRIGGER media_deletion_events_immutable
BEFORE UPDATE OR DELETE ON public.media_deletion_events
FOR EACH ROW EXECUTE FUNCTION public.scopx_reject_media_deletion_event_mutation();
CREATE TRIGGER media_deletion_events_no_truncate
BEFORE TRUNCATE ON public.media_deletion_events
FOR EACH STATEMENT EXECUTE FUNCTION public.scopx_reject_media_deletion_event_mutation();

-- Tenant isolation for all new lifecycle tables.
DO $$
DECLARE tbl text;
BEGIN
  FOREACH tbl IN ARRAY ARRAY[
    'media_retention_policies','media_legal_holds',
    'media_deletion_queue','media_deletion_events'
  ]
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
