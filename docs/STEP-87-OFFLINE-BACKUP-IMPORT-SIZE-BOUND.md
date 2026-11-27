# Step 87 — Offline backup import memory bound

## Scope
Prevent oversized user-selected JSON backups from being fully read into browser memory before validation.

## Changes
- Added a 10 MiB maximum backup file size.
- Empty and oversized files are rejected before calling `file.text()`.
- Existing confirmation and schema validation still run for eligible backup files.

## Tests
The Playwright browser suite imports a deliberately oversized synthetic file and verifies the existing synthetic patient remains present. Structural validation confirms the size check occurs before file reading.

## Safety boundaries
This affects only the offline demo's local JSON import. It does not touch server storage or clinical data.
