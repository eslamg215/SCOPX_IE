# Step 86 — Windows demo artifact verification

## Changes
- Added a Windows CI packaging check after `dotnet publish`.
- The check requires the published artifact to contain `DemoWeb/index.html`, `app.js`, `styles.css`, and `sw.js`.
- Artifact upload is blocked if any required demo shell asset is missing.
- Added a structural validator check that the packaging guard remains in the workflow.

## Verification
The Windows CI job validates the actual publish output before uploading the workstation artifact. Interactive launch still requires a Windows device.

## Safety
The included demo uses synthetic data only and is not a clinical workstation.
