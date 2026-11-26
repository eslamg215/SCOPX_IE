-- SCOPX.I.E migration 012: media retention lifecycle invariant hardening (DRAFT).
-- Adds one-open-request semantics and one-way legal-hold release controls.
BEGIN;

CREATE UNIQUE INDEX media_deletion_queue_one_open_per_asset_idx
    ON public.media_deletion_queue(hospital_id, media_id)
    WHERE status IN ('pending','processing','failed');

CREATE OR REPLACE FUNCTION public.scopx_guard_media_legal_hold_mutation()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'legal hold records cannot be deleted' USING ERRCODE = '55000';
    END IF;

    IF OLD.released_at IS NOT NULL THEN
        RAISE EXCEPTION 'released legal hold records are immutable' USING ERRCODE = '55000';
    END IF;

    IF NEW.id IS DISTINCT FROM OLD.id
       OR NEW.hospital_id IS DISTINCT FROM OLD.hospital_id
       OR NEW.media_id IS DISTINCT FROM OLD.media_id
       OR NEW.hold_reason IS DISTINCT FROM OLD.hold_reason
       OR NEW.placed_by_user_id IS DISTINCT FROM OLD.placed_by_user_id
       OR NEW.placed_at IS DISTINCT FROM OLD.placed_at THEN
        RAISE EXCEPTION 'legal hold placement facts are immutable' USING ERRCODE = '55000';
    END IF;

    IF NEW.released_at IS NULL OR NEW.released_by_user_id IS NULL
       OR NEW.release_reason IS NULL OR length(trim(NEW.release_reason)) = 0 THEN
        RAISE EXCEPTION 'legal hold release requires actor, timestamp, and reason' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END $$;

CREATE TRIGGER media_legal_holds_guard_mutation
BEFORE UPDATE OR DELETE ON public.media_legal_holds
FOR EACH ROW EXECUTE FUNCTION public.scopx_guard_media_legal_hold_mutation();

COMMIT;