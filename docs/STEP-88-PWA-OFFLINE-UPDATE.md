# Step 88 — PWA offline shell update

## Changes
- Bumped the PWA shell cache to `scopx-demo-shell-v4` so installed clients refresh the updated HTML, CSS, and JavaScript.
- The new service worker calls `skipWaiting()` after precaching the complete shell.
- On activation, it removes only older caches with the SCOPX demo prefix and claims controlled clients.
- Cross-origin requests and non-GET requests remain outside the cache strategy.

## Verification
- Structural checks validate cache version and activation behavior.
- Browser acceptance test waits for service-worker readiness, switches the browser context offline, reloads, and verifies the cached dashboard is available.

## Boundaries
This is an offline static-shell cache, not offline synchronization or a multi-device data backup. Synthetic data remains local to the browser profile.
