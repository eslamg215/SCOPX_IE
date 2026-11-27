# Step 96 — Android APK asset integrity gate

## Changes
- Added a post-build APK archive inspection in Android CI.
- The gate requires the HTML, CSS, JavaScript, manifest, service worker, and icon under `assets/scopx/`.
- Missing runtime assets fail the Android preview job before artifact upload.
- Structural validation checks that the APK inspection step remains present.

## Verification
The Android CI job must complete the APK build and ZIP archive inspection successfully before its debug artifact is accepted.

## Boundaries
This verifies packaging only. It does not prove correct behavior on every Android device or enable clinical workflows.
