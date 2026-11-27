# Step 85 — Versioned demo backups

## Changes
- Newly exported demo JSON includes `schemaVersion: 1`.
- Import accepts schema version 1 and legacy versionless backups.
- Unknown future schema versions are rejected.
- Existing record-count, relationship, field-length, media-type, and 10 MiB file-size checks remain in place.

## Verification
- Added structural regression checks for export version metadata and compatibility handling.
- Browser acceptance tests cover export/import round-trip.

## Boundaries
This is a versioned backup format for synthetic local demo data only. It is not a secure clinical backup, archive, or disaster-recovery mechanism.
