# Step 17 — API foundation hardening


## Changes in this step
- Added a safe `X-Correlation-ID` response/request trace identifier: accepted only when <=64 chars and ASCII letters/digits/dash/underscore; otherwise a server-generated GUID is used.
- Added Content-Security-Policy `default-src 'none'; frame-ancestors 'none'` for the API scaffold.
- Retained defensive headers: nosniff, deny framing, no referrer, no-store.
- Kept rate limiting and generic exception response.
- Explicitly did not add a patient endpoint: authentication, tenant resolution, RLS, persistence, audit, and clinical DTO validation are not yet integrated as a complete tested boundary. This avoids a false-safe endpoint that accepts PHI before safeguards exist.

## Review
- Correlation ID is validated before echoing, preventing response-header injection.
- No client-supplied hospital ID is accepted as an authorization source.
- System endpoint exposes only non-secret build/service metadata.
- No PHI accepted, stored, logged, or seeded.

## Verification status
- Static source review only. .NET SDK is not available in the authoring environment, so build and endpoint tests were not run.
- These headers are a baseline, not a complete security control. CSP should be revisited when the API serves browser UI assets.

## Next
Implement authentication configuration and test the deny-by-default behavior first, then tenant context resolution and a database-backed patient API. Never expose a patient CRUD endpoint before tenant-isolation tests run against PostgreSQL using the non-owner runtime role.
