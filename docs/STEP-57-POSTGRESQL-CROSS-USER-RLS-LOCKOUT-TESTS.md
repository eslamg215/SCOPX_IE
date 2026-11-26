# Step 57 — PostgreSQL cross-user isolation and persisted lockout enforcement

## Scope

The PostgreSQL-backed HTTP integration suite now exercises two active synthetic users assigned to different hospitals, plus real database-backed failed-login and lockout behavior through the least-privilege runtime connection.

## Added coverage

- A second active synthetic account logs in through the HTTP API using the concrete Npgsql repositories.
- User A's `/hospitals/mine` response contains hospital A and not hospital B.
- User B's `/hospitals/mine` response contains hospital B and not hospital A.
- User B cannot select hospital A through `X-Hospital-ID`.
- Five sequential incorrect-password attempts against user A are rejected.
- The concrete PostgreSQL user repository persists the failed-login count and a future lockout deadline.
- A correct password is rejected while the persisted lockout is active.

## Test execution

These scenarios run in the disposable PostgreSQL 17 CI database after migrations, runtime grants, runtime privilege validation, and the SQL RLS smoke suite. API HTTP requests use only the `scopx_runtime` connection. The owner connection is restricted to synthetic fixture setup and cleanup.

## CI result

The run for this change passed both jobs:
- Windows API/security test build and execution.
- PostgreSQL migrations, grants, RLS, and concrete Npgsql-backed HTTP integration tests.

The integration log confirms both tenant users see only their own hospital, all five failed logins are rejected, the database stores the lockout state, and a correct password is blocked while locked.

## Safety and limits

- Synthetic accounts, hospital names, UUIDs, passwords, and session tokens only.
- No patient or clinical records are created by this test.
- No production database is contacted.
- This does not activate or test clinical endpoints, deletion approval, media deletion, or background workers.
- The current lockout and tenant checks are validated for the present development schema; production deployment still requires a separate security review and operational controls.
