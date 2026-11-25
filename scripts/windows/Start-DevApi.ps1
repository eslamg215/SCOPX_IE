$ErrorActionPreference = 'Stop'
$projectRoot = Resolve-Path (Join-Path $PSScriptRoot '../..')
Set-Location $projectRoot

$envFile = Join-Path $projectRoot '.env'
if (-not (Test-Path $envFile)) {
  throw '.env was not found. Run Start-DevDatabase.ps1 first, then add ConnectionStrings__ScopxDb locally. Never commit .env.'
}

$connectionString = $null
$ownerPassword = $null
foreach ($line in Get-Content -LiteralPath $envFile) {
  if ($line -match '^\s*ConnectionStrings__ScopxDb\s*=\s*(.*)
if ([string]::IsNullOrWhiteSpace($connectionString)) {
  throw 'ConnectionStrings__ScopxDb is missing from .env. Add the local PostgreSQL connection string without sharing or committing it.'
}
if ($connectionString -notmatch '(?i)(^|;)\s*Username\s*=\s*scopx_runtime\s*(;|$)') {
  throw 'The API must connect as the least-privilege scopx_runtime role, never as the POSTGRES_USER database owner. Run Prepare-DevRuntimeRole.ps1, apply reviewed migrations/grants, then update ConnectionStrings__ScopxDb.'
}
if ([string]::IsNullOrWhiteSpace($ownerPassword) -or $connectionString -notmatch '(?i)(^|;)\s*Password\s*=\s*([^;]*)') {
  throw 'Both POSTGRES_PASSWORD and the runtime Password must be present in .env.'
}
$runtimePassword = $Matches[2].Trim()
if ($runtimePassword -eq $ownerPassword) {
  throw 'Use a different password for scopx_runtime than for the database owner.'
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  throw '.NET SDK was not found. Run Preflight.ps1 and install .NET 10 SDK.'
}
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
  throw 'Docker command was not found. Start the local database with Start-DevDatabase.ps1 first.'
}
$dbHealth = docker inspect --format '{{.State.Health.Status}}' scopx-postgres-dev 2>$null
if ($LASTEXITCODE -ne 0 -or $dbHealth -ne 'healthy') {
  throw 'Local PostgreSQL is not healthy. Run scripts/windows/Start-DevDatabase.ps1 and retry.'
}

$sdks = dotnet --list-sdks 2>&1
if ($LASTEXITCODE -ne 0 -or -not ($sdks -match '^10\.')) {
  throw '.NET 10 SDK is required. Install it before starting the API.'
}

# Set the connection string only in this PowerShell process; never print the secret.
$env:ConnectionStrings__ScopxDb = $connectionString
$env:ASPNETCORE_ENVIRONMENT = 'Development'

Write-Host 'Starting SCOPX API in Development mode. No migrations are run automatically.' -ForegroundColor Cyan
Write-Host 'Use synthetic test data only. Press Ctrl+C to stop.' -ForegroundColor Yellow

dotnet run --project 'src/SCOPX.Api/SCOPX.Api.csproj'
if ($LASTEXITCODE -ne 0) {
  throw "SCOPX API exited with code $LASTEXITCODE."
}
) {
    $connectionString = $Matches[1].Trim()
  }
  elseif ($line -match '^\s*POSTGRES_PASSWORD\s*=\s*(.*)
if ([string]::IsNullOrWhiteSpace($connectionString)) {
  throw 'ConnectionStrings__ScopxDb is missing from .env. Add the local PostgreSQL connection string without sharing or committing it.'
}
if ($connectionString -notmatch '(?i)(^|;)\s*Username\s*=\s*scopx_runtime\s*(;|$)') {
  throw 'The API must connect as the least-privilege scopx_runtime role, never as the POSTGRES_USER database owner. Run Prepare-DevRuntimeRole.ps1, apply reviewed migrations/grants, then update ConnectionStrings__ScopxDb.'
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  throw '.NET SDK was not found. Run Preflight.ps1 and install .NET 10 SDK.'
}
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
  throw 'Docker command was not found. Start the local database with Start-DevDatabase.ps1 first.'
}
$dbHealth = docker inspect --format '{{.State.Health.Status}}' scopx-postgres-dev 2>$null
if ($LASTEXITCODE -ne 0 -or $dbHealth -ne 'healthy') {
  throw 'Local PostgreSQL is not healthy. Run scripts/windows/Start-DevDatabase.ps1 and retry.'
}

$sdks = dotnet --list-sdks 2>&1
if ($LASTEXITCODE -ne 0 -or -not ($sdks -match '^10\.')) {
  throw '.NET 10 SDK is required. Install it before starting the API.'
}

# Set the connection string only in this PowerShell process; never print the secret.
$env:ConnectionStrings__ScopxDb = $connectionString
$env:ASPNETCORE_ENVIRONMENT = 'Development'

Write-Host 'Starting SCOPX API in Development mode. No migrations are run automatically.' -ForegroundColor Cyan
Write-Host 'Use synthetic test data only. Press Ctrl+C to stop.' -ForegroundColor Yellow

dotnet run --project 'src/SCOPX.Api/SCOPX.Api.csproj'
if ($LASTEXITCODE -ne 0) {
  throw "SCOPX API exited with code $LASTEXITCODE."
}
) {
    $ownerPassword = $Matches[1].Trim()
  }
}

if ([string]::IsNullOrWhiteSpace($connectionString)) {
  throw 'ConnectionStrings__ScopxDb is missing from .env. Add the local PostgreSQL connection string without sharing or committing it.'
}
if ($connectionString -notmatch '(?i)(^|;)\s*Username\s*=\s*scopx_runtime\s*(;|$)') {
  throw 'The API must connect as the least-privilege scopx_runtime role, never as the POSTGRES_USER database owner. Run Prepare-DevRuntimeRole.ps1, apply reviewed migrations/grants, then update ConnectionStrings__ScopxDb.'
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  throw '.NET SDK was not found. Run Preflight.ps1 and install .NET 10 SDK.'
}
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
  throw 'Docker command was not found. Start the local database with Start-DevDatabase.ps1 first.'
}
$dbHealth = docker inspect --format '{{.State.Health.Status}}' scopx-postgres-dev 2>$null
if ($LASTEXITCODE -ne 0 -or $dbHealth -ne 'healthy') {
  throw 'Local PostgreSQL is not healthy. Run scripts/windows/Start-DevDatabase.ps1 and retry.'
}

$sdks = dotnet --list-sdks 2>&1
if ($LASTEXITCODE -ne 0 -or -not ($sdks -match '^10\.')) {
  throw '.NET 10 SDK is required. Install it before starting the API.'
}

# Set the connection string only in this PowerShell process; never print the secret.
$env:ConnectionStrings__ScopxDb = $connectionString
$env:ASPNETCORE_ENVIRONMENT = 'Development'

Write-Host 'Starting SCOPX API in Development mode. No migrations are run automatically.' -ForegroundColor Cyan
Write-Host 'Use synthetic test data only. Press Ctrl+C to stop.' -ForegroundColor Yellow

dotnet run --project 'src/SCOPX.Api/SCOPX.Api.csproj'
if ($LASTEXITCODE -ne 0) {
  throw "SCOPX API exited with code $LASTEXITCODE."
}
