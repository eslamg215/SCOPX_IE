# Step 35 — Deployment & Licensing Foundation (v2.27)

**Status: development scaffolding only; not a production installer or validated licensing system.**

## Deployment guardrails
- Do not expose the development Compose database port beyond loopback.
- Do not reuse `.env.example` passwords; create a local `.env` and keep it untracked.
- Production deployment must use secrets management, TLS termination, private network segmentation, a non-superuser DB account, reviewed migration plan, monitoring, and tested restore drills.
- Do not place PHI in logs, development backups, or test fixtures.
- Draft migrations 004, 005, and 007 remain opt-in and require review against a disposable database before any deployment.
- No container/API production deployment manifest is supplied in this phase.

## Backup and restore
- `scripts/windows/Backup-DevDatabase.ps1` creates a timestamped dump for the named development database.
- `scripts/windows/Restore-DevDatabase.ps1` restores to a disposable development DB only and requires an explicit confirmation phrase.
- Backups are not verified merely because a dump command succeeded. Validate using `pg_restore --list` for custom-format dumps or restore into a clean disposable DB and run smoke tests.
- Protect backup files with disk encryption and access restrictions. Retention and offsite backup are organization decisions; no automated retention policy is enabled.

## Licensing foundation
The infrastructure folder contains only a contract/policy scaffold. License enforcement is not wired into API or desktop runtime. A real licensing service needs a signed license format, issuer key protection, clock/renewal policy, revocation strategy, offline grace rules, audit, and abuse/recovery procedures. Do not trust unsigned local license text or treat this scaffold as enforceable licensing.

## Beta gate
A beta candidate is blocked until .NET builds, Android build, PostgreSQL migrations and tenant isolation tests, API integration tests, Windows installer tests, device capture compatibility, backup/restore drill, security review, and usability/clinical safety review are recorded.
