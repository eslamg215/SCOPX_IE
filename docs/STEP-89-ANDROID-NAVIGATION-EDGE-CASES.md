# Step 89 — Android WebView navigation edge-case hardening

## Changes
- Made host comparison null-safe for malformed URI authorities.
- Requires the raw and decoded asset paths to match, rejecting percent-encoded path variants.
- Rejects backslash path forms.
- Added JUnit regression cases for missing host, encoded traversal, and backslash path forms.

## Verification
- Android unit tests run in CI before APK packaging.
- Structural validation confirms the additional path checks.

## Boundaries
The allowlist remains limited to the bundled offline demo asset origin. It does not enable external browsing or clinical API routes.
