# Step 54 — API authentication and session security regression harness

## Scope

This step adds deterministic security regression checks to the existing CI-only test harness. All identities, tokens, timestamps, and database records are synthetic. The PostgreSQL fixture runs inside the existing disposable CI database transaction and is rolled back.

## Implemented checks

### Bearer header parsing

The .NET security test executable now verifies that:
- Empty and non-Bearer authorization headers are rejected.
- A missing bearer value is rejected.
- Tokens shorter than 30 characters and longer than 128 characters are rejected.
- Boundary lengths of 30 and 128 characters are accepted by the parser.
- The Bearer scheme is case-insensitive.
- Whitespace surrounding the opaque token is trimmed.

### Session persistence predicate

The PostgreSQL smoke test now creates synthetic active, expired, revoked, and other-user session rows. It verifies the same active-session predicate used by `NpgsqlSessionRepository`:
`token_hash = @hash AND revoked_at IS NULL AND expires_at > @now`.

The fixture verifies that an expired session, a revoked session, and a session belonging to another user are not returned by the tested lookup predicates.

### Existing tenant and membership regression coverage

The same CI SQL smoke suite continues to exercise tenant-scoped membership and data visibility under the least-privilege runtime role, including cross-tenant write rejection. No clinical API route was enabled by this step.

## CI integration

The existing Windows security-test job executes the expanded .NET security test program. The existing PostgreSQL 17 security-smoke job executes the new synthetic session lifecycle assertions together with the tenant/RLS tests.

## Explicit limitations

This is not a full HTTP integration test of login, logout, `/me`, or `/hospitals/context`. It does not yet prove API-level behavior for disabled users, invalid passwords, lockout transitions, header-to-membership mismatch responses, or session revocation through the HTTP logout endpoint. Those require a dedicated API test host and repository fakes or a disposable PostgreSQL-backed API integration fixture.

The current API continues to fail closed for hospital context when the supplied hospital ID has no active membership for the authenticated user. This step does not alter that behavior or activate patient, procedure, media, report-signing, or media-deletion routes.

## Safety

- Synthetic data only; no patient information or real credentials.
- No production database access.
- No migrations are applied to the user's local database.
- No media deletion request endpoints, approval workflow, queue, worker, or physical deletion are enabled.
