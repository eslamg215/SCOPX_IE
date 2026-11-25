# Step 15 — Database and Tenant RLS Security Review

**Status:** Static review completed; PostgreSQL execution not performed.

## Findings addressed

1. PostgreSQL policies are permissive by default and combine with OR. The earlier `FOR ALL` write policy also applied to SELECT, so separate SELECT/INSERT/UPDATE/DELETE policies now make intent clearer and prevent a tenant-context membership policy from broadening membership discovery.
2. `hospital_memberships` SELECT is intentionally limited to the authenticated user context (`scopx.user_id`). Mutations are hospital-context scoped and must additionally be blocked by API authorization.
3. Tenant tables now have command-specific policies. `FORCE ROW LEVEL SECURITY` remains defense-in-depth; runtime DB role must not own tables or have `BYPASSRLS`.
4. `audit_events` tenant read/insert policies remain separate. Application role must not receive UPDATE or DELETE privileges on audit records.

## Critical limitations / remaining review

- The SQL function casts settings to UUID; malformed context will error (fail closed, but should be validated by API before setting).
- `membership_roles` still needs role-scope enforcement: only hospital-scope roles may be granted through hospital memberships. Enforce in API plus a database trigger/constraint after role model review.
- `platform_user_roles` is not tenant RLS-scoped and must only be reachable via a restricted platform service path; table grants are not yet implemented.
- `users`, `hospitals`, `roles`, license tables, and activation/device tables have no RLS. Do not expose them to a general runtime DB role until access policies/grants are designed.
- No GRANT/REVOKE privilege script exists yet. RLS alone is not sufficient security.
- Migration 004 is a draft and has not been applied. Existing deployments must not blindly rerun/replace migration files; use migration tracking and reviewed forward migrations.

## Test gate

Run migrations only in a disposable local database. Then use a non-owner, non-BYPASSRLS application role and separate test users/hospitals. Confirm: no context returns zero tenant rows; Hospital A cannot read or mutate Hospital B; malformed context errors; authenticated membership discovery returns only the caller's memberships; audit INSERT is allowed only for the active tenant and UPDATE/DELETE privileges are denied; privileged platform tables are inaccessible to hospital runtime role.

No test below should contain real patient information.

## Privilege script draft
A least-privilege SQL grant template is provided at `scripts/database/Grant-RuntimePrivileges.sql`. It intentionally assumes a separately provisioned `scopx_runtime` role and is not applied. Review it against the target database first; it is a template and does not provision credentials or schema/database access.
