-- Disposable-database integration smoke test for SCOPX tenant RLS and hardening.
-- Run only after migrations 001-009 and Grant-RuntimePrivileges.sql.
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

INSERT INTO public.procedures (id, hospital_id, patient_id, procedure_type)
VALUES
 ('00000000-0000-0000-0000-0000000000e1', '00000000-0000-0000-0000-0000000000b1', '00000000-0000-0000-0000-0000000000d1', 'test'),
 ('00000000-0000-0000-0000-0000000000e2', '00000000-0000-0000-0000-0000000000b2', '00000000-0000-0000-0000-0000000000d2', 'test');

INSERT INTO public.media_assets (id, hospital_id, procedure_id, media_type, storage_key, file_name, content_type, file_size_bytes, sha256_hex)
VALUES
 ('00000000-0000-0000-0000-0000000000f1', '00000000-0000-0000-0000-0000000000b1', '00000000-0000-0000-0000-0000000000e1', 'image', 'ci/a.jpg', 'a.jpg', 'image/jpeg', 1, repeat('a',64)),
 ('00000000-0000-0000-0000-0000000000f2', '00000000-0000-0000-0000-0000000000b2', '00000000-0000-0000-0000-0000000000e2', 'image', 'ci/b.jpg', 'b.jpg', 'image/jpeg', 1, repeat('b',64));

INSERT INTO public.procedure_reports (id, hospital_id, procedure_id, report_body)
VALUES
 ('10000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-0000000000b1', '00000000-0000-0000-0000-0000000000e1', 'synthetic A'),
 ('10000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-0000000000b2', '00000000-0000-0000-0000-0000000000e2', 'synthetic B');

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
END $;

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

ROLLBACK;
