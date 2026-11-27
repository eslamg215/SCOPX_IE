# Step 90 — Startup validation of local demo state

## Changes
- Startup now validates the browser's stored dataset with the same bounded schema used for JSON backup imports.
- Invalid or corrupted data is replaced with the synthetic starter dataset.
- Legacy versionless backups remain compatible under the existing schema rules.

## Verification
- Browser acceptance test writes a malformed local dataset, reloads the app, and verifies that the synthetic starter data is restored.
- Structural checks confirm startup uses the shared backup validator.

## Boundaries
This only validates local synthetic demo data. It is not a migration or recovery strategy for clinical databases.
