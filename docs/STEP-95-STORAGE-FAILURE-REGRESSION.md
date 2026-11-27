# Step 95 — Local storage quota failure regression

## Changes
- Extended browser acceptance tests to simulate a `QuotaExceededError` from Local Storage.
- Verifies a failed synthetic patient save is rolled back to the last persisted snapshot.
- Verifies the form remains open and the new record is not displayed as saved.

## Verification
This test runs in the required Chromium browser acceptance job with `SCOPX_REQUIRE_BROWSER=1`.

## Boundaries
This tests local synthetic demo persistence only; it does not validate database transaction durability or clinical data storage.
