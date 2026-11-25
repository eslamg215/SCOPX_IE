# Step 14 — Windows development environment bootstrap

## Goal
Provide repeatable, low-risk local setup scripts for a Windows workstation. This does not install dependencies automatically and does not make the app clinically usable.

## Files
- `scripts/windows/Preflight.ps1`: checks for Docker, .NET, Git, and Docker Engine readiness.
- `scripts/windows/Start-DevDatabase.ps1`: creates `.env` from the example when absent, starts PostgreSQL, and waits for its health check.
- `scripts/windows/Stop-DevDatabase.ps1`: stops the DB container without deleting its persistent volume.
- `scripts/windows/Backup-DevDatabase.ps1`: writes a local development SQL dump.
- `scripts/windows/Start-DevApi.ps1`: reads `ConnectionStrings__ScopxDb` from the ignored local `.env` file and launches the API without printing the secret.

## User setup sequence
1. Install Git, Docker Desktop, and .NET 10 SDK from official sources.
2. Open PowerShell in the project folder.
3. If execution policy blocks scripts, use a one-session policy only after reviewing the script: `Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass`.
4. Run `./scripts/windows/Preflight.ps1`.
5. Review `.env`; set a strong local owner password in `POSTGRES_PASSWORD`. The API uses a separate `scopx_runtime` password in `ConnectionStrings__ScopxDb`; never reuse the owner password.
6. Run `./scripts/windows/Start-DevDatabase.ps1`.
7. Confirm health with `docker compose ps` and `docker compose logs db`.
8. Run `./scripts/windows/Prepare-DevRuntimeRole.ps1`. At the psql prompt, type `\\password scopx_runtime`, enter a separate strong password twice, then type `\\q`.
9. Install Python and PostgreSQL client tools (`psql`) if they are not already available. Review the SQL migrations and grant scripts before proceeding.
10. Run `./scripts/windows/Apply-DevDatabaseSecurity.ps1` and type `APPLY SCOPX DEV SECURITY` only when you intend to apply migrations 001-009, least-privilege grants, and synthetic smoke tests to the disposable `scopx_dev` database.
11. Update `ConnectionStrings__ScopxDb` in `.env` to use `Username=scopx_runtime` and the separate runtime password. Never share or commit `.env`.
12. Run `./scripts/windows/Start-DevApi.ps1` in a separate PowerShell window to start the API. It refuses to launch with the database-owner account and does not apply migrations automatically.

## Backup
Run `./scripts/windows/Backup-DevDatabase.ps1`. Backup output defaults to `./backups`; keep backups out of source control. This script is a development convenience, not a validated production backup solution. Test restore procedures before relying on backups.


## Safety boundaries
- Use synthetic/test data only. Never enter patient data.
- Do not expose PostgreSQL to public networks.
- The compose port is bound to loopback.
- Database migrations and runtime grants are deliberately not auto-applied by these scripts; they remain opt-in and must be reviewed, especially RLS and tenant hardening.
- Do not treat the preflight as a build/test check. The .NET build and integration tests remain unrun until the SDK and environment are available.
