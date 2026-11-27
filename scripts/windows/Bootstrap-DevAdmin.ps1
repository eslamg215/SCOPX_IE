$ErrorActionPreference = 'Stop'
$projectRoot = Resolve-Path (Join-Path $PSScriptRoot '../..')
Set-Location $projectRoot
$envFile = Join-Path $projectRoot '.env'
if (-not (Test-Path $envFile)) { throw '.env is missing. Run Start-DevDatabase.ps1 first.' }

$values = @{}
foreach ($line in Get-Content -LiteralPath $envFile) {
  if ($line -match '^\s*([A-Za-z0-9_]+)\s*=\s*(.*)$') { $values[$Matches[1]] = $Matches[2].Trim() }
}
$dbName = $values['POSTGRES_DB']
$dbOwner = $values['POSTGRES_USER']
$dbPassword = $values['POSTGRES_PASSWORD']
if ($dbName -ne 'scopx_dev' -or [string]::IsNullOrWhiteSpace($dbOwner) -or $dbOwner -eq 'scopx_runtime' -or [string]::IsNullOrWhiteSpace($dbPassword)) {
  throw 'Bootstrap is restricted to scopx_dev using the local database-owner credentials.'
}
$dbHealth = docker inspect --format '{{.State.Health.Status}}' scopx-postgres-dev 2>$null
if ($LASTEXITCODE -ne 0 -or $dbHealth -ne 'healthy') { throw 'PostgreSQL is not healthy. Run Start-DevDatabase.ps1 first.' }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET 10 SDK is required.' }

$env:POSTGRES_DB = $dbName
$env:POSTGRES_USER = $dbOwner
$env:POSTGRES_PASSWORD = $dbPassword
try {
  Write-Host 'Creating the first development hospital administrator. This requires an empty users/hospitals database.' -ForegroundColor Yellow
  Write-Host 'Use synthetic development identity details only. The password prompt is hidden.' -ForegroundColor Yellow
  dotnet run --project 'src/SCOPX.Bootstrap/SCOPX.Bootstrap.csproj' -- --dev-only
  if ($LASTEXITCODE -ne 0) { throw 'Development administrator bootstrap failed.' }
}
finally {
  Remove-Item Env:POSTGRES_PASSWORD -ErrorAction SilentlyContinue
  Remove-Item Env:POSTGRES_USER -ErrorAction SilentlyContinue
  Remove-Item Env:POSTGRES_DB -ErrorAction SilentlyContinue
}
