# Step 82 — Windows offline demo launch

## Changes
- The Windows workstation project copies `src/SCOPX.Web` into the build output under `DemoWeb/`.
- Added an `Open Offline Demo` button to the workstation shell.
- The launcher resolves the fixed bundled `DemoWeb/index.html` path and opens it with the operating system's default browser.
- Missing files and launch errors produce user-facing messages rather than crashing the workstation.

## Testing
- Added structural checks for bundled web assets and the launcher.
- Windows build and repository CI validate compilation and packaging configuration.
- A real Windows interactive launch test still requires running the packaged EXE on a Windows machine.

## What is usable
The browser-based offline demo includes synthetic patient, procedure, report, media, and JSON backup workflows. The separate WinForms cards remain placeholders and are clearly not connected to clinical services.

## Safety
Synthetic data only. Do not enter real patient information or real endoscopy recordings. This does not enable clinical capture or production API workflows.
