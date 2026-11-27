# Step 92 — Windows demo package completeness

## Changes
- Expanded the Windows publish artifact check to require the HTML, JavaScript, CSS, service worker, PWA manifest, and app icon.
- Added structural validation for the expanded artifact gate.

## Verification
The Windows build workflow fails the publish job if any required demo asset is missing. Runtime launch on a physical Windows workstation remains a user-side smoke test.

## Boundaries
This validates packaging of the offline synthetic demo, not clinical desktop integration or real capture hardware.
