# Step 62 — Strict CORS allowlist for browser clients

## Scope

The API now registers a named CORS policy using an explicit `Cors:AllowedOrigins` configuration list. The list is empty by default, so no cross-origin browser origin is allowed unless an operator deliberately configures one.

## Origin validation

- Only exact HTTPS origins are accepted.
- HTTP origins are accepted only for loopback development addresses.
- Wildcards, paths, credentials, queries, fragments, and duplicate origins are rejected during startup configuration.
- No credentialed cross-origin requests are enabled.

## Allowed browser surface

For configured origins only:
- Methods: GET, POST, OPTIONS.
- Request headers: Authorization, Content-Type, X-Hospital-ID, X-Correlation-ID.
- Exposed response header: X-Correlation-ID.
- No `Access-Control-Allow-Credentials` response is emitted.

The policy runs before rate limiting so CORS preflight requests can be handled correctly. The security-header middleware remains outside both CORS and rate limiting.

## HTTP regression coverage

The test suite verifies:
- Wildcard origin configuration fails closed.
- An exact configured origin receives the matching `Access-Control-Allow-Origin`.
- An unlisted origin receives no CORS permission.
- Valid preflight requests succeed only for the configured origin and requested method/headers.
- Credentialed CORS is never enabled.
- Only the correlation identifier is exposed to browser JavaScript.

## Configuration example

Configure an exact origin in the deployment environment, for example:

```text
Cors__AllowedOrigins__0=https://app.example.org
```

Do not configure a wildcard. Do not add a production origin until its ownership, TLS, and deployment environment have been reviewed.

## CI result

The full API/Windows build, security HTTP tests, PostgreSQL migrations/grants/RLS checks, and Npgsql-backed HTTP integration suite passed for this change.

## Safety and limitations

The current repository does not configure any production web origin. CORS is default-deny until an operator supplies an exact origin. This does not enable clinical routes or authorize a user; authentication, session validation, and hospital membership checks remain mandatory.
