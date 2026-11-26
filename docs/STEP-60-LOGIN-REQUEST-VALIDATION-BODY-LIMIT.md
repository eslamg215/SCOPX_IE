# Step 60 — Login request validation and body-size hardening

## Scope

The authentication login endpoint now has an endpoint-specific 16 KiB request-body limit. The broader API request-body configuration remains unchanged for future separately reviewed media workflows; login credentials do not need large payloads.

## HTTP behavior verified

- Malformed JSON is rejected with HTTP 400.
- Unsupported content types are rejected with HTTP 415.
- Login request bodies larger than 16 KiB are rejected with HTTP 413.
- Valid synthetic login requests continue to work.
- The login rate-limit test now uses a separate fresh HTTP host so malformed/oversized requests do not affect the exact ten-request rate-limit assertion.
- Ten login attempts from one client are admitted to the handler; the eleventh receives HTTP 429.
- The standard response security headers remain present on rate-limit rejections.

## CI

The change passed:
- API build and security test executable.
- HTTP malformed JSON, unsupported media type, and 16 KiB body-limit checks.
- Isolated login rate-limit test.
- Windows workstation build and structural checks.
- PostgreSQL migration, grant, RLS, and Npgsql-backed HTTP integration suite.

## Safety and limitations

All test identities and credentials are synthetic. No clinical route, media upload, patient record, or production service is used. This endpoint-specific cap does not itself validate future media streaming or large-file upload handling.
