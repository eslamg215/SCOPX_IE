-- Disposable-database integration smoke test for SCOPX tenant RLS and hardening.
-- Run only after migrations 001-013 and Grant-RuntimePrivileges.sql.
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

INSERT INTO public.media_retention_policies (hospital_id, enabled, retention_days, policy_reference)
VALUES
 ('00000000-0000-0000-0000-0000000000b1', true, 1, 'CI synthetic policy A'),
 ('00000000-0000-0000-0000-0000000000b2', false, NULL, NULL);

INSERT INTO public.media_assets (id, hospital_id, procedure_id, media_type, storage_key, file_name, content_type, file_size_bytes, sha256_hex, created_at, retention_until)
VALUES
 ('00000000-0000-0000-0000-0000000000f1', '00000000-0000-0000-0000-0000000000b1', '00000000-0000-0000-0000-0000000000e1', 'image', 'ci/a.jpg', 'a.jpg', 'image/jpeg', 1, repeat('a',64), now() - interval '2 days', now() - interval '100 years'),
 ('00000000-0000-0000-0000-0000000000f2', '00000000-0000-0000-0000-0000000000b2', '00000000-0000-0000-0000-0000000000e2', 'image', 'ci/b.jpg', 'b.jpg', 'image/jpeg', 1, repeat('b',64), now(), now() - interval '100 years');

-- A missing policy must also stamp NULL, even if a caller supplies a deadline.
DELETE FROM public.media_retention_policies
 WHERE hospital_id = '00000000-0000-0000-0000-0000000000b2';
INSERT INTO public.media_assets (id, hospital_id, procedure_id, media_type, storage_key, file_name, content_type, file_size_bytes, sha256_hex, retention_until)
VALUES
 ('00000000-0000-0000-0000-0000000000f3', '00000000-0000-0000-0000-0000000000b2', '00000000-0000-0000-0000-0000000000e2', 'image', 'ci/no-policy.jpg', 'no-policy.jpg', 'image/jpeg', 1, repeat('c',64), now() - interval '100 years');
INSERT INTO public.media_retention_policies (hospital_id, enabled, retention_days, policy_reference)
VALUES ('00000000-0000-0000-0000-0000000000b2', false, NULL, NULL);

DO $scopx_test$
DECLARE deadline timestamptz;
BEGIN
    SELECT retention_until INTO deadline FROM public.media_assets
     WHERE id = '00000000-0000-0000-0000-0000000000f1';
    IF deadline IS DISTINCT FROM (SELECT created_at + interval '1 day' FROM public.media_assets WHERE id = '00000000-0000-0000-0000-0000000000f1')
       OR (SELECT created_at < transaction_timestamp() FROM public.media_assets WHERE id = '00000000-0000-0000-0000-0000000000f1') THEN
        RAISE EXCEPTION 'enabled policy did not stamp from a server-controlled creation timestamp';
    END IF;
    IF (SELECT retention_until FROM public.media_assets WHERE id = '00000000-0000-0000-0000-0000000000f2') IS NOT NULL THEN
        RAISE EXCEPTION 'disabled policy accepted or stamped a caller-supplied retention deadline';
    END IF;
    IF (SELECT retention_until FROM public.media_assets WHERE id = '00000000-0000-0000-0000-0000000000f3') IS NOT NULL THEN
        RAISE EXCEPTION 'missing policy accepted or stamped a caller-supplied retention deadline';
    END IF;

    -- Policy changes do not rewrite deadlines already stamped on existing media.
    UPDATE public.media_retention_policies SET retention_days = 30
     WHERE hospital_id = '00000000-0000-0000-0000-0000000000b1';
    IF (SELECT retention_until FROM public.media_assets WHERE id = '00000000-0000-0000-0000-0000000000f1') IS DISTINCT FROM deadline THEN
        RAISE EXCEPTION 'policy change recalculated an existing media retention deadline';
    END IF;

    BEGIN
        UPDATE public.media_assets SET retention_until = now() - interval '1 day'
         WHERE id = '00000000-0000-0000-0000-0000000000f2';
        RAISE EXCEPTION 'runtime media retention stamp was mutable';
    EXCEPTION WHEN SQLSTATE '55000' THEN NULL;
    END;
    BEGIN
        UPDATE public.media_assets SET created_at = created_at - interval '100 years'
         WHERE id = '00000000-0000-0000-0000-0000000000f2';
        RAISE EXCEPTION 'runtime media creation timestamp was mutable';
    EXCEPTION WHEN SQLSTATE '55000' THEN NULL;
    END;
