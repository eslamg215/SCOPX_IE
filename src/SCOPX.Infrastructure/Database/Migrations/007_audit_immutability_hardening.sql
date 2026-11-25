-- SCOPX.I.E migration 007: audit immutability hardening (DRAFT)
-- Apply only in a disposable environment after review. Application writes are append-only.
BEGIN;

CREATE OR REPLACE FUNCTION public.scopx_reject_audit_mutation()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
  RAISE EXCEPTION 'audit_events is append-only; % is prohibited', TG_OP
    USING ERRCODE = '55000';
END;
$$;

DROP TRIGGER IF EXISTS audit_events_reject_update_delete ON public.audit_events;
CREATE TRIGGER audit_events_reject_update_delete
BEFORE UPDATE OR DELETE ON public.audit_events
FOR EACH ROW EXECUTE FUNCTION public.scopx_reject_audit_mutation();

DROP TRIGGER IF EXISTS audit_events_reject_truncate ON public.audit_events;
CREATE TRIGGER audit_events_reject_truncate
BEFORE TRUNCATE ON public.audit_events
FOR EACH STATEMENT EXECUTE FUNCTION public.scopx_reject_audit_mutation();

-- Bound the details payload to reduce accidental oversized audit records.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'audit_events_details_size_check'
          AND conrelid = 'public.audit_events'::regclass
    ) THEN
        ALTER TABLE public.audit_events
          ADD CONSTRAINT audit_events_details_size_check
          CHECK (pg_column_size(details) <= 8192);
    END IF;
END $$;

COMMIT;
