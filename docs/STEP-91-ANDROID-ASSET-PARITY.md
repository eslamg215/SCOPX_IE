# Step 91 — Android bundled demo asset parity

## Finding
The Android WebView's copied demo assets had drifted from the canonical responsive web demo. That meant Android could ship older behavior and miss later fixes.

## Changes
- Synchronized Android bundled HTML, CSS, JavaScript, manifest, service worker, and icon with `src/SCOPX.Web/`.
- Added byte-for-byte structural checks to CI so future drift is detected.

## Verification
- CI structural validation compares all six canonical runtime assets with the Android bundle.
- Android unit tests and APK packaging remain separate CI gates.

## Boundaries
The Android app remains an offline synthetic-data demo with an optional API sign-in scaffold. No clinical workflow is activated.
