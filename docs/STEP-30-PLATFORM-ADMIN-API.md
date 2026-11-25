# Step 30 — Platform Admin API (read-only scaffold)

Added platform administration read endpoints, guarded by active sessions and the `platform_super_admin` platform role:

- `GET /api/v1/platform-admin/overview`: aggregate counts for hospitals, users, active memberships, and active licenses.
- `GET /api/v1/platform-admin/hospitals?limit=25&offset=0`: paginated hospital directory with active member/license counts.
- `GET /api/v1/platform-admin/users?q=&limit=25&offset=0`: paginated searchable user directory.

Security/limits: role is checked server-side for every route; bearer token is validated against active, non-revoked, non-expired session hash; only platform super-admin role grants access; page size capped at 100; SQL is parameterized. No patient data is returned. No admin write operations are included.

Caveats: this remains a development scaffold. Platform role provisioning must be via a controlled bootstrap, all admin access must be audited, and runtime/database integration tests plus compilation have not been run. Do not deploy or use with real PHI.
