$ErrorActionPreference = 'Continue'
Write-Host 'SCOPX.I.E Development Preflight' -ForegroundColor Cyan
$checks = @()
function Check-Command($name, $args) {
  $cmd = Get-Command $name -ErrorAction SilentlyContinue
  if ($cmd) { $script:checks += [pscustomobject]@{Name=$name; Status='FOUND'; Detail=$cmd.Source} }
  else { $script:checks += [pscustomobject]@{Name=$name; Status='MISSING'; Detail='Install and add to PATH'} }
}
Check-Command 'docker'
Check-Command 'dotnet'
Check-Command 'git'
Check-Command 'python'
Check-Command 'psql'
if (Get-Command docker -ErrorAction SilentlyContinue) {
  $dockerVersion = docker --version 2>&1
  $dockerInfo = docker info --format '{{.ServerVersion}}' 2>&1
  if ($LASTEXITCODE -eq 0) { $script:checks += [pscustomobject]@{Name='Docker Engine'; Status='READY'; Detail=$dockerInfo} }
  else { $script:checks += [pscustomobject]@{Name='Docker Engine'; Status='NOT READY'; Detail='Start Docker Desktop, then rerun'} }
}
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
  $sdk = dotnet --list-sdks 2>&1
  if ($LASTEXITCODE -eq 0) { $script:checks += [pscustomobject]@{Name='.NET SDK'; Status='FOUND'; Detail=($sdk -join ', ')} }
}
$script:checks | Format-Table -AutoSize
if ($script:checks.Status -contains 'MISSING' -or $script:checks.Status -contains 'NOT READY') {
  Write-Host 'Preflight incomplete. Resolve MISSING/NOT READY items before setup.' -ForegroundColor Yellow
  exit 1
}
Write-Host 'Preflight checks passed (tool presence only; does not validate project build).' -ForegroundColor Green
