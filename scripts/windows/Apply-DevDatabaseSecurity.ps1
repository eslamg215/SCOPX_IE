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
$dbPassword = $values['POSTGRES_PASSWORD']
if ($dbName -ne 'scopx_dev') { throw 'This script is restricted to the disposable scopx_dev database.' }
if ([string]::IsNullOrWhiteSpace($dbOwner) -or [string]::IsNullOrWhiteSpace($dbPassword)) {
  throw 'POSTGRES_USER and POSTGRES_PASSWORD must be set in the ignored .env file.'
}
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Docker command not found.' }
if (-not (Get-Command psql -ErrorAction SilentlyContinue)) { throw 'psql is required on PATH. Install PostgreSQL client tools first.' }
if (-not (Get-Command python -ErrorAction SilentlyContinue)) { throw 'Python is required on PATH to run the migration ledger.' }

$dbHealth = docker inspect --format '{{.State.Health.Status}}' scopx-postgres-dev 2>$null
if ($LASTEXITCODE -ne 0 -or $dbHealth -ne 'healthy') {
  throw 'PostgreSQL is not healthy. Run Start-DevDatabase.ps1 first.'
}

$roleCheckSql = "SELECT (rolpassword IS NOT NULL AND NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole AND NOT rolreplication AND NOT rolbypassrls AND NOT rolinherit) FROM pg_authid WHERE rolname = 'scopx_runtime';"
$roleCheck = docker compose exec -T db psql -U $dbOwner -d $dbName --tuples-only --no-align --command $roleCheckSql
if ($LASTEXITCODE -ne 0 -or ($roleCheck | Out-String).Trim() -ne 't') {
  throw 'scopx_runtime is missing, has no password, or has unsafe attributes. Run Prepare-DevRuntimeRole.ps1 first.'
}

Write-Host 'This will apply the reviewed draft migrations 001-009, runtime grants, and disposable synthetic RLS smoke tests to scopx_dev.' -ForegroundColor Yellow
Write-Host 'It does not delete tables, but it changes the local database schema and role privileges.'
$confirmation = Read-Host 'Type APPLY SCOPX DEV SECURITY to continue'
if ($confirmation -cne 'APPLY SCOPX DEV SECURITY') { throw 'Cancelled. No database changes were requested.' }

$env:PGHOST = 'localhost'
$env:PGPORT = '5432'
$env:PGDATABASE = $dbName
$env:PGUSER = $dbOwner
$env:PGPASSWORD = $dbPassword
try {
  Write-Host 'Applying migrations 001-009...' -ForegroundColor Cyan
  python 'scripts/database/Apply-Migrations.py' --include-rls-draft --include-auth-draft --include-audit-hardening-draft --include-tenant-hardening-draft
  if ($LASTEXITCODE -ne 0) { throw 'Migration runner failed.' }

  Write-Host 'Applying least-privilege runtime grants...' -ForegroundColor Cyan
  psql --no-psqlrc --set ON_ERROR_STOP=1 --dbname $dbName --file 'scripts/database/Grant-RuntimePrivileges.sql'
  if ($LASTEXITCODE -ne 0) { throw 'Runtime grants failed.' }

  Write-Host 'Validating runtime role and RLS...' -ForegroundColor Cyan
  psql --no-psqlrc --set ON_ERROR_STOP=1 --dbname $dbName --file 'scripts/database/Validate-RuntimePrivileges.sql'
  if ($LASTEXITCODE -ne 0) { throw 'Runtime privilege validation failed.' }

  Write-Host 'Running synthetic tenant-isolation and audit smoke tests...' -ForegroundColor Cyan
  psql --no-psqlrc --set ON_ERROR_STOP=1 --dbname $dbName --file 'scripts/tests/tenant-rls-ci-smoke.sql'
  if ($LASTEXITCODE -ne 0) { throw 'Tenant RLS smoke test failed.' }

  Write-Host 'Local database migrations, runtime grants, and smoke tests completed.' -ForegroundColor Green
  Write-Host 'Update ConnectionStrings__ScopxDb in .env to Username=scopx_runtime and the separate runtime password, then use Start-DevApi.ps1.'
}
finally {
  Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}
