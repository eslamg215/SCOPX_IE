# Step 93 — Physical preview test checklist

## Android
- Download the Android debug artifact from a successful CI run and extract `app-debug.apk`.
- Install on Android 8.0/API 26 or newer.
- Launch **Open Offline Demo App**; do not enter API credentials for the offline demo.
- Test synthetic patient/procedure/report flows, a small supported raster image, JSON export, airplane mode, and optional confirmed reset.

## Windows
- Download and extract the self-contained Windows x64 preview artifact.
- Run the workstation EXE and select **Open Offline Demo**.
- Verify the bundled browser demo opens without a running API.

## Troubleshooting
- Android installation blocked: grant the file manager/browser permission to install unknown apps for this installation.
- Existing differently signed debug build: uninstall it before installing the new artifact; this deletes its local demo data.
- Android version too old: the app requires API 26 / Android 8.0 or later.
- Windows SmartScreen may warn because the preview is unsigned; only run a build downloaded from the project's official GitHub Actions artifact.

## Safety
Use synthetic names, procedures, and images only. This checklist does not validate hardware capture, clinical performance, privacy compliance, or production readiness.
