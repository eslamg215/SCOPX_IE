# Step 7 — Identity & Authentication Design Review

Status: design-only, not runtime verified.

## Decision
Use ASP.NET Core Identity for password hashing, credential lifecycle, lockout, reset, and MFA primitives. Do not implement custom password hashing or token issuance. For the first desktop/web API deployment, use secure server-side cookie authentication for browser clients or a standards-based OIDC authorization-code flow with PKCE for native clients. Avoid storing long-lived bearer tokens in browser localStorage.

## Account model
- `users` remains the SCOPX domain user table.
- ASP.NET Identity tables are the credential store and must be linked by a stable user ID. A reviewed implementation may either replace the domain `users` table with the Identity user entity or maintain a strict 1:1 mapping; avoid duplicated email/password state.
- Platform Super Admin is provisioned only by an offline/bootstrap command that requires an explicit one-time secret. No default admin password, public signup-to-admin path, or seed credential.

## Minimum controls before enabling sign-in
- Email normalized and unique.
- Password policy configured and breached-password checks considered.
- Account lockout and rate limiting for login/reset routes.
- MFA required for Platform Super Admin; preferably all privileged roles.
- Generic login/reset responses to reduce account enumeration.
- Secure, HttpOnly, SameSite cookies; TLS required outside local development.
- Session invalidation after password reset, role changes, or account disable.
- Audit events for login success/failure, MFA changes, resets, role grants/revokes, and admin bootstrap. Do not log passwords, tokens, or PHI.
- CSRF protection for cookie-authenticated state-changing requests.
- Authorization checks at every endpoint, including tenant/hospital scope. Never trust hospital IDs supplied by client without authorization.

## Tenant isolation
Resolve tenant/hospital context from the authenticated principal and verified membership, not from a client-supplied header alone. Each data query must include tenant scope. Database row-level security can be added as defense-in-depth, but it must be tested with connection pooling and transaction context.

## Test gates
- Unauthenticated request to protected endpoint => 401.
- Authenticated but unauthorized role => 403.
- Hospital A user cannot read/update Hospital B data (including guessed IDs) => 404/403 according to disclosure policy.
- Disabled user/session and revoked sessions rejected.
- Login throttling/lockout verified.
- CSRF and cookie attributes verified.
- Admin MFA enforced.
- Secrets are not committed or exposed in logs.

## Not completed
No Identity implementation, migrations, login endpoints, bootstrap command, MFA, authorization policies, or runtime tests exist yet. The .NET SDK is not available in this environment, so build/test cannot be performed here.
