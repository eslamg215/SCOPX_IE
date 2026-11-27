# Step 81 — Android request-origin and redirect hardening

## Scope
Harden the Android companion's optional API login requests after the base URL has passed the HTTPS policy.

## Changes
- Every request must remain on the configured HTTPS origin and use the `/api/v1/` path family.
- Automatic redirects are disabled so bearer tokens are not forwarded to redirect targets.
- Non-success responses expose only the HTTP status code; response bodies are not copied into the UI.
- `HttpURLConnection.disconnect()` runs in a `finally` block.

## Tests
JUnit coverage verifies same-origin HTTPS requests are accepted and host changes, HTTP downgrade, user-info credentials, query strings, and paths outside the API family are rejected. Structural checks verify redirect disabling and connection cleanup.

## Safety boundaries
This hardens the optional mobile login scaffold. It does not enable patient/clinical workflows or establish production backend readiness.
