$ErrorActionPreference = 'Stop'
$projectRoot = Resolve-Path (Join-Path $PSScriptRoot '../..')
Set-Location $projectRoot

$envFile = Join-Path $projectRoot '.env'
if (-not (Test-Path $envFile)) { throw '.env is missing. Run Start-DevDatabase.ps1 first.' }

$values = @{}
foreach ($line in Get-Content -LiteralPath $envFile) {
  if ($line -match '^\s*([A-Za-z0-9_]+)\s*=\s*(.*)$') {
    $values[$Matches[1]] = $Matches[2].Trim()
  }
}
$dbName = $values['POSTGRES_DB']
$dbOwner = $values['POSTGRES_USER']
if ([string]::IsNullOrWhiteSpace($dbName) -or $dbName -notmatch '_dev$') {
  throw 'This helper is restricted to a database name ending in _dev.'
}
if ([string]::IsNullOrWhiteSpace($dbOwner) -or $dbOwner -eq 'scopx_runtime') {
  throw 'POSTGRES_USER must be the local development database owner, not scopx_runtime.'
}
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Docker command not found.' }
$dbHealth = docker inspect --format '{{.State.Health.Status}}' scopx-postgres-dev 2>$null
if ($LASTEXITCODE -ne 0 -or $dbHealth -ne 'healthy') {
  throw 'PostgreSQL is not healthy. Run Start-DevDatabase.ps1 first.'
}

$sql = @'
DO $block$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'scopx_runtime') THEN
        CREATE ROLE scopx_runtime LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
    ELSE
        ALTER ROLE scopx_runtime LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
    END IF;
END
$block$;
'@

Write-Host 'Creating or normalizing the least-privilege scopx_runtime role...' -ForegroundColor Cyan
docker compose exec -T db psql -U $dbOwner -d $dbName --set ON_ERROR_STOP=1 --command $sql
if ($LASTEXITCODE -ne 0) { throw 'Could not create the runtime role.' }

Write-Host ''
Write-Host 'Set a separate strong password for scopx_runtime.' -ForegroundColor Yellow
Write-Host 'At the PostgreSQL prompt, type: password scopx_runtime'
Write-Host 'Enter the new password twice, then type: q'
Write-Host 'The password is entered interactively and is not printed by this script.'
docker compose exec db psql -U $dbOwner -d $dbName
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL password setup did not complete.' }

$checkSql = "SELECT (rolpassword IS NOT NULL AND NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole AND NOT rolreplication AND NOT rolbypassrls AND NOT rolinherit) FROM pg_authid WHERE rolname = 'scopx_runtime';"
$check = docker compose exec -T db psql -U $dbOwner -d $dbName --tuples-only --no-align --command $checkSql
if ($LASTEXITCODE -ne 0 -or ($check | Out-String).Trim() -ne 't') {
  throw 'Runtime role password or least-privilege attributes were not verified.'
}

Write-Host 'Runtime role exists, has a password, and has no elevated role attributes.' -ForegroundColor Green
Write-Host 'Next: apply reviewed migrations and Grant-RuntimePrivileges.sql, then set ConnectionStrings__ScopxDb to Username=scopx_runtime with this separate password.'
