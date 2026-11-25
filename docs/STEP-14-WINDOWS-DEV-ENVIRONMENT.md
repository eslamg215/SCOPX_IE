# Step 14 — Windows development environment bootstrap

## Goal
Provide repeatable, low-risk local setup scripts for a Windows workstation. This does not install dependencies automatically and does not make the app clinically usable.

## Files
- `scripts/windows/Preflight.ps1`: checks for Docker, .NET, Git, and Docker Engine readiness.
- `scripts/windows/Start-DevDatabase.ps1`: creates `.env` from the example when absent, starts PostgreSQL, and waits for its health check.
- `scripts/windows/Stop-DevDatabase.ps1`: stops the DB container without deleting its persistent volume.
- `scripts/windows/Backup-DevDatabase.ps1`: writes a local development SQL dump.

## User setup sequence
1. Install Git, Docker Desktop, and .NET 10 SDK from official sources.
2. Open PowerShell in the project folder.
3. If execution policy blocks scripts, use a one-session policy only after reviewing the script: `Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass`.
4. Run `./scripts/windows/Preflight.ps1`.
5. Review `.env`; replace the local development password. Do not reuse a real password.
6. Run `./scripts/windows/Start-DevDatabase.ps1`.
7. Confirm health with `docker compose ps` and `docker compose logs db`.

## Backup
Run `./scripts/windows/Backup-DevDatabase.ps1`. Backup output defaults to `./backups`; keep backups out of source control. This script is a development convenience, not a validated production backup solution. Test restore procedures before relying on backups.


## Safety boundaries
- Use synthetic/test data only. Never enter patient data.
- Do not expose PostgreSQL to public networks.
- The compose port is bound to loopback.
- Database migrations are deliberately not auto-applied by these scripts; they remain draft and must be reviewed, especially the RLS migration.
- Do not treat the preflight as a build/test check. The .NET build and integration tests remain unrun until the SDK and environment are available.
