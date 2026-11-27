-- SCOPX.I.E least-privilege runtime grants.
-- Apply only after reviewed migrations 001-009 (and optional 010/011 when installed) and the RLS validation suite have passed
-- in a disposable development database. Execute as the database/schema owner.
-- Create scopx_runtime separately with LOGIN, NOSUPERUSER, NOCREATEDB,
-- NOCREATEROLE, NOINHERIT. Never grant ownership, BYPASSRLS, or migration rights.
--
-- This is the shared API runtime role for the current scaffold. Platform-admin
-- and licensing operations remain disabled until a separate privileged connection
-- and independently authorized service are implemented.

GRANT CONNECT ON DATABASE scopx_dev TO scopx_runtime;
GRANT USAGE ON SCHEMA public TO scopx_runtime;

-- Revoke broad defaults only on the SCOPX tables.
REVOKE ALL ON TABLE
  patients, procedures, media_assets, procedure_reports,
  hospital_memberships, membership_roles, audit_events,
  users, user_sessions, hospitals, roles,
  platform_user_roles, hospital_licenses, license_features,
  license_activations, licensed_devices
FROM PUBLIC;

-- Clinical tenant records. RLS must be enabled and tested before granting access.
GRANT SELECT, INSERT, UPDATE, DELETE
  ON TABLE patients, procedures, media_assets, procedure_reports
  TO scopx_runtime;

-- Reset previous grants on identity tables before applying the narrow grant set.
REVOKE ALL ON TABLE users, user_sessions FROM scopx_runtime;

-- Authentication: only the columns required by the current repository methods.
-- Column-level SELECT is sufficient for the current repository queries.
GRANT SELECT (id, email_normalized, display_name, status, password_hash,
               failed_login_count, lockout_until, last_login_at)
  ON TABLE users TO scopx_runtime;
GRANT UPDATE (password_hash, failed_login_count, lockout_until, last_login_at, updated_at)
  ON TABLE users TO scopx_runtime;

-- Opaque session hashes only; raw bearer tokens must never be persisted.
GRANT SELECT (id, user_id, token_hash, expires_at, revoked_at)
  ON TABLE user_sessions TO scopx_runtime;
GRANT INSERT ON TABLE user_sessions TO scopx_runtime;
GRANT UPDATE (revoked_at) ON TABLE user_sessions TO scopx_runtime;

-- Membership discovery occurs before tenant context is selected. The repository
-- sets scopx.user_id transaction-locally; membership_roles remains tenant-RLS scoped.
GRANT SELECT ON TABLE hospitals, hospital_memberships, membership_roles, roles
  TO scopx_runtime;

-- Audit is append-only for the runtime.
GRANT SELECT, INSERT ON TABLE audit_events TO scopx_runtime;
REVOKE UPDATE, DELETE, TRUNCATE ON TABLE audit_events FROM scopx_runtime;

-- No platform administration or licensing privileges are granted here.
REVOKE ALL ON TABLE
  platform_user_roles, hospital_licenses, license_features,
  license_activations, licensed_devices
FROM scopx_runtime;

-- No CREATE, ALTER, DROP, ownership, sequence, or migration privileges are granted.


-- Optional report version/signature artifacts (migration 010). Keep them
-- append-only for the API runtime; no UPDATE, DELETE, or TRUNCATE privileges.
DO $$
BEGIN
    IF to_regclass('public.procedure_report_versions') IS NOT NULL THEN
        EXECUTE 'GRANT SELECT, INSERT ON TABLE public.procedure_report_versions TO scopx_runtime';
        EXECUTE 'REVOKE UPDATE, DELETE, TRUNCATE ON TABLE public.procedure_report_versions FROM scopx_runtime';
    END IF;
    IF to_regclass('public.procedure_report_signatures') IS NOT NULL THEN
        EXECUTE 'GRANT SELECT, INSERT ON TABLE public.procedure_report_signatures TO scopx_runtime';
        EXECUTE 'REVOKE UPDATE, DELETE, TRUNCATE ON TABLE public.procedure_report_signatures FROM scopx_runtime';
    END IF;
