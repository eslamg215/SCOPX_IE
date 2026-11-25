$ErrorActionPreference = 'Stop'
$projectRoot = Resolve-Path (Join-Path $PSScriptRoot '../..')
Set-Location $projectRoot
if (-not (Test-Path '.env')) {
  Copy-Item '.env.example' '.env'
  Write-Host 'Created .env from .env.example. Review the local password before starting.' -ForegroundColor Yellow
}
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Docker command not found. Run Preflight.ps1 first.' }
$engine = docker info --format '{{.ServerVersion}}' 2>&1
if ($LASTEXITCODE -ne 0) { throw 'Docker Engine is not running. Start Docker Desktop and retry.' }
docker compose up -d db
if ($LASTEXITCODE -ne 0) { throw 'Compose failed to start PostgreSQL.' }
Write-Host 'PostgreSQL container requested. Checking health...' -ForegroundColor Cyan
$deadline = (Get-Date).AddSeconds(60)
while ((Get-Date) -lt $deadline) {
  $state = docker inspect --format '{{.State.Health.Status}}' scopx-postgres-dev 2>$null
  if ($state -eq 'healthy') { Write-Host 'Database is healthy.' -ForegroundColor Green; exit 0 }
  Start-Sleep -Seconds 2
}
throw 'Database did not report healthy within 60 seconds. Inspect: docker compose logs db'