END $scopx_test$;
INSERT INTO public.media_legal_holds
    (id, hospital_id, media_id, hold_reason, placed_by_user_id)
VALUES
 ('20000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-0000000000b1',
  '00000000-0000-0000-0000-0000000000f1', 'CI synthetic legal hold',
  '00000000-0000-0000-0000-0000000000a1');

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

-- Exercise retention stamping through the actual least-privilege runtime role.
-- The hospital-A policy is enabled for thirty days after the preceding policy-change test; a hostile caller-supplied
-- deadline must be ignored by the database trigger.
INSERT INTO public.media_assets
    (id, hospital_id, procedure_id, media_type, storage_key, file_name,
     content_type, file_size_bytes, sha256_hex, retention_until)
VALUES
    ('00000000-0000-0000-0000-0000000000f4',
     '00000000-0000-0000-0000-0000000000b1',
     '00000000-0000-0000-0000-0000000000e1',
     'image', 'ci/runtime-stamped.jpg', 'runtime-stamped.jpg',
     'image/jpeg', 1, repeat('d',64), now() - interval '100 years');

DO $scopx_runtime_test$
DECLARE stamped_created_at timestamptz; stamped_deadline timestamptz;
BEGIN
    SELECT created_at, retention_until
      INTO stamped_created_at, stamped_deadline
      FROM public.media_assets
     WHERE id = '00000000-0000-0000-0000-0000000000f4';
    IF stamped_created_at IS DISTINCT FROM transaction_timestamp()
       OR stamped_deadline IS DISTINCT FROM stamped_created_at + interval '30 days' THEN
        RAISE EXCEPTION 'runtime role insert did not apply the enabled tenant retention policy';
    END IF;

    BEGIN
        UPDATE public.media_assets
           SET retention_until = now() - interval '1 day'
         WHERE id = '00000000-0000-0000-0000-0000000000f4';
        RAISE EXCEPTION 'runtime role changed an immutable retention deadline';
    EXCEPTION WHEN SQLSTATE '55000' THEN NULL;
    END;
END $scopx_runtime_test$;

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
    -- Tenant A has its original fixture plus the runtime-role retention-stamping fixture.
    IF n <> 2 THEN RAISE EXCEPTION 'media RLS failed for tenant A: %', n; END IF;
    SELECT count(*) INTO n FROM public.procedure_reports;
    IF n <> 1 THEN RAISE EXCEPTION 'report RLS failed for tenant A: %', n; END IF;
    SELECT count(*) INTO n FROM public.procedure_report_versions;
    IF n <> 1 THEN RAISE EXCEPTION 'report-version RLS failed for tenant A: %', n; END IF;
    SELECT count(*) INTO n FROM public.procedure_report_signatures;
    IF n <> 0 THEN RAISE EXCEPTION 'report-signature RLS failed for tenant A: %', n; END IF;
    SELECT count(*) INTO n FROM public.media_retention_policies;
    IF n <> 1 THEN RAISE EXCEPTION 'media retention policy RLS failed for tenant A: %', n; END IF;
    SELECT count(*) INTO n FROM public.media_legal_holds;
    IF n <> 1 THEN RAISE EXCEPTION 'media legal-hold RLS failed for tenant A: %', n; END IF;
    SELECT count(*) INTO n FROM public.media_deletion_queue;
    IF n <> 0 THEN RAISE EXCEPTION 'media deletion queue RLS failed for tenant A: %', n; END IF;
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
    IF n <> 2 THEN RAISE EXCEPTION 'media RLS failed for tenant B: %', n; END IF;
    SELECT count(*) INTO n FROM public.procedure_reports;
    IF n <> 1 THEN RAISE EXCEPTION 'report RLS failed for tenant B: %', n; END IF;
    SELECT count(*) INTO n FROM public.procedure_report_versions;
    IF n <> 1 THEN RAISE EXCEPTION 'report-version RLS failed for tenant B: %', n; END IF;
    SELECT count(*) INTO n FROM public.procedure_report_signatures;
    IF n <> 0 THEN RAISE EXCEPTION 'report-signature RLS failed for tenant B: %', n; END IF;
    SELECT count(*) INTO n FROM public.media_retention_policies;
    IF n <> 1 THEN RAISE EXCEPTION 'media retention policy RLS failed for tenant B: %', n; END IF;
    SELECT count(*) INTO n FROM public.media_legal_holds;
    IF n <> 0 THEN RAISE EXCEPTION 'media legal-hold tenant isolation failed for tenant B: %', n; END IF;
    SELECT count(*) INTO n FROM public.media_deletion_queue;
    IF n <> 0 THEN RAISE EXCEPTION 'media deletion queue RLS failed for tenant B: %', n; END IF;
    SELECT count(*) INTO n FROM public.audit_events;
    IF n <> 1 THEN RAISE EXCEPTION 'audit RLS failed for tenant B: %', n; END IF;
