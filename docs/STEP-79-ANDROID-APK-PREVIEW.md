# Step 79 — Android debug APK preview

## Scope
Add a CI build for the existing Kotlin Android WebView companion so a debug APK can be tested on a physical Android phone or emulator.

## Build environment
- Java 17 (Temurin).
- Android SDK Platform 35 and Build Tools 35.0.0.
- Android Gradle Plugin 8.7.3.
- Gradle 8.9.
- Task: `assembleDebug`.
- Artifact: `SCOPX.Android-debug-preview`, retained for seven days.

## How to test
1. Open the successful GitHub Actions run.
2. Download the Android debug APK artifact.
3. Transfer/install it on a compatible Android device or emulator, allowing installation from that source if Android asks.
4. Open the offline demo and use synthetic data only.

## Safety and limitations
This is a debug APK and is not signed for production distribution or Google Play. The mobile client is an offline demo; real patient workflows, clinical capture, and production backend security are not enabled. Do not enter real patient data.
