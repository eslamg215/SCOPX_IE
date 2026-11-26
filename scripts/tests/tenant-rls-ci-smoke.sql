-- Disposable-database integration smoke test for SCOPX tenant RLS and hardening.
-- Run only after migrations 001-010 and Grant-RuntimePrivileges.sql.
-- The entire fixture set is rolled back. All records are synthetic.
BEGIN;

INSERT INTO public.users (id, email_normalized, display_name, status)
VALUES
 ('00000000-0000-0000-0000-0000000000a1', 'rls-a@example.invalid', 'RLS Test A', 'active'),
 ('00000000-0000-0000-0000-0000000000a2', 'rls-b@example.invalid', 'RLS Test B', 'active');

INSERT INTO public.hospitals (id, name, status)
VALUES
 ('00000000-0000-0000-0000-0000000000b1', 'RLS Test Hospital A', 'active'),
 ('00000000-0000-0000-0000-0000000000b2', 'RLS Test Hospital B', 'active');

INSERT INTO public.hospital_memberships (id, hospital_id, user_id, status)
VALUES
 ('00000000-0000-0000-0000-0000000000c1', '00000000-0000-0000-0000-0000000000b1', '00000000-0000-0000-0000-0000000000a1', 'active'),
 ('00000000-0000-0000-0000-0000000000c2', '00000000-0000-0000-0000-0000000000b2', '00000000-0000-0000-0000-0000000000a2', 'active');

INSERT INTO public.membership_roles (hospital_id, membership_id, role_id)
SELECT '00000000-0000-0000-0000-0000000000b1',
       '00000000-0000-0000-0000-0000000000c1', id
FROM public.roles WHERE code = 'doctor';

INSERT INTO public.membership_roles (hospital_id, membership_id, role_id)
SELECT '00000000-0000-0000-0000-0000000000b2',
       '00000000-0000-0000-0000-0000000000c2', id
FROM public.roles WHERE code = 'nurse';

INSERT INTO public.patients (id, hospital_id, patient_code, full_name)
VALUES
 ('00000000-0000-0000-0000-0000000000d1', '00000000-0000-0000-0000-0000000000b1', 'RLS-A', 'Synthetic Patient A'),
 ('00000000-0000-0000-0000-0000000000d2', '00000000-0000-0000-0000-0000000000b2', 'RLS-B', 'Synthetic Patient B');

INSERT INTO public.procedures (id, hospital_id, patient_id, procedure_type, performed_by_membership_id)
VALUES
 ('00000000-0000-0000-0000-0000000000e1', '00000000-0000-0000-0000-0000000000b1', '00000000-0000-0000-0000-0000000000d1', 'test', '00000000-0000-0000-0000-0000000000c1'),
 ('00000000-0000-0000-0000-0000000000e2', '00000000-0000-0000-0000-0000000000b2', '00000000-0000-0000-0000-0000000000d2', 'test', '00000000-0000-0000-0000-0000000000c2');

INSERT INTO public.media_assets (id, hospital_id, procedure_id, media_type, storage_key, file_name, content_type, file_size_bytes, sha256_hex)
VALUES
 ('00000000-0000-0000-0000-0000000000f1', '00000000-0000-0000-0000-0000000000b1', '00000000-0000-0000-0000-0000000000e1', 'image', 'ci/a.jpg', 'a.jpg', 'image/jpeg', 1, repeat('a',64)),
 ('00000000-0000-0000-0000-0000000000f2', '00000000-0000-0000-0000-0000000000b2', '00000000-0000-0000-0000-0000000000e2', 'image', 'ci/b.jpg', 'b.jpg', 'image/jpeg', 1, repeat('b',64));

INSERT INTO public.procedure_reports
    (id, hospital_id, procedure_id, created_by_membership_id, report_body, report_body_sha256)
VALUES
 ('10000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-0000000000b1', '00000000-0000-0000-0000-0000000000e1', '00000000-0000-0000-0000-0000000000c1', 'synthetic A', encode(digest(convert_to('synthetic A', 'UTF8'), 'sha256'), 'hex')),
 ('10000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-0000000000b2', '00000000-0000-0000-0000-0000000000e2', '00000000-0000-0000-0000-0000000000c2', 'synthetic B', encode(digest(convert_to('synthetic B', 'UTF8'), 'sha256'), 'hex'));

INSERT INTO public.audit_events (hospital_id, actor_user_id, event_type, target_type, target_id, details)
VALUES
 ('00000000-0000-0000-0000-0000000000b1', '00000000-0000-0000-0000-0000000000a1', 'ci.test', 'patient', 'RLS-A', '{"synthetic":true}'::jsonb),
 ('00000000-0000-0000-0000-0000000000b2', '00000000-0000-0000-0000-0000000000a2', 'ci.test', 'patient', 'RLS-B', '{"synthetic":true}'::jsonb);

