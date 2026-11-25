# Step 34 — Security & Audit Hardening (foundation)

This stage adds an **append-only database guard** for audit events and documents deployment checks. It is not a security certification.


## Added
- Migration `007_audit_immutability_hardening.sql` installs a trigger rejecting `UPDATE` and `DELETE` on `audit_events`. The migration runner excludes it by default; explicitly opt in with `--include-audit-hardening-draft` only after review on a disposable DB.
- Audit `details` payload constrained to 8 KiB at the database level.
- Tenant RLS migration remains a draft and requires manual review; runtime-role ownership/BYPASSRLS must be checked.
- Operational checklist for credential handling, TLS, backups, access control, tenant isolation, and test evidence.


## Before deployment
1. Run migrations on a disposable database first; review SQL and migration order.
2. Use a dedicated runtime DB role; it must not own tables or have `BYPASSRLS`, superuser, DDL, or migration privileges.
3. Verify RLS policy behavior with separate tenants and empty tenant context. In particular, membership discovery requires trusted `scopx.user_id` context; the current API repository flow must be corrected/tested before draft RLS is enabled.
4. Confirm API HTTPS termination, secret management, log redaction, and non-production data policy.
5. Test append-only audit rejection (UPDATE/DELETE) and allowed INSERT under the runtime role.
6. Test backup restore and key/credential rotation before storing any sensitive data.


## Limitations
- Audit events are not yet wired to every patient, procedure, report, media, or admin mutation endpoint.
- No .NET build, PostgreSQL integration, penetration test, or clinical validation was run in this environment.
- Do not use real patient data.
