$ErrorActionPreference = 'Stop'
$projectRoot = Resolve-Path (Join-Path $PSScriptRoot '../..')
Set-Location $projectRoot
docker compose stop db
if ($LASTEXITCODE -ne 0) { throw 'Failed to stop database service.' }
Write-Host 'Database stopped. Persistent volume was not deleted.' -ForegroundColor Green
