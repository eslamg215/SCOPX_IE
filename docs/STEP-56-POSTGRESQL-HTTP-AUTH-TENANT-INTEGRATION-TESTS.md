# Step 56 — PostgreSQL-backed HTTP authentication and tenant integration tests

## Scope

The PostgreSQL CI job now starts the same minimal HTTP test host used by the API security suite, but registers the concrete Npgsql repositories and connects through the least-privilege `scopx_runtime` role. A separate owner connection is used only to create and remove deterministic synthetic fixtures.

## Real PostgreSQL coverage

- Authenticate an active synthetic account through the HTTP login endpoint using the concrete `NpgsqlUserRepository`.
- Resolve `/me` through a persisted opaque session using `NpgsqlSessionRepository`.
- Verify the session is rejected when expired.
- Persist logout revocation and verify the revoked session is rejected on the next request.
- List memberships through `NpgsqlHospitalMembershipRepository` under the runtime role.
- Verify hospital A's membership query cannot disclose hospital B's membership.
- Verify a user's own hospital context is accepted and a different user's hospital is denied.
- Verify disabled and locked synthetic accounts cannot log in.
- Exercise actual runtime column/table grants and the RLS policies created by migrations 004 and 009.

## Test database lifecycle

The PostgreSQL job applies migrations to a disposable PostgreSQL 17 service, creates the non-owner `scopx_runtime` role, applies and validates runtime grants, and runs the existing RLS smoke test. It then installs .NET 10, builds the security test host, and runs the HTTP integration scenarios using two CI-only connection strings.

The owner connection inserts only deterministic synthetic identity, hospital, membership, role, and expired-session fixtures. The test harness deletes those fixtures in a `finally` cleanup path. The runtime connection is the only connection used by the API endpoints and concrete repositories.

## CI result

The run for this change completed successfully:
- API and Windows workstation build: success.
- Fake-repository HTTP integration suite: success.
- PostgreSQL migrations and runtime grant validation: success.
- PostgreSQL RLS and cross-tenant smoke suite: success.
- PostgreSQL-backed HTTP integration suite using `scopx_runtime`: success.

## Safety and limitations

- All database fixtures use synthetic names, IDs, passwords, and tokens.
- The database is a disposable GitHub Actions PostgreSQL service, not a user or production database.
- The owner connection is test-fixture setup/cleanup only; API requests use `scopx_runtime`.
- No patient/procedure/media/report routes, deletion-request routes, approval endpoints, queues, workers, or physical deletion are enabled.
- This proves the current authentication and hospital-context paths against the present draft schema and grants. It is not production certification, an external penetration test, or validation for clinical use.
