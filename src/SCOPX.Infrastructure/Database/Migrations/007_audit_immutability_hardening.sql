-- SCOPX.I.E migration 007: audit immutability hardening (DRAFT)
-- Apply only in a disposable environment after review. Application writes are append-only.
BEGIN;

CREATE OR REPLACE FUNCTION scopx_reject_audit_mutation()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
  RAISE EXCEPTION 'audit_events is append-only; % is prohibited', TG_OP
    USING ERRCODE = '55000';
END;
$$;

DROP TRIGGER IF EXISTS audit_events_reject_update_delete ON audit_events;
CREATE TRIGGER audit_events_reject_update_delete
BEFORE UPDATE OR DELETE ON audit_events
FOR EACH ROW EXECUTE FUNCTION scopx_reject_audit_mutation();

-- Bound the details payload to reduce accidental oversized audit records.
ALTER TABLE audit_events
  ADD CONSTRAINT audit_events_details_size_check
  CHECK (pg_column_size(details) <= 8192);

COMMIT;
