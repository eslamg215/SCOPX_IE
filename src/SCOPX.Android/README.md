# SCOPX.I.E Mobile Demo (Android WebView wrapper)

This is an offline Android wrapper around the responsive SCOPX.I.E demo web client. It is a **synthetic-data-only prototype**. It is not a clinical app and is not connected to a backend.

## Build APK
1. Install Android Studio with Android SDK Platform 35 and Build Tools.
2. Open this `SCOPX.Android` folder in Android Studio.
3. Let Gradle sync. The project uses Android Gradle Plugin 8.7.3 and Kotlin 2.0.21; use Gradle 8.9.
4. Choose Build > Build Bundle(s) / APK(s) > Build APK(s). The debug APK appears under `app/build/outputs/apk/debug/`.

## App behavior
- Opens the bundled web demo offline in a WebView.
- Demo records are stored locally using WebView local storage.
- Synthetic patient/procedure/report/media workflows and JSON import/export only.
- Do not use real patient information or clinical images.

## Limitations
This environment does not have Android SDK or Gradle installed, so APK compilation and device testing have not been completed. This project is a build-ready wrapper source, not an APK. Clinical functionality, hardware capture, backend connectivity, and security validation are not enabled.


## CI APK preview
The repository CI now builds a debug APK using Java 17, Android SDK 35, and Gradle 8.9. Download the `SCOPX.Android-debug-preview` artifact from the successful GitHub Actions run. The APK is for synthetic-data testing only and is not production-signed.
