# Step 22 — Authentication API and Opaque Sessions

## Implemented in this development slice
- `POST /api/v1/auth/login`: validates basic request bounds, normalizes email, verifies ASP.NET Identity password hashes, returns generic credential errors, and applies the account status/lockout gate.
- Failure counters and escalating lockout are updated atomically by PostgreSQL (`5-9`: 15 min, `10-14`: 1 hour, `15+`: 24 hours).
- Successful authentication resets failure/lockout and records last login, then creates a random 256-bit opaque bearer token.
- Only SHA-256 token hashes are stored in `user_sessions`; raw bearer token is returned once.
- `GET /api/v1/auth/me` checks active, non-revoked, unexpired session and active user.
- `POST /api/v1/auth/logout` revokes a session token hash.
- Eight-hour session expiry; auth endpoints are subject to the API limiter.
- Endpoints are mapped only when `ConnectionStrings:ScopxDb` is configured; otherwise they return 503.
- Migration 006 adds session storage. It is opt-in and only included with `--include-auth-draft` along with migration 005.

## Configure for local disposable development
Set `ConnectionStrings:ScopxDb` via an environment variable or local secrets; do not commit secrets. Before applying, use a disposable local database and review both migrations 005 and 006. The migration runner defaults to excluding 004, 005, and 006 draft security migrations. To opt into auth persistence, pass `--include-auth-draft`; this includes 005 and 006 in sequence.

## Important remaining gaps
- No refresh-token flow or multi-device session-management UI.
- No tenant/hospital context or roles attached to the session yet; this comes in authorization/tenant steps.
- Login is not MFA, and current rate limiting is API-wide rather than per-account/IP. A production design needs layered throttling, monitoring, account recovery, audit, MFA and secrets management.
- No integration tests or .NET build executed in this environment; only offline source checks can be run here.
- This remains a development scaffold. Do not use with real patient data or in clinical workflows.
