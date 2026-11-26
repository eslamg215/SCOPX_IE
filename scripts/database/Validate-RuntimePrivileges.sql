-- SCOPX.I.E runtime-role preflight. Run after migrations 001-009 (and optional 010 when installed) and
-- Grant-RuntimePrivileges.sql, as a database owner, against the target dev DB.
-- This checks configuration only; it does not replace cross-tenant integration tests.
DO $$
DECLARE
    runtime_role name := 'scopx_runtime';
    tbl text;
    required_rls text[] := ARRAY[
        'hospital_memberships','membership_roles','hospitals','patients','procedures',
        'media_assets','procedure_reports','audit_events'
    ];
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = runtime_role) THEN
        RAISE EXCEPTION 'Required role % does not exist', runtime_role;
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_roles
        WHERE rolname = runtime_role
          AND (rolsuper OR rolbypassrls OR rolcreatedb OR rolcreaterole OR rolreplication)
    ) THEN
        RAISE EXCEPTION 'Role % has unsafe elevated attributes', runtime_role;
    END IF;

    FOREACH tbl IN ARRAY required_rls LOOP
        IF to_regclass(format('public.%I', tbl)) IS NULL THEN
            RAISE EXCEPTION 'Required table public.% is missing', tbl;
        END IF;
        IF NOT EXISTS (
            SELECT 1 FROM pg_class c
            WHERE c.oid = to_regclass(format('public.%I', tbl))
              AND c.relrowsecurity AND c.relforcerowsecurity
        ) THEN
            RAISE EXCEPTION 'RLS and FORCE RLS must both be enabled on public.%', tbl;
        END IF;
        IF EXISTS (
            SELECT 1 FROM pg_class c
            WHERE c.oid = to_regclass(format('public.%I', tbl))
              AND pg_get_userbyid(c.relowner) = runtime_role::text
        ) THEN
            RAISE EXCEPTION 'Runtime role must not own public.%', tbl;
        END IF;
    END LOOP;

    -- Migration 010 is optional in local setup; validate its controls when installed.
    FOREACH tbl IN ARRAY ARRAY['procedure_report_versions','procedure_report_signatures'] LOOP
        IF to_regclass(format('public.%I', tbl)) IS NOT NULL THEN
            IF NOT EXISTS (
                SELECT 1 FROM pg_class c
                WHERE c.oid = to_regclass(format('public.%I', tbl))
                  AND c.relrowsecurity AND c.relforcerowsecurity
            ) THEN
                RAISE EXCEPTION 'RLS and FORCE RLS must both be enabled on optional public.%', tbl;
            END IF;
            IF has_table_privilege(runtime_role, format('public.%I', tbl), 'UPDATE')
               OR has_table_privilege(runtime_role, format('public.%I', tbl), 'DELETE')
               OR has_table_privilege(runtime_role, format('public.%I', tbl), 'TRUNCATE') THEN
                RAISE EXCEPTION 'Runtime role has forbidden mutation privileges on public.%', tbl;
            END IF;
            IF NOT has_table_privilege(runtime_role, format('public.%I', tbl), 'SELECT')
               OR NOT has_table_privilege(runtime_role, format('public.%I', tbl), 'INSERT') THEN
                RAISE EXCEPTION 'Runtime role is missing append-only access on public.%', tbl;
            END IF;
        END IF;
    END LOOP;

    IF has_table_privilege(runtime_role, 'public.users', 'SELECT')
       OR has_table_privilege(runtime_role, 'public.user_sessions', 'SELECT') THEN
        RAISE EXCEPTION 'Runtime role has broader table-level identity read access than intended';
    END IF;

    IF NOT has_column_privilege(runtime_role, 'public.users', 'id', 'SELECT')
       OR NOT has_column_privilege(runtime_role, 'public.users', 'email_normalized', 'SELECT')
       OR NOT has_column_privilege(runtime_role, 'public.users', 'display_name', 'SELECT')
       OR NOT has_column_privilege(runtime_role, 'public.users', 'status', 'SELECT')
       OR NOT has_column_privilege(runtime_role, 'public.users', 'password_hash', 'SELECT')
       OR NOT has_column_privilege(runtime_role, 'public.users', 'failed_login_count', 'SELECT')
       OR NOT has_column_privilege(runtime_role, 'public.users', 'lockout_until', 'SELECT')
       OR NOT has_column_privilege(runtime_role, 'public.users', 'last_login_at', 'SELECT')
       OR NOT has_column_privilege(runtime_role, 'public.users', 'password_hash', 'UPDATE')
       OR NOT has_column_privilege(runtime_role, 'public.users', 'failed_login_count', 'UPDATE')
       OR NOT has_column_privilege(runtime_role, 'public.users', 'lockout_until', 'UPDATE')
       OR NOT has_column_privilege(runtime_role, 'public.users', 'last_login_at', 'UPDATE')
       OR NOT has_column_privilege(runtime_role, 'public.users', 'updated_at', 'UPDATE') THEN
        RAISE EXCEPTION 'Runtime role is missing required users privileges';
    END IF;

    IF NOT has_column_privilege(runtime_role, 'public.user_sessions', 'id', 'SELECT')
       OR NOT has_column_privilege(runtime_role, 'public.user_sessions', 'user_id', 'SELECT')
       OR NOT has_column_privilege(runtime_role, 'public.user_sessions', 'token_hash', 'SELECT')
       OR NOT has_column_privilege(runtime_role, 'public.user_sessions', 'expires_at', 'SELECT')
       OR NOT has_column_privilege(runtime_role, 'public.user_sessions', 'revoked_at', 'SELECT')
       OR NOT has_table_privilege(runtime_role, 'public.user_sessions', 'INSERT')
       OR NOT has_column_privilege(runtime_role, 'public.user_sessions', 'revoked_at', 'UPDATE') THEN
        RAISE EXCEPTION 'Runtime role is missing required user_sessions privileges';
    END IF;

    IF has_table_privilege(runtime_role, 'public.platform_user_roles', 'SELECT')
       OR has_table_privilege(runtime_role, 'public.hospital_licenses', 'SELECT')
       OR has_table_privilege(runtime_role, 'public.license_activations', 'SELECT')
       OR has_table_privilege(runtime_role, 'public.licensed_devices', 'SELECT') THEN
        RAISE EXCEPTION 'Runtime role unexpectedly has platform/licensing access';
    END IF;

    IF has_table_privilege(runtime_role, 'public.audit_events', 'UPDATE')
       OR has_table_privilege(runtime_role, 'public.audit_events', 'DELETE')
       OR has_table_privilege(runtime_role, 'public.audit_events', 'TRUNCATE') THEN
        RAISE EXCEPTION 'Runtime role has a forbidden audit mutation privilege';
    END IF;
END $$;

SELECT 'SCOPX runtime privilege preflight passed' AS result;
