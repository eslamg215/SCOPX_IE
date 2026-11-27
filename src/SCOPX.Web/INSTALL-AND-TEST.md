# SCOPX.I.E Demo — Local installation and test

**Safety:** demo-only with synthetic data. Do not enter real patient information, clinical findings, or real endoscopy images.

## Browser / Android PWA
1. Serve this directory over HTTPS or localhost (service-worker install requires a secure origin).
2. Open `index.html` in Chrome/Edge on desktop or Android.
3. On Android Chrome: menu → Install app / Add to Home screen.
4. Desktop Chrome/Edge: use the install icon in the address bar if offered.

## Features in this demo build
- Dashboard counts and shortcuts.
- Create and search synthetic patients.
- Create procedures, start, and complete them.
- Auto-create demo reports, edit title/body/status.
- Local demo media gallery: image upload (2 MB/file), procedure linking, delete.
- JSON export and import backup (replaces local demo dataset).
- Responsive mobile layout and offline shell caching.

## Data storage
Patient, procedure, report, and media demo records use browser localStorage. Media data URLs can consume browser quota; limit image size and use synthetic test imagery only. JSON backups contain the entire demo dataset.

## Android APK source
See `../SCOPX.Android/README.md`. APK building requires Android Studio, SDK and Gradle; the current workspace does not include a compiled APK.

## Verification
Run `python3 ../scripts/tests/validate_project.py` and `python3 ../scripts/tests/pwa_smoke_test.py`. The HTTP smoke script checks asset delivery; browser interaction testing may be unavailable depending on the environment.


## Windows workstation preview
The Windows workstation preview includes a button named **Open Offline Demo**. It opens the bundled `SCOPX.Web/index.html` in the default browser. The web files are copied beside the workstation executable under `DemoWeb/` during build. This is the full offline synthetic-data demo; the WinForms Patients/Procedures/Reports/Media cards remain placeholders.


## Automated browser acceptance coverage
The CI browser smoke test exercises synthetic patient creation, procedure state transitions, report generation, synthetic image upload, JSON export contents, local-storage persistence after reload, and a narrow mobile viewport. These checks validate the offline demo only, not clinical behavior.
