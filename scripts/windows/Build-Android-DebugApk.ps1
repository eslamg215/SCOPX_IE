param([string]$AndroidProject = (Join-Path $PSScriptRoot "../../src/SCOPX.Android"))
$ErrorActionPreference = "Stop"
$project = (Resolve-Path $AndroidProject).Path
Write-Host "Build SCOPX.I.E Android debug APK from: $project"
if (-not (Get-Command gradle -ErrorAction SilentlyContinue)) {
  Write-Error "Gradle not found. Install Android Studio, set up SDK, and open src/SCOPX.Android to sync Gradle first."
}
Push-Location $project
try { gradle assembleDebug; Write-Host "APK expected at app/build/outputs/apk/debug/app-debug.apk" }
finally { Pop-Location }
