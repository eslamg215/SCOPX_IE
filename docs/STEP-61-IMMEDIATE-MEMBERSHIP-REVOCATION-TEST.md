# Step 61 — Immediate hospital-membership revocation integration test

## Scope

The PostgreSQL-backed HTTP integration suite now changes a synthetic user's hospital membership from active to suspended while that user's bearer session remains live, then verifies authorization is recalculated from PostgreSQL on the next hospital-context request.

## Verified behavior

- User A initially logs in and selects hospital A successfully.
- User B is independently authenticated and remains restricted to hospital B.
- The test owner connection changes user A's synthetic hospital membership to `suspended`.
- User A's next `/api/v1/hospitals/context` request for hospital A receives HTTP 403.
- User A's independent `/api/v1/auth/me` request still succeeds because the user account and login session remain active.
- This demonstrates that tenant membership revocation takes effect immediately for tenant access without incorrectly conflating membership status with account/session status.

## CI

The full run passed:
- API and Windows workstation build.
- Authentication, request validation, rate-limit, and security middleware tests.
- PostgreSQL migration and runtime-grant validation.
- RLS and cross-tenant smoke suite.
- Concrete Npgsql HTTP integration tests, including immediate membership suspension.

## Safety

Only synthetic users, hospitals, and memberships are used in the disposable PostgreSQL CI database. The owner connection changes only a test fixture and cleanup removes it. No production database or patient records are involved.
