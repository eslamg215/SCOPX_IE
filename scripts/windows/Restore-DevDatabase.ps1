param(
  [Parameter(Mandatory=$true)][string]$BackupFile,
  [Parameter(Mandatory=$true)][string]$TargetDatabase,
  [string]$Container = 'scopx-postgres-dev',
  [string]$User = 'scopx_local'
)
$ErrorActionPreference = 'Stop'
if ($TargetDatabase -notmatch '^scopx_[a-z0-9_]+$') { throw 'Target DB name must begin with scopx_ and contain only lowercase letters, numbers, and underscores.' }
if (-not (Test-Path -LiteralPath $BackupFile -PathType Leaf)) { throw "Backup file not found: $BackupFile" }
Write-Host 'WARNING: This overwrites the target database. Use only a disposable development database.' -ForegroundColor Yellow
$confirmation = Read-Host "Type RESTORE $TargetDatabase to confirm"
if ($confirmation -ne "RESTORE $TargetDatabase") { throw 'Confirmation phrase did not match; restore cancelled.' }
# Restore through psql. Backup script emits plain SQL, so use psql with stop-on-error.
docker exec $Container psql -U $User -d postgres -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='$TargetDatabase' AND pid <> pg_backend_pid();" | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not terminate sessions; restore aborted.' }
docker exec $Container psql -U $User -d postgres -c "DROP DATABASE IF EXISTS $TargetDatabase; CREATE DATABASE $TargetDatabase;"
if ($LASTEXITCODE -ne 0) { throw 'Could not recreate target database; restore aborted.' }
Get-Content -LiteralPath $BackupFile -Raw | docker exec -i $Container psql -U $User -d $TargetDatabase -v ON_ERROR_STOP=1
if ($LASTEXITCODE -ne 0) { throw 'Restore failed. Inspect database state before proceeding.' }
Write-Host 'Restore command completed. Run smoke tests and verify the restored data before relying on it.' -ForegroundColor Green
