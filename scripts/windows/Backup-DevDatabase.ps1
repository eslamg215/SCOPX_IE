param([string]$OutputFolder = '.\backups')
$ErrorActionPreference = 'Stop'
$projectRoot = Resolve-Path (Join-Path $PSScriptRoot '../..')
Set-Location $projectRoot
if (-not (Test-Path '.env')) { throw 'No .env file found. Start the dev database setup first.' }
$vars = Get-Content '.env' | Where-Object { $_ -match '^\s*[^#]+=.*$' }
$envMap = @{}
foreach ($line in $vars) { $parts = $line -split '=', 2; $envMap[$parts[0].Trim()] = $parts[1].Trim() }
$db = $envMap['POSTGRES_DB']; if (-not $db) { $db = 'scopx_dev' }
$user = $envMap['POSTGRES_USER']; if (-not $user) { $user = 'scopx_local' }
New-Item -ItemType Directory -Force -Path $OutputFolder | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$outFile = Join-Path $OutputFolder "scopx-dev-$stamp.sql"
$dump = docker exec scopx-postgres-dev pg_dump -U $user -d $db --no-owner --no-privileges
if ($LASTEXITCODE -ne 0) { throw 'Backup failed.' }
$dump | Set-Content -Encoding utf8 $outFile
Write-Host "Backup created: $outFile" -ForegroundColor Green
Write-Host 'Development data only. Confirm backup validity before relying on it.' -ForegroundColor Yellow