-- Prove that a non-owner runtime role sees only the selected tenant.
SET LOCAL ROLE scopx_runtime;
SELECT set_config('scopx.user_id', '00000000-0000-0000-0000-0000000000a1', true);
SELECT set_config('scopx.hospital_id', '00000000-0000-0000-0000-0000000000b1', true);

DO $$
DECLARE n bigint;
BEGIN
    SELECT count(*) INTO n FROM public.hospitals;
    IF n <> 1 THEN RAISE EXCEPTION 'hospital RLS failed for tenant A: %', n; END IF;
    SELECT count(*) INTO n FROM public.hospital_memberships;
    IF n <> 1 THEN RAISE EXCEPTION 'membership RLS failed for user A: %', n; END IF;
    SELECT count(*) INTO n FROM public.membership_roles;
    IF n <> 1 THEN RAISE EXCEPTION 'membership role RLS failed for tenant A: %', n; END IF;
    SELECT count(*) INTO n FROM public.patients;
    IF n <> 1 THEN RAISE EXCEPTION 'patient RLS failed for tenant A: %', n; END IF;
    SELECT count(*) INTO n FROM public.procedures;
    IF n <> 1 THEN RAISE EXCEPTION 'procedure RLS failed for tenant A: %', n; END IF;
    SELECT count(*) INTO n FROM public.media_assets;
    IF n <> 1 THEN RAISE EXCEPTION 'media RLS failed for tenant A: %', n; END IF;
    SELECT count(*) INTO n FROM public.procedure_reports;
    IF n <> 1 THEN RAISE EXCEPTION 'report RLS failed for tenant A: %', n; END IF;
    SELECT count(*) INTO n FROM public.procedure_report_versions;
    IF n <> 1 THEN RAISE EXCEPTION 'report-version RLS failed for tenant A: %', n; END IF;
    SELECT count(*) INTO n FROM public.procedure_report_signatures;
    IF n <> 0 THEN RAISE EXCEPTION 'report-signature RLS failed for tenant A: %', n; END IF;
    SELECT count(*) INTO n FROM public.audit_events;
    IF n <> 1 THEN RAISE EXCEPTION 'audit RLS failed for tenant A: %', n; END IF;

    BEGIN
        INSERT INTO public.patients (hospital_id, patient_code, full_name)
        VALUES ('00000000-0000-0000-0000-0000000000b2', 'CROSS-TENANT', 'Must be rejected');
        RAISE EXCEPTION 'RLS failed to reject cross-tenant patient insert';
    EXCEPTION WHEN insufficient_privilege THEN
        NULL;
    END;

    INSERT INTO public.audit_events (hospital_id, actor_user_id, event_type, target_type, target_id, details)
    VALUES ('00000000-0000-0000-0000-0000000000b1',
            '00000000-0000-0000-0000-0000000000a1',
            'ci.runtime_audit', 'test', 'runtime-write', '{"synthetic":true}'::jsonb);
END $$;

SELECT set_config('scopx.user_id', '00000000-0000-0000-0000-0000000000a2', true);
SELECT set_config('scopx.hospital_id', '00000000-0000-0000-0000-0000000000b2', true);

DO $$
DECLARE n bigint;
BEGIN
    SELECT count(*) INTO n FROM public.hospitals;
    IF n <> 1 THEN RAISE EXCEPTION 'hospital RLS failed for tenant B: %', n; END IF;
    SELECT count(*) INTO n FROM public.patients;
    IF n <> 1 THEN RAISE EXCEPTION 'patient RLS failed for tenant B: %', n; END IF;
    SELECT count(*) INTO n FROM public.procedures;
    IF n <> 1 THEN RAISE EXCEPTION 'procedure RLS failed for tenant B: %', n; END IF;
    SELECT count(*) INTO n FROM public.media_assets;
    IF n <> 1 THEN RAISE EXCEPTION 'media RLS failed for tenant B: %', n; END IF;
    SELECT count(*) INTO n FROM public.procedure_reports;
    IF n <> 1 THEN RAISE EXCEPTION 'report RLS failed for tenant B: %', n; END IF;
    SELECT count(*) INTO n FROM public.procedure_report_versions;
    IF n <> 1 THEN RAISE EXCEPTION 'report-version RLS failed for tenant B: %', n; END IF;
    SELECT count(*) INTO n FROM public.procedure_report_signatures;
    IF n <> 0 THEN RAISE EXCEPTION 'report-signature RLS failed for tenant B: %', n; END IF;
    SELECT count(*) INTO n FROM public.audit_events;
    IF n <> 1 THEN RAISE EXCEPTION 'audit RLS failed for tenant B: %', n; END IF;
