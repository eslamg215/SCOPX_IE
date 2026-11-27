# Step 81 — Android WebView navigation isolation

## Changes
- Added a pure navigation policy allowing only the bundled AndroidX WebViewAssetLoader origin: `https://appassets.androidplatform.net/assets/`.
- The WebView blocks external navigation, cleartext URLs, file URLs, JavaScript URLs, embedded URL credentials, query strings, fragments, and basic path traversal forms.
- Disabled mixed content, file access, content access, and file-origin cross-access. Safe Browsing is enabled.
- Kept JavaScript and DOM storage for the local bundled demo only.

## Verification
- Added JUnit tests for allowed bundled assets and rejected external/unsafe URL forms.
- CI runs Android unit tests before the debug APK build.

## Boundaries
The bundled UI remains an offline synthetic-data demo. This does not activate clinical media, patient API workflows, or hardware capture. Physical Android device testing is still required.
