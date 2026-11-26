# Step 55 — HTTP authentication, session, and hospital-context integration tests

## Scope

A real loopback HTTP test host now maps only the existing authentication and hospital-context endpoint groups. The test host uses in-memory fake repositories and synthetic identities; it does not start the production API entry point or connect to PostgreSQL.

## HTTP scenarios covered

- Login success for an active synthetic user and bearer-token issuance.
- Missing, malformed, and unknown bearer-token rejection on `/api/v1/auth/me`.
- Successful authenticated `/me` identity resolution.
- Incorrect password, unknown account, disabled account, and locked account rejection.
- Generic login error body equality for unknown-user and wrong-password cases.
- Anonymous hospital list rejection.
- Authenticated hospital list restricted to the caller's active synthetic membership.
- Missing `X-Hospital-ID` rejection.
- Hospital context acceptance for a matching active membership and HTTP 403 for a hospital without membership.
- Expired session rejection.
- Logout revocation, immediate post-logout rejection, and repeated logout rejection.
- Existing session rejection after the associated user is disabled.

## Isolation and safety

- Kestrel binds to an ephemeral loopback port for the test duration.
- No external network listener is opened.
- All users, hospital IDs, memberships, passwords, and session tokens are synthetic.
- Test repositories are in-memory and scoped to the test host.
- No clinical routes, media routes, report-signing routes, deletion-request routes, or background workers are mapped.
- No production configuration or database connection is loaded by the test host.

## CI

The integration suite runs from the existing `SCOPX.SecurityTests` executable in the Windows CI job, alongside the prior cryptography and parser regression checks. A successful build alone is not considered completion: the HTTP assertions and the PostgreSQL/RLS smoke job must both pass.

## Limits

These tests exercise the current endpoint handlers over HTTP with fake repositories. They do not replace PostgreSQL-backed end-to-end tests of the concrete Npgsql repositories, proxy/forwarded-header deployment tests, or production identity-provider review. The current hospital-context contract remains membership-based; a request for a hospital without an active membership is denied.

## Safety note

The product remains a development scaffold and is not validated for clinical use. No patient data or real credentials are used.
