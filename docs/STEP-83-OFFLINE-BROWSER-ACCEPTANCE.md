# Step 83 — Offline browser acceptance tests

## Scope
Run real browser interaction tests for the standalone synthetic-data web demo in CI.

## Changes
- Added an Ubuntu workflow job with Python, pinned Playwright 1.55.0, and Playwright-managed Chromium. CI sets `SCOPX_REQUIRE_BROWSER=1` so browser failures cannot silently turn into skips.
- Removed the hard-coded system Chromium path from the smoke script.
- Added a wait for asynchronous FileReader/media rendering before asserting the gallery result.

## Coverage
Dashboard render; synthetic patient creation; procedure creation and state transitions; report preview; synthetic image upload; JSON export contents; local-storage persistence after reload; uncaught JavaScript errors; and a narrow mobile viewport without horizontal overflow.

## Safety boundaries
Tests use only synthetic records and a tiny synthetic PNG fixture. They do not connect to a backend, real device, media store, or clinical system.
