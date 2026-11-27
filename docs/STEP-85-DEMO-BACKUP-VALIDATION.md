# Step 85 — Demo backup import validation

## Changes
- Added strict validation for JSON backup structure before replacing local demo records.
- Enforced unique record IDs and valid patient/procedure relationships.
- Enforced allowed demo workflow statuses, field length bounds, and collection size limits.
- Media entries must reference an existing procedure or remain unlinked, contain an image data URL, and remain under the bounded encoded-size limit.
- Invalid imports are rejected before the existing local dataset is changed.

## Tests
- Expanded browser smoke tests to attempt importing a malformed backup and confirm existing synthetic records remain visible.
- Added structural validation checks for the backup validator and count bounds.

## Safety
This is validation for the offline synthetic-data demo only. It does not make browser local storage suitable for patient records or clinical media.
