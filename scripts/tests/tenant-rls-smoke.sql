-- Manual smoke test scaffold. Run only in a disposable DB after applying migrations.
-- Execute as the restricted runtime role (NOT a table owner and NOT BYPASSRLS).
-- These are assertions/checks, not a complete tenant isolation certification.

DO $$
BEGIN
  IF to_regclass('public.patients') IS NULL THEN
    RAISE EXCEPTION 'patients table missing; migrations not applied';
  END IF;
  IF NOT (SELECT relrowsecurity FROM pg_class WHERE oid = 'public.patients'::regclass) THEN
    RAISE EXCEPTION 'patients RLS is not enabled';
  END IF;
  IF NOT (SELECT relforcerowsecurity FROM pg_class WHERE oid = 'public.patients'::regclass) THEN
    RAISE EXCEPTION 'patients FORCE RLS is not enabled';
  END IF;
  IF NOT (SELECT relrowsecurity FROM pg_class WHERE oid = 'public.procedure_reports'::regclass) THEN
    RAISE EXCEPTION 'procedure_reports RLS is not enabled';
  END IF;
END $$;

-- Set context only after API membership verification; example values are fake.
-- SET LOCAL scopx.user_id = '00000000-0000-0000-0000-000000000001';
-- SET LOCAL scopx.hospital_id = '00000000-0000-0000-0000-000000000002';
-- SELECT count(*) FROM patients; -- verify only this tenant, and zero rows with no context
-- Attempt read/write against another tenant in controlled integration tests.
