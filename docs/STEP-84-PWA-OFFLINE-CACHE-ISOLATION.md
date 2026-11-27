# Step 84 — PWA offline cache isolation

## Changes
- Service worker ignores non-GET requests and all cross-origin requests.
- Only known same-origin static app-shell assets are eligible for caching.
- Cached index fallback is used only for navigation requests; missing scripts/styles are not replaced with HTML.
- Cache cleanup only removes older cache entries owned by the SCOPX demo cache prefix.

## Verification
Offline structural checks assert origin isolation, method restrictions, app-owned cache cleanup, and navigation-only fallback. Browser smoke tests continue to validate the local UI.

## Safety boundary
The PWA remains an offline synthetic-data demo. This does not add API persistence or enable clinical workflows.
