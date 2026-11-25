# SCOPX.I.E v2.33 — Responsive Demo + Mobile APK Source

## Added / improved
- Responsive PWA with a mobile layout and install manifest.
- Synthetic patient list: create/search and quick procedure workflow.
- Procedures: create, start, complete, and generate a demo report.
- Report editor: edit title, text and draft/review status.
- Media demo library: upload synthetic images up to 2 MB, optionally link to a procedure, and remove local media.
- JSON import/export backup for demo dataset.
- Android WebView shell opens the bundled demo offline, with a separate API login/membership request scaffold.
- Mobile Android project now bundles the web demo assets for offline use.

## Verification performed
- `node --check src/SCOPX.Web/app.js`: passed.
- `scripts/tests/validate_project.py`: passed structural checks.
- `scripts/tests/pwa_smoke_test.py`: 7/7 HTTP smoke checks passed.
- Browser interaction test: not run because browser navigation is blocked by this environment policy.
- Android APK compilation: not performed; Android SDK and Gradle are absent from the build environment.
- Physical device tests and clinical/security validation: not performed.

## Status
This is a comprehensive demo/prototype package with a build-ready Android project source, not a compiled APK and not a clinical production release. Use synthetic data only.