END $$;

RESET ROLE;

-- Prove role-scope triggers reject cross-scope assignments.
DO $$
DECLARE hospital_role uuid; platform_role uuid;
BEGIN
    SELECT id INTO hospital_role FROM public.roles WHERE code = 'doctor';
    SELECT id INTO platform_role FROM public.roles WHERE code = 'platform_super_admin';

    BEGIN
        INSERT INTO public.membership_roles (hospital_id, membership_id, role_id)
        VALUES ('00000000-0000-0000-0000-0000000000b1', '00000000-0000-0000-0000-0000000000c1', platform_role);
        RAISE EXCEPTION 'role-scope trigger accepted platform role in hospital membership';
    EXCEPTION WHEN check_violation THEN NULL;
    END;

    BEGIN
        INSERT INTO public.platform_user_roles (user_id, role_id)
        VALUES ('00000000-0000-0000-0000-0000000000a1', hospital_role);
        RAISE EXCEPTION 'role-scope trigger accepted hospital role as platform role';
    EXCEPTION WHEN check_violation THEN NULL;
    END;

    BEGIN
        UPDATE public.roles SET scope = 'platform' WHERE id = hospital_role;
        RAISE EXCEPTION 'role-scope trigger allowed scope change for assigned role';
    EXCEPTION WHEN check_violation THEN NULL;
    END;
END $$;

-- Prove the audit table rejects row mutation and truncation.
DO $$
BEGIN
    BEGIN
        DELETE FROM public.audit_events WHERE target_id = 'RLS-A';
        RAISE EXCEPTION 'audit trigger allowed DELETE';
    EXCEPTION WHEN SQLSTATE '55000' THEN NULL;
    END;

    BEGIN
        EXECUTE 'TRUNCATE TABLE public.audit_events';
        RAISE EXCEPTION 'audit trigger allowed TRUNCATE';
    EXCEPTION WHEN SQLSTATE '55000' THEN NULL;
    END;
END $$;

-- A schema-level fixture verifies that signing requires a reviewed current version,
-- creates an immutable artifact, and freezes the signed report. The bytes below are
-- intentionally dummy test data; they are NOT a valid cryptographic signature.
UPDATE public.procedure_reports
SET status = 'review'
WHERE id = '10000000-0000-0000-0000-000000000001';

INSERT INTO public.procedure_report_signatures
    (hospital_id, report_id, report_version, document_sha256, signer_membership_id,
     signed_at, reauthenticated_at, authentication_method, canonicalization_version,
     signature_algorithm, signing_key_id, signature_value)
SELECT hospital_id, id, report_version, report_body_sha256,
       '00000000-0000-0000-0000-0000000000c1',
       now(), now() - interval '1 minute', 'password', 'SCOPX-REPORT-TEXT-V1',
       'RSA-PSS-SHA256', 'ci-test-placeholder-not-crypto', decode(repeat('a5', 256), 'hex')
FROM public.procedure_reports
WHERE id = '10000000-0000-0000-0000-000000000001';

DO $
DECLARE report_status text;
BEGIN
    SELECT status INTO report_status FROM public.procedure_reports
    WHERE id = '10000000-0000-0000-0000-000000000001';
    IF report_status <> 'signed' THEN
        RAISE EXCEPTION 'signature insert did not move the reviewed report to signed state';
    END IF;

    BEGIN
        UPDATE public.procedure_reports
        SET report_body = 'tampered after signature'
        WHERE id = '10000000-0000-0000-0000-000000000001';
        RAISE EXCEPTION 'signed report mutation was not rejected';
    EXCEPTION WHEN SQLSTATE '55000' THEN NULL;
    END;

    BEGIN
        UPDATE public.procedure_report_signatures
        SET signing_key_id = 'tampered'
        WHERE report_id = '10000000-0000-0000-0000-000000000001';
        RAISE EXCEPTION 'signature artifact UPDATE was not rejected';
    EXCEPTION WHEN SQLSTATE '55000' THEN NULL;
    END;

    BEGIN
        DELETE FROM public.procedure_report_versions
        WHERE report_id = '10000000-0000-0000-0000-000000000001';
        RAISE EXCEPTION 'report version DELETE was not rejected';
    EXCEPTION WHEN SQLSTATE '55000' THEN NULL;
    END;

    BEGIN
        EXECUTE 'TRUNCATE TABLE public.procedure_report_signatures';
        RAISE EXCEPTION 'signature artifact TRUNCATE was not rejected';
    EXCEPTION WHEN SQLSTATE '55000' THEN NULL;
    END;
END $;


ROLLBACK;