END $$;


-- Optional medical-media retention lifecycle (migration 011). These tables are
-- tenant-RLS scoped. No DELETE/TRUNCATE privileges are granted; deletion events
-- are append-only. The worker/API remain disabled until separately reviewed.
DO $$
BEGIN
    IF to_regclass('public.media_retention_policies') IS NOT NULL THEN
        EXECUTE 'REVOKE ALL ON TABLE public.media_retention_policies FROM PUBLIC';
        EXECUTE 'GRANT SELECT, INSERT, UPDATE ON TABLE public.media_retention_policies TO scopx_runtime';
    END IF;
    IF to_regclass('public.media_legal_holds') IS NOT NULL THEN
        EXECUTE 'REVOKE ALL ON TABLE public.media_legal_holds FROM PUBLIC';
        EXECUTE 'GRANT SELECT, INSERT, UPDATE ON TABLE public.media_legal_holds TO scopx_runtime';
        EXECUTE 'REVOKE DELETE, TRUNCATE ON TABLE public.media_legal_holds FROM scopx_runtime';
    END IF;
    IF to_regclass('public.media_deletion_queue') IS NOT NULL THEN
        EXECUTE 'REVOKE ALL ON TABLE public.media_deletion_queue FROM PUBLIC';
        EXECUTE 'GRANT SELECT, INSERT, UPDATE ON TABLE public.media_deletion_queue TO scopx_runtime';
        EXECUTE 'REVOKE DELETE, TRUNCATE ON TABLE public.media_deletion_queue FROM scopx_runtime';
    END IF;
    IF to_regclass('public.media_deletion_events') IS NOT NULL THEN
        EXECUTE 'REVOKE ALL ON TABLE public.media_deletion_events FROM PUBLIC';
        EXECUTE 'GRANT SELECT, INSERT ON TABLE public.media_deletion_events TO scopx_runtime';
        EXECUTE 'REVOKE UPDATE, DELETE, TRUNCATE ON TABLE public.media_deletion_events FROM scopx_runtime';
    END IF;
END $$;

-- Optional media deletion request workflow (migration 014). The runtime can
-- submit and move requests through its narrow state column grant. Event rows
-- are emitted only by the database trigger; direct event insertion is denied.
DO $$
BEGIN
    IF to_regclass('public.media_deletion_requests') IS NOT NULL THEN
        EXECUTE 'REVOKE ALL ON TABLE public.media_deletion_requests FROM PUBLIC';
        EXECUTE 'REVOKE ALL ON TABLE public.media_deletion_requests FROM scopx_runtime';
        EXECUTE 'GRANT SELECT (id, hospital_id, media_id, status, reason, requested_by_user_id, requested_at, decided_at) ON TABLE public.media_deletion_requests TO scopx_runtime';
        EXECUTE 'GRANT INSERT ON TABLE public.media_deletion_requests TO scopx_runtime';
        EXECUTE 'GRANT UPDATE (status, decision_reason) ON TABLE public.media_deletion_requests TO scopx_runtime';
        EXECUTE 'REVOKE DELETE, TRUNCATE ON TABLE public.media_deletion_requests FROM scopx_runtime';
    END IF;
    IF to_regclass('public.media_deletion_request_events') IS NOT NULL THEN
        EXECUTE 'REVOKE ALL ON TABLE public.media_deletion_request_events FROM PUBLIC';
        EXECUTE 'REVOKE ALL ON TABLE public.media_deletion_request_events FROM scopx_runtime';
        EXECUTE 'GRANT SELECT (hospital_id, request_id, from_status, to_status, occurred_at) ON TABLE public.media_deletion_request_events TO scopx_runtime';
        EXECUTE 'REVOKE INSERT, UPDATE, DELETE, TRUNCATE ON TABLE public.media_deletion_request_events FROM scopx_runtime';
    END IF;
END $$;
