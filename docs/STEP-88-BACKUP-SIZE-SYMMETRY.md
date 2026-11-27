# Step 88 — Backup export/import size symmetry

## Scope
Ensure a backup produced by the offline demo is eligible for re-import under the same file-size limit.

## Changes
- Moved the shared 10 MiB limit to the app initialization scope.
- Export now measures the generated Blob and refuses oversized backups with a clear message.
- Import continues to reject empty/oversized files before reading them.
- Browser acceptance verifies the exported synthetic backup is at or below the shared limit.

## Safety boundaries
This applies only to local demo JSON backup files. It does not change server backup policy or clinical data retention.
