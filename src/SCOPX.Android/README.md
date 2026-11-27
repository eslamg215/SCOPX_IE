# SCOPX.I.E Mobile Demo (Android WebView wrapper)

This is an Android companion prototype with two separate paths: an offline WebView demo and an optional HTTPS API sign-in scaffold. The bundled demo is **synthetic-data-only** and is not a clinical app. API sign-in does not activate clinical workflows.

## Build APK
1. Install Android Studio with Android SDK Platform 35 and Build Tools.
2. Open this `SCOPX.Android` folder in Android Studio.
3. Let Gradle sync. The project uses Android Gradle Plugin 8.7.3 and Kotlin 2.0.21; use Gradle 8.9.
4. Choose Build > Build Bundle(s) / APK(s) > Build APK(s). The debug APK appears under `app/build/outputs/apk/debug/`.

## App behavior
- Opens the bundled web demo offline in a WebView.
- WebView navigation is restricted to the bundled app-assets origin; external URLs, file URLs, and mixed content are blocked.
- JavaScript and DOM storage are enabled only for the bundled demo; file access and cross-file access remain disabled.
- Demo records are stored locally using WebView local storage.
- Optional API sign-in uses HTTPS and is constrained to the configured origin; it is a connectivity scaffold, not a clinical workflow.
- Synthetic patient/procedure/report/media workflows and JSON import/export only.
- Do not use real patient information or clinical images.

## Limitations
CI builds a debug APK, but physical-device installation and runtime behavior still require user testing. Clinical functionality, hardware capture, and production backend workflows are not enabled.


## CI APK preview
The repository CI now builds a debug APK using Java 17, Android SDK 35, and Gradle 8.9. Download the `SCOPX.Android-debug-preview` artifact from the successful GitHub Actions run. The APK is for synthetic-data testing only and is not production-signed.


## Step 81 — WebView navigation isolation
The Android WebView now permits navigation and resource loading only from the bundled `https://appassets.androidplatform.net/assets/` origin. External origins, file URLs, JavaScript URLs, URL credentials, query strings, and fragments are rejected. Mixed content and file-origin access are disabled. JUnit regression tests run in CI.


## Step 89 — Navigation edge cases
The WebView allowlist rejects missing hosts, percent-encoded paths, and backslash path forms in addition to external origins and file URLs. JUnit regression tests cover these cases.


## Step 91 — Bundled asset parity
The Android WebView assets under `app/src/main/assets/scopx/` are kept byte-for-byte identical to `src/SCOPX.Web/`. CI checks HTML, CSS, JavaScript, manifest, service worker, and icon parity before release artifacts are considered valid.
