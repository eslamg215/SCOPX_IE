$ErrorActionPreference = 'Stop'
$projectRoot = Resolve-Path (Join-Path $PSScriptRoot '../..')
Set-Location $projectRoot

$envFile = Join-Path $projectRoot '.env'
if (-not (Test-Path $envFile)) {
  throw '.env was not found. Run Start-DevDatabase.ps1 first, then add ConnectionStrings__ScopxDb locally. Never commit .env.'
}

$connectionString = $null
foreach ($line in Get-Content -LiteralPath $envFile) {
  if ($line -match '^\s*ConnectionStrings__ScopxDb\s*=\s*(.*)$') {
    $connectionString = $Matches[1].Trim()
    break
  }
}

if ([string]::IsNullOrWhiteSpace($connectionString)) {
  throw 'ConnectionStrings__ScopxDb is missing from .env. Add the local PostgreSQL connection string without sharing or committing it.'
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  throw '.NET SDK was not found. Run Preflight.ps1 and install .NET 10 SDK.'
}

$sdks = dotnet --list-sdks 2>&1
if ($LASTEXITCODE -ne 0 -or -not ($sdks -match '^10\.')) {
  throw '.NET 10 SDK is required. Install it before starting the API.'
}

# Set the connection string only in this PowerShell process; never print it.
$env:ConnectionStrings__ScopxDb = $connectionString
$env:ASPNETCORE_ENVIRONMENT = 'Development'

Write-Host 'Starting SCOPX API in Development mode. No migrations are run automatically.' -ForegroundColor Cyan
Write-Host 'Use synthetic test data only. Press Ctrl+C to stop.' -ForegroundColor Yellow

dotnet run --project 'src/SCOPX.Api/SCOPX.Api.csproj'
if ($LASTEXITCODE -ne 0) {
  throw "SCOPX API exited with code $LASTEXITCODE."
}
