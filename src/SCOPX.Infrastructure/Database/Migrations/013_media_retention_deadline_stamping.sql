-- SCOPX.I.E migration 013: stamp retention deadlines on new media only (DRAFT).
-- A deadline is calculated only from an explicitly enabled hospital policy.
-- Existing media rows are never backfilled or recalculated when a policy changes.
-- No deletion worker, scheduler, or object deletion is enabled.
BEGIN;

CREATE OR REPLACE FUNCTION public.scopx_stamp_media_retention_deadline()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE policy_enabled boolean;
DECLARE policy_days integer;
BEGIN
    IF TG_OP = 'UPDATE' THEN
        IF NEW.retention_until IS DISTINCT FROM OLD.retention_until
           OR NEW.created_at IS DISTINCT FROM OLD.created_at
           OR NEW.hospital_id IS DISTINCT FROM OLD.hospital_id THEN
            RAISE EXCEPTION 'media retention stamp and creation tenant are immutable'
                USING ERRCODE = '55000';
        END IF;
        RETURN NEW;
    END IF;

    SELECT enabled, retention_days
      INTO policy_enabled, policy_days
      FROM public.media_retention_policies
     WHERE hospital_id = NEW.hospital_id;

    -- Ignore any caller-supplied value. No policy or a disabled policy means
    -- NULL, which keeps the deletion guard fail-closed.
    IF NOT FOUND OR policy_enabled IS DISTINCT FROM true THEN
        NEW.retention_until := NULL;
    ELSE
        IF policy_days IS NULL OR policy_days NOT BETWEEN 1 AND 36500 THEN
            RAISE EXCEPTION 'enabled media retention policy is invalid'
                USING ERRCODE = '23514';
        END IF;
        NEW.retention_until := NEW.created_at + make_interval(days => policy_days);
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER media_assets_retention_stamp_insert
BEFORE INSERT ON public.media_assets
FOR EACH ROW EXECUTE FUNCTION public.scopx_stamp_media_retention_deadline();

CREATE TRIGGER media_assets_retention_stamp_immutable
BEFORE UPDATE OF retention_until, created_at, hospital_id ON public.media_assets
FOR EACH ROW EXECUTE FUNCTION public.scopx_stamp_media_retention_deadline();

COMMIT;
