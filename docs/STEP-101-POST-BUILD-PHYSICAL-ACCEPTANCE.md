# Step 101 — post-build preview handoff and physical acceptance

**Date:** 2026-11-27  
**Branch:** `fix/database-runtime-security`  
**Scope:** Non-clinical preview packaging and user-side device verification only.

## Automated validation baseline

The preview baseline was built from commit `686df7bfaaf892d39178a78f995a4239a46a0804`.

GitHub Actions run [46019126124](https://github.com/eslamg215/SCOPX_IE/actions/runs/46019126124) completed successfully:
- Offline browser acceptance tests — passed.
- Android debug APK preview build — passed.
- API and Windows workstation build — passed.
- PostgreSQL migrations, grants, and RLS smoke tests — passed.

These results establish CI/build success only. They do **not** establish installation or operation on the user's physical Android phone or Windows computer.

## Android preview acceptance

Artifact: `SCOPX.Android-debug-preview` (debug APK; unsigned preview, not a store release).

On a test Android device:
1. Download the APK from the matching GitHub Actions artifact.
2. If Android prompts, allow installation from the browser/file manager used to open the APK.
3. Install and launch SCOPX.
4. Confirm the bundled offline demo opens without requiring a server or network.
5. Navigate between the available demo sections.
6. Create or edit synthetic demo records only; verify the visible result survives closing and reopening the app.
7. Try the supported image import and JSON backup/export flows using non-sensitive sample files.
8. Enable airplane mode and confirm the offline demo remains usable.
9. Record device model, Android version, install result, launch result, offline result, and any error text.

If Android refuses installation because of a signature conflict with an older build, uninstalling the older preview may be necessary. **Uninstalling can erase that app's locally stored demo data.** Export any demo data first if it matters.

## Windows preview acceptance

Artifact: `SCOPX.Workstation-win-x64-preview` (Windows x64 preview package).

On a test Windows computer:
1. Extract the complete ZIP to a normal writable folder.
2. Run `SCOPX.Workstation.exe`.
3. Choose **Open Offline Demo**.
4. Confirm the demo opens and its navigation works.
5. Exercise only synthetic demo records and the available local backup/export flow.
6. Close and reopen the workstation; confirm the demo starts normally.
7. Record Windows version, launch result, offline result, and any error text.

Do not move only the EXE out of the extracted folder; keep the accompanying files and `DemoWeb` directory together.

## Acceptance record

Fill this section after testing on the actual devices:

| Check | Result | Notes |
|---|---|---|
| Android install | Not yet tested | |
| Android first launch | Not yet tested | |
| Android offline use | Not yet tested | |
| Android local persistence | Not yet tested | |
| Android backup/export | Not yet tested | |
| Windows extraction and launch | Not yet tested | |
| Windows offline demo | Not yet tested | |
| Windows local persistence | Not yet tested | |

## Safety and release boundary

- Use synthetic/demo data only. Never enter real patient identifiers, clinical reports, or recordings.
- This preview is not a clinical workstation, medical device release, production deployment, or validated clinical workflow.
- No real endoscope/capture hardware integration is established by this preview.
- Media API routes and report-signing routes remain disabled pending integration, operational controls, and independent review.
- Do not apply draft retention/deletion migrations to production. No deletion worker or physical media deletion is enabled.
- Do not merge the pull request or activate clinical/media functions as part of this step.

## Exit criteria

Step 101 is complete when the checklist has been run on at least one physical Android device and one Windows computer, failures are recorded, and any required fixes are validated in CI. Until the user reports those device results, physical acceptance remains **pending**.
