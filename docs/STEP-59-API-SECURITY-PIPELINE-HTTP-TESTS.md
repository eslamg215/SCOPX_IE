# Step 59 — API response security, exception redaction, and login throttling

## Scope

The API's security pipeline was extracted into reusable production extensions and is now exercised by the HTTP integration test hosts. The production API and test hosts use the same middleware and rate-limit policy implementation.

## Implemented pipeline

- Generic exception handling returns HTTP 500 with a fixed `internal_error` response and does not expose exception details.
- Correlation IDs accept only 1–64 ASCII letters, digits, hyphens, or underscores. Invalid or missing values are replaced with a generated identifier.
- Responses include `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Cache-Control: no-store`, and a restrictive Content Security Policy.
- Rate-limit policy permits 120 general API requests per minute and 10 login requests per minute per socket peer address.
- Security-header middleware wraps the rate limiter so even HTTP 429 responses retain the standard response headers.

## HTTP regression coverage

The test host verifies:
- All five response security headers.
- Valid correlation-ID preservation and replacement of an invalid value.
- Generic exception response and absence of the synthetic exception message.
- First ten login requests remain within the limit and the eleventh request receives HTTP 429.
- Security headers remain present on the rate-limit rejection response.

The existing HTTP authentication and PostgreSQL integration tests continue to run with the extracted production pipeline.

## Deployment note

Rate limiting currently partitions on the socket peer address. Deployments behind a reverse proxy must configure trusted forwarded headers and trusted proxy ranges before changing this behavior; do not trust arbitrary client-supplied forwarded headers.

## CI result

The run for this change passed:
- API and Windows workstation builds.
- HTTP authentication/session/tenant tests.
- HTTP security headers, correlation, exception redaction, and login throttling tests.
- PostgreSQL-backed HTTP integration suite using `scopx_runtime`.
- PostgreSQL RLS/grant smoke tests and structural checks.

## Safety and limitations

This step does not enable clinical routes or add any patient-facing functionality. It does not resolve the separate governance approval required for global authentication security-event audit retention and read/export authority.
