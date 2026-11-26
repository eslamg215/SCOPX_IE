# Step 63 — CORS default-deny and unsafe-origin validation tests

## Scope

The PostgreSQL-backed HTTP test host now verifies the actual production default when no `Cors:AllowedOrigins` setting is supplied. The unit-level configuration tests also cover unsafe origin formats.

## Additional verification

- With an empty origin list, even the otherwise valid test web origin receives no `Access-Control-Allow-Origin`.
- Wildcard origins are rejected.
- Non-loopback HTTP origins are rejected.
- Origins containing a path are rejected.
- Origins containing embedded username/password credentials are rejected.
- Duplicate origins are rejected by startup validation.
- Existing exact-origin allowlist and preflight tests continue to pass.

## CI

The full CI run passed for the change, including Windows API/security tests, structural checks, PostgreSQL migrations and grants, RLS smoke tests, and concrete Npgsql-backed HTTP integration tests.

## Safety

No production browser origin is configured by this step. The API remains cross-origin default-deny until an operator supplies exact HTTPS origins after deployment review.
