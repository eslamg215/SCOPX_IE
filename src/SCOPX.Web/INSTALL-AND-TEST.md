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


## Offline cache safety
The service worker caches only the app's same-origin static shell. It does not intercept cross-origin requests or non-GET requests, and it only falls back to the cached index for navigation requests. Cache cleanup is limited to SCOPX demo cache names.


## Backup import validation
JSON imports are checked for supported record fields, unique IDs, valid patient/procedure relationships, allowed demo statuses, bounded field and collection sizes, and image data URLs before the current local dataset can be replaced. Invalid backups are rejected.


## Local storage recovery (Step 83)
The demo keeps the last successfully saved snapshot. If local browser storage is full or unavailable during a write, the change is rolled back to the last saved snapshot and the UI reports that it was not saved. Export backups regularly during testing; browser storage is not a clinical record store.


## Demo image validation (Step 84)
Only PNG, JPEG, GIF, and WebP images up to 2 MiB are accepted. The app checks the file's binary signature rather than trusting its extension or browser MIME label. SVG and other formats are rejected. These restrictions apply to the synthetic offline demo only.


## Backup format version (Step 85)
New exports include `schemaVersion: 1`. Imports accept version 1 and legacy backups without a version; unknown future schema versions are rejected to avoid silent misinterpretation. Keep a copy of important demo backups outside the browser profile.


## Resetting the synthetic demo (Step 86)
Use **إعادة الضبط** only when you want to discard locally added demo records and restore the sample dataset. The app asks for confirmation first. Export a JSON demo backup before resetting if you want to retain your test records.


## Keyboard navigation (Step 87)
Press Tab from the top of the page to reach **تخطي إلى المحتوى الرئيسي**, then press Enter to move focus into the main content. Navigation controls expose their active and expanded state to assistive technology.


## Offline app-shell update (Step 88)
After a new service-worker build is installed, the updated cached shell activates and takes control of open app pages. The browser acceptance suite verifies that the demo can reload from the cache while offline. Locally entered synthetic data remains in browser Local Storage; export backups separately.


## Local data recovery at startup (Step 90)
On startup, the app validates stored synthetic data. If the saved structure is malformed or unsupported, it restores the sample dataset rather than attempting to render unsafe or inconsistent records.


## Step 93 — Device testing checklist
### Android
Follow the Android physical-device steps in `../SCOPX.Android/README.md`. The offline demo should open without server credentials and should use only synthetic records and images.

### Windows
1. Download and extract the `SCOPX.Workstation-win-x64-preview` artifact.
2. Run the SCOPX workstation executable.
3. Choose **Open Offline Demo**.
4. Confirm the bundled demo opens in the default browser and can be used without a running API.

### Safe test sequence
- Create a synthetic patient and procedure.
- Start and complete the procedure; confirm a draft demo report appears.
- Upload only a synthetic raster image within the 2 MiB limit.
- Export a JSON demo backup.
- Test the offline app shell.
- Reset the synthetic dataset only after saving any demo backup you want to keep.

Do not enter real patient details, clinical findings, or real endoscopy images. These steps verify the preview UI only; they do not validate capture hardware, clinical workflow, or production deployment.


## Storage failure test (Step 95)
The automated browser suite simulates a browser storage quota failure during a synthetic patient save. The expected result is a warning, no new saved record, and an open form so the user can retry after freeing space.
