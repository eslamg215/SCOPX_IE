# Step 78 — Windows x64 preview packaging

## Scope
Extend CI to create a downloadable development preview of the Windows workstation.

## Build configuration
- Target: `win-x64`.
- Self-contained .NET deployment.
- Single-file publish, with native libraries included for extraction.
- Trimming disabled to avoid unreviewed reflection/UI trimming behavior.
- GitHub Actions artifact name: `SCOPX.Workstation-win-x64-preview`.
- Artifact retention: 7 days.

## How to test
1. Open the successful GitHub Actions run for the branch.
2. Download the `SCOPX.Workstation-win-x64-preview` artifact from the Artifacts section.
3. Extract the ZIP on a Windows x64 computer.
4. Run `SCOPX.Workstation.exe`.
5. Treat the application as a development shell only; API connection must be configured separately.

## Safety and limitations
The preview is unsigned and may trigger Windows SmartScreen warnings. It is not an installer, production release, or clinically validated build. No media routes or physical capture drivers are enabled. Do not use real patient data.