END $$;

RESET ROLE;

-- Exercise the media deletion lifecycle gates as the schema owner in tenant A.
SELECT set_config('scopx.hospital_id', '00000000-0000-0000-0000-0000000000b1', true);
DO $scopx_test$
BEGIN
    BEGIN
        INSERT INTO public.media_deletion_queue
            (hospital_id, media_id, reason, requested_by_user_id, not_before)
        VALUES
            ('00000000-0000-0000-0000-0000000000b1',
             '00000000-0000-0000-0000-0000000000f1', 'CI hold-block test',
             '00000000-0000-0000-0000-0000000000a1', now());
        RAISE EXCEPTION 'media deletion queue accepted an asset under legal hold';
    EXCEPTION WHEN SQLSTATE '55000' THEN NULL;
    END;
END $scopx_test$;

UPDATE public.media_legal_holds
   SET released_by_user_id = '00000000-0000-0000-0000-0000000000a1',
       released_at = now(), release_reason = 'CI synthetic hold release'
 WHERE id = '20000000-0000-0000-0000-000000000001';

DO $scopx_test$
BEGIN
    BEGIN
        UPDATE public.media_legal_holds
           SET release_reason = 'tampered release reason'
         WHERE id = '20000000-0000-0000-0000-000000000001';
        RAISE EXCEPTION 'released legal hold was mutable';
    EXCEPTION WHEN SQLSTATE '55000' THEN NULL;
    END;
    BEGIN
        DELETE FROM public.media_legal_holds
         WHERE id = '20000000-0000-0000-0000-000000000001';
        RAISE EXCEPTION 'legal hold record deletion was allowed';
    EXCEPTION WHEN SQLSTATE '55000' THEN NULL;
    END;
END $scopx_test$;

-- Model a pre-existing, expired legacy asset for queue-guard coverage. This
-- privileged CI fixture bypass is unavailable to the application runtime role.
SET session_replication_role = replica;
UPDATE public.media_assets
   SET created_at = now() - interval '2 days',
       retention_until = now() - interval '1 day'
 WHERE id = '00000000-0000-0000-0000-0000000000f1';
SET session_replication_role = origin;

INSERT INTO public.media_deletion_queue
    (hospital_id, media_id, reason, requested_by_user_id, not_before)
VALUES
 ('00000000-0000-0000-0000-0000000000b1',
  '00000000-0000-0000-0000-0000000000f1', 'CI eligible synthetic deletion',
  '00000000-0000-0000-0000-0000000000a1', now() - interval '1 day');

DO $scopx_test$
BEGIN
    BEGIN
        INSERT INTO public.media_deletion_queue
            (hospital_id, media_id, reason, requested_by_user_id, not_before)
        VALUES
            ('00000000-0000-0000-0000-0000000000b1',
             '00000000-0000-0000-0000-0000000000f1', 'CI duplicate active request',
             '00000000-0000-0000-0000-0000000000a1', now() - interval '1 day');
        RAISE EXCEPTION 'duplicate active deletion request was accepted';
    EXCEPTION WHEN unique_violation THEN NULL;
    END;
END $scopx_test$;

INSERT INTO public.media_deletion_events
    (hospital_id, media_id, queue_id, event_type, actor_user_id, details)
SELECT hospital_id, media_id, id, 'requested',
       '00000000-0000-0000-0000-0000000000a1', '{"synthetic":true}'::jsonb
  FROM public.media_deletion_queue
 WHERE media_id = '00000000-0000-0000-0000-0000000000f1';

DO $scopx_test$
BEGIN
    BEGIN
        UPDATE public.media_deletion_events SET event_type = 'object_deleted';
        RAISE EXCEPTION 'media deletion event UPDATE was not rejected';
    EXCEPTION WHEN SQLSTATE '55000' THEN NULL;
    END;
    BEGIN
        DELETE FROM public.media_deletion_events;
        RAISE EXCEPTION 'media deletion event DELETE was not rejected';
    EXCEPTION WHEN SQLSTATE '55000' THEN NULL;
    END;
END $scopx_test$;

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

DO $$
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
END $$;


ROLLBACK;
