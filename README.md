# SCOPX.I.E — Project Bootstrap

Medical endoscopy workflow, recording, reporting, and AI-assisted tools. This scaffold is for development only and is **not** clinically validated or suitable for production patient data.

## Initial stack (provisional)
- Backend API: ASP.NET Core on .NET 10 LTS
- Database: PostgreSQL (local development via Docker Compose)
- Desktop: Windows workstation app (WPF planned; device/video capture isolated behind adapters)
- Web admin: separate client, planned after core API contracts
- AI: provider-agnostic gateway; no clinical decisions in initial scaffold

## Start local database
```bash
docker compose up -d db
```

## Next milestones
1. Confirm workstation and hardware capture constraints.
2. Define threat model, tenant isolation, roles, and audit events.
3. Add API auth, migrations, and test suite before patient-data workflows.

## Safety
Never use real patient identifiers or clinical recordings in this scaffold. Keep secrets out of source control.

## API hardening scaffold (v1.6)
The current public scaffold includes a global rate limiter and baseline response headers, plus a generic exception handler. These are development protections only. Authentication, tenant-aware authorization, audit logging, and production deployment hardening are not yet complete. Do not expose the API publicly or use real patient data.

## Identity status
Identity and authentication are still design-only. See `docs/STEP-7-IDENTITY-REVIEW.md` and `docs/STEP-7-IDENTITY-CHECKLIST.md`. We will use ASP.NET Core Identity rather than inventing password/token security. No sign-in feature should be considered active until implementation and runtime tests pass.

### Step 8 status — authorization and tenant isolation
Authorization/tenant isolation review and draft RLS migration are added in `docs/STEP-8-AUTHORIZATION-TENANCY-REVIEW.md`, `docs/STEP-8-AUTHORIZATION-CHECKLIST.md`, and migration `004_tenant_rls_policies.sql`. These are design artifacts, **not applied or runtime-tested**. Review the migration carefully before applying; runtime tenant context must be set transaction-locally only after trusted membership verification.

### Step 9 status — core workflow API contract
Draft route permissions, DTO safety, procedure/report state transitions, concurrency, upload protections, and integration-test expectations are documented in `docs/STEP-9-API-CLINICAL-WORKFLOW-CONTRACT.md` and `docs/STEP-9-API-REVIEW-CHECKLIST.md`. No clinical API endpoints have been implemented yet.

### Step 10 status — device/capture adapter design
A vendor-neutral capture adapter design and onsite hardware checklist are documented in `docs/STEP-10-DEVICE-CAPTURE-ADAPTER-DESIGN.md` and `docs/STEP-10-DEVICE-CAPTURE-CHECKLIST.md`. Olympus CV-180 compatibility is a target only, not confirmed. No hardware has been connected or tested.

### Step 11 status — workstation UI
Windows endoscopy workstation screen layout, usability/accessibility requirements, and prototype acceptance criteria are documented in `docs/STEP-11-WORKSTATION-UI-SPEC.md` and `docs/STEP-11-UI-CHECKLIST.md`. The Windows desktop application has not yet been implemented.

### Step 12 status — AI gateway
AI gateway design, assistive feature boundaries, privacy controls, prompt-injection safeguards, evaluation gate, and rollout checklist are documented in `docs/STEP-12-AI-GATEWAY-SAFETY-ARCHITECTURE.md` and `docs/STEP-12-AI-CHECKLIST.md`. No model/provider integration is active.

### Step 13 status — Web Admin and Android companion
Platform/hospital administration boundaries and mobile companion use cases are documented in `docs/STEP-13-ADMIN-MOBILE-PLATFORM-PLAN.md` and `docs/STEP-13-PLATFORM-CHECKLIST.md`. Neither client is implemented.

### Project review (v2.4)
See `docs/PROJECT-REVIEW-v2.4.md` for the milestone summary, verified checks, remaining blockers, and safe next execution gate. This remains a blueprint/scaffold, not a runnable or clinically validated application.

### Step 14 — Windows development bootstrap

Windows setup scripts and instructions are now included under `scripts/windows/` and `docs/STEP-14-WINDOWS-DEV-ENVIRONMENT.md`. These scripts provide preflight checks, local database startup/stop, and a development SQL dump. They are not yet executed or validated on Windows; use synthetic data only. The scripts do not automatically apply migrations.

### Step 15 status — database tenant policy refinement
Tenant RLS policies in migration 004 were refined into command-specific SELECT/INSERT/UPDATE/DELETE policies after static review. See `docs/STEP-15-DATABASE-SECURITY-REVIEW.md` and `docs/STEP-15-REVIEW.md`. This remains unexecuted SQL; no PostgreSQL server/parser was available during authoring. A manual smoke-test scaffold is in `scripts/tests/tenant-rls-smoke.sql`. Do not treat RLS as sufficient without runtime-role GRANT/REVOKE tests.
- Least-privilege runtime grant template: `scripts/database/Grant-RuntimePrivileges.sql` (review-only; not applied).

## Step 16 — Migration execution starter

A checksum-aware migration runner was added at `scripts/database/Apply-Migrations.py`. It requires Python and PostgreSQL `psql` client tools, plus PG connection environment variables. Migration 004 (draft tenant RLS policies) is intentionally excluded by default; opt-in is `--include-rls-draft` and only for a disposable, reviewed database. Read `docs/STEP-16-DATABASE-EXECUTION-LAYER.md` first. **The runner has only been Python syntax-checked; no database run was performed in this environment.**

### Step 17 — API foundation hardening
- Correlation ID validation/creation and response propagation
- CSP and defensive HTTP headers
- Existing rate-limit, generic error handling, and public metadata retained
- Patient API remains intentionally gated until authentication, tenant isolation, audit, and database integration are tested together
- See `docs/STEP-17-API-FOUNDATION.md`

## Step 18 — Authentication foundation (v2.9)
Added password-policy and adaptive password-hash helper scaffolding. Authentication is still not wired to routes or a persistent user store; see `docs/STEP-18-AUTHENTICATION-FOUNDATION.md`. No real patient data is allowed in this development scaffold.

## Step 19 — Authentication persistence (draft)
Migration `005_user_authentication.sql` adds credential hash and lockout persistence fields. `LoginLockoutPolicy.cs` contains the lockout schedule helper. This is not a complete login implementation; login/session issuance, atomic persistence, recovery, and runtime tests remain pending. Apply only to a disposable local database after reviewing the migration.

## Step 20 — Offline structural validation

Run `python scripts/tests/validate_project.py` for offline structural checks. This does not replace PostgreSQL migration execution, .NET compilation, integration tests, security review, or clinical validation.

## Step 21 — User repository foundation
- PostgreSQL user repository contract and parameterized Npgsql implementation added under `src/SCOPX.Infrastructure/Users`.
- This is not an authentication endpoint; auth wiring remains blocked pending integration review and tests.
- See `docs/STEP-21-USER-REPOSITORY.md`.

## Step 22 — Authentication and opaque sessions (development draft)
- Implemented `/api/v1/auth/login`, `/api/v1/auth/me`, and `/api/v1/auth/logout` endpoints when `ConnectionStrings:ScopxDb` is configured.
- Opaque random bearer tokens; database stores SHA-256 token hashes only. Sessions expire after eight hours and support revocation.
- Failed login counter and escalating lockout are updated atomically by SQL.
- Migration `006_user_sessions.sql` is opt-in with `--include-auth-draft`, which also includes migration 005. Review docs before use.
- Offline checks cover structure only. .NET compilation, database migrations, runtime/security tests remain unverified.
- See `docs/STEP-22-AUTHENTICATION-AND-SESSIONS.md`. This is not clinical software and must not receive real patient data.

## Step 23 — Hospital Context
Authenticated hospital membership listing and server-side hospital-context validation endpoints are now scaffolded. See `docs/STEP-23-HOSPITAL-CONTEXT-AUTHORIZATION.md`. Still requires runtime verification; do not use real patient data.

## Step 25 — Patient API scaffold
Patient CRUD-style endpoints have been added as a development scaffold with active session and hospital membership checks, tenant-local PostgreSQL context, role gating, parameterized queries, bounded list pagination, and soft archive. Details and notes access is restricted more than the patient list. See `docs/STEP-25-PATIENT-API.md`. Do not use real patient data; runtime integration and clinical/security validation remain incomplete.

## Step 26 — Procedure workflow API
A development scaffold for tenant-scoped procedure listing, scheduling, starting, and completion has been added. Status transitions are guarded and procedure rows remain tied to hospital and patient composite references. See `docs/STEP-26-PROCEDURE-API.md`. Not runtime-tested and not for PHI/clinical use.

## Step 27 — Reports Engine (Draft + Review)
The starter now includes a tenant-scoped report draft/review API scaffold. See `docs/STEP-27-REPORTS-ENGINE.md` for endpoints and limitations. Report signing and amendment/version workflows remain intentionally disabled pending audit integration and review controls.

## Step 28 — Media Storage scaffold
- Procedure-linked media upload/list/download routes under `/api/v1/media` with session, tenant membership, and role checks.
- Local filesystem storage using generated hospital/media keys (not user-provided paths); metadata stores size and SHA-256 digest.
- Whitelisted formats: JPEG/PNG/WebP images and MP4/WebM video; maximum file size 50 MiB. Upload body limits are 60 MiB.
- Configure `MediaStorage:Root` to a private storage directory. Keep the directory outside web-root and restrict OS permissions; do not expose storage directly.
- This is a development scaffold. Storage encryption, malware scanning, audit event integration, retention/deletion policies, backups, runtime tests, and deployment hardening are pending. No real patient data.

## Step 29 — Windows Workstation (early desktop shell)
A separate Windows desktop shell is added under `src/SCOPX.Workstation`. It has SCOPX styling, feature placeholders, and an API `/health` connectivity check only. It does not yet implement authentication, clinical screens, media capture, device integration, installer/signing, or clinical readiness. See `docs/STEP-29-WINDOWS-WORKSTATION.md`. Build/run requires Windows and .NET 10 SDK; compilation was not available in this environment.

## Step 30 — Platform Admin API (read-only draft)

Read-only platform admin endpoints are available in `docs/STEP-30-PLATFORM-ADMIN-API.md`. They require an active session and the `platform_super_admin` platform role. The overview, hospital directory, and searchable user directory expose aggregate/admin metadata only. Admin mutations are intentionally deferred pending an append-only audit workflow and security testing. This project is still a non-clinical development scaffold.

## Step 31 — Android Companion Preview
A minimal Kotlin native Android shell is added at `src/SCOPX.Android`. It accepts an HTTPS API base URL, authenticates through `/api/v1/auth/login`, and retrieves active hospital memberships from `/api/v1/hospitals/mine`. The token remains memory-only and is cleared on Activity teardown. No hospital selector, clinical data views, secure token vault, refresh, device integration, or clinical readiness is implemented. Android Studio build/device validation remains required; synthetic data only. See `docs/STEP-31-ANDROID-COMPANION.md`.

## Step 32 — Hardware capture integration foundation

Added capture transport/device descriptor/status/options contracts and an `IDeviceCaptureAdapter` interface to isolate device drivers from workstation UI. A test-only simulator validates state transitions and settings bounds but produces no video. See `docs/STEP-32-HARDWARE-CAPTURE-INTEGRATION.md`. **No physical capture hardware, vendor driver, or Olympus CV-180 compatibility has been tested. This is not for clinical use.**

## Step 33 — AI Analysis Engine Foundation

Provider-neutral AI contracts and fail-closed disabled provider were added. No AI inference is enabled and no patient data is sent to a model. See `docs/STEP-33-AI-ANALYSIS-ENGINE.md` for activation requirements and limitations.

## Step 34 — Security & Audit Hardening foundation

Migration `007_audit_immutability_hardening.sql` adds a DB trigger that blocks audit event UPDATE/DELETE and caps `details` payload size. See `docs/STEP-34-SECURITY-AUDIT-HARDENING.md`. This is a foundation only; audit event emission is not yet wired to all mutation APIs and draft RLS requires a separate security review.

## Step 35 — Deployment and licensing foundation (v2.27)
Deployment guardrails, a development-only restore helper with confirmation prompt, backup/restore guidance, and an explicitly non-enforcing licensing scaffold are included. This is **not** a production installer, a validated licensing system, or a deployment-ready stack. See `docs/STEP-35-DEPLOYMENT-LICENSING.md`. Do not use real patient data.

## Step 36 — Integration readiness checkpoint (v2.28)
A consolidated beta gate and safe synthetic-data integration checklist are included in `docs/STEP-36-FINAL-INTEGRATION-BETA-READINESS.md`. This checkpoint is **not clinically cleared**: no .NET/Android build, PostgreSQL runtime tests, real capture hardware integration, AI inference validation, installer signing, or restore drill has been executed in this environment. Do not use real patient data.

## Step 38 — Membership discovery RLS context repair

The membership repository now sets trusted transaction-local `scopx.user_id` and, when reading role data, `scopx.hospital_id` contexts. `/mine` reads memberships first and then reads role codes per hospital using tenant context derived from returned membership rows. This is a static-code repair only; draft RLS migration 004 remains unapplied and runtime isolation testing is still required. See `docs/STEP-38-TENANT-MEMBERSHIP-RLS-FIX.md`.

### Step 38 — execution readiness review

A direct code review confirms the audit event writer/integration is absent; migration 007 hardens the audit table only. RLS migration 004 remains draft/opt-in and has not been runtime tested. Local build and runtime verification are blocked because .NET SDK, Docker, Gradle, and PostgreSQL CLI are unavailable in the current environment. See `docs/STEP-38-EXECUTION-READINESS-REVIEW.md` for minimum technical-demo gates. No PHI or clinical use.

## Step 39 — Responsive PWA client prototype
A single responsive web client is now included at `src/SCOPX.Web` for laptop and phone preview. It contains dashboard, synthetic patient list/create, demo procedures with basic status transitions, and draft report previews. It uses local browser storage only and has no API connection. Run locally with `python3 -m http.server 8080` from that folder. For phone installation, serve over HTTPS. **Synthetic data only; no PHI and no clinical use.** See `docs/STEP-39-RESPONSIVE-PWA-CLIENT.md`.

### Step 39 verification notes
JavaScript syntax, manifest JSON, local HTML references, HTTP asset responses, and offline structural checks are verified. Automated browser interactions could not run because Chromium navigation is blocked by the execution environment's administrator policy. See `docs/STEP-39-TEST-RESULTS.md` for exact scope.

## Latest demo package
See `docs/RELEASE-v2.33.md` for verified changes and status. Responsive web demo install steps are in `src/SCOPX.Web/INSTALL-AND-TEST.md`. Android wrapper source/build steps are in `src/SCOPX.Android/README.md`.

**Important:** synthetic data only; this is not clinically validated or production-ready. Android APK compilation still requires Android SDK and Gradle on a build machine.

## Step 40 — Database security gate and fail-closed API

The current security review branch deliberately maps only authentication and hospital-membership context routes. A transactional append-only audit writer is now integrated into patient, procedure, report, and media repository read/write operations; audit details contain operational metadata only, not clinical text or patient names. Patient, procedure, report, media, and platform-administration routes are still **not mapped by the API host** until media encryption, malware scanning, retention/backup controls, report signing/amendment controls, read/write audit integration testing, and a separate privileged platform access path are independently verified. Their source files remain in the repository for continued development.

Database migration execution is opt-in for security-sensitive drafts:
- --include-rls-draft includes migration 004.
- --include-auth-draft includes migrations 005 and 006 together.
- --include-audit-hardening-draft includes migration 007.
- --include-tenant-hardening-draft includes migrations 008 and 009 and requires --include-rls-draft.

The runtime grant template is scripts/database/Grant-RuntimePrivileges.sql; the configuration preflight is scripts/database/Validate-RuntimePrivileges.sql. Do not run the grant script until migrations 001-009 have been reviewed and the RLS/runtime-role checks have been performed on a disposable database. Never use the database owner or a BYPASSRLS role as the application runtime role.

The API exposes /health for liveness and /health/ready for a database SELECT 1 connectivity probe; the Windows workstation uses the readiness endpoint but does not claim schema or clinical readiness.

For local development, Prepare-DevRuntimeRole.ps1 creates/verifies a separate non-owner runtime login; Apply-DevDatabaseSecurity.ps1 applies migrations/grants and synthetic checks only after an explicit confirmation and only to scopx_dev. The API launcher refuses owner credentials. Bootstrap-DevAdmin.ps1 creates the first hospital_admin account only when the development users/hospitals tables are empty; it uses ASP.NET Core password hashing and a hidden password prompt.

Windows API startup is available through scripts/windows/Start-DevApi.ps1; it reads ConnectionStrings__ScopxDb from the ignored local .env file, does not print the secret, and never auto-applies migrations. It refuses to start if the API connection string uses the database-owner account; configure the separate least-privilege scopx_runtime role first.

GitHub Actions now builds the API and Windows workstation on pushes and pull requests. A queued or running workflow is not a passing build; check the Actions result before treating compilation as verified. No database migration, privilege script, tenant-isolation integration test, clinical workflow test, or clinical validation is claimed complete by this change.

**Development only. Synthetic data only. No real patient identifiers, reports, or recordings.**


## Step 41 — Protected medical-media foundation

A protected media foundation now validates JPEG/PNG/WebP/MP4/WebM file signatures, applies a 50 MiB upload cap, requires malware scanning through a registered ClamAV INSTREAM adapter, encrypts stored content with AES-256-GCM, and verifies the plaintext hash after authenticated decryption. Missing scanner configuration, daemon errors, malformed responses, and timeouts fail closed. The ClamAV protocol is unencrypted and must be restricted to a private network. The API host still does **not** map media routes.

The SCX2 AES-GCM envelope carries a key ID and supports keyring-based decryption during rotation; automated bulk re-encryption is still outstanding. ClamAV uses an unencrypted TCP protocol and must be isolated on a private network.

See `docs/STEP-41-MEDIA-PROTECTION-FOUNDATION.md` for the encryption envelope, key configuration, and remaining requirements. Production malware scanning, managed key rotation, encrypted backups, a deletion worker, secure object deletion, streaming video storage, end-to-end integration tests, and independent security review remain outstanding. Migration 011 provides only the draft retention/legal-hold/queue schema; it does not enable deletion.

**Development only. Synthetic data only. No real patient identifiers, reports, or recordings.**


## Step 42 — Immutable report versions and signature evidence

Migration 010 adds append-only report snapshots, canonical SHA-256 digests, signature evidence metadata, tenant RLS, and database guards that freeze signed reports. Draft edits create a new report version, and stale updates are rejected by an expected-version check. A signature can only be recorded for the current reviewed version and must carry recent re-authentication metadata.

The migration is **excluded from normal local runs** and requires explicit `--include-rls-draft --include-report-signing-draft` flags. See `docs/STEP-42-REPORT-VERSIONING-SIGNATURE-EVIDENCE.md`.

Important: a provider-neutral RSA-PSS-SHA256 signing/verification service and a password-reauthenticated signing route implementation are present. The route checks the reviewed report version and writes signature evidence plus its audit event in one transaction, but it is deliberately not mapped by the API host and has no UI. Managed signing-key lifecycle, MFA, public-key registry, amendment workflow, and legal e-signature compliance remain incomplete. The service requires an externally managed private key. CI exercises RSA-PSS with ephemeral keys, including verification and tamper/fail-closed cases; the database smoke test uses dummy signature bytes only for DB guard testing. No production or legally qualified signature is issued.

**Development only. Synthetic data only. No real patient identifiers, reports, or recordings.**


## Step 43 — Medical-media retention lifecycle (draft)

Migration 011 adds per-hospital retention policy records (disabled until explicitly configured), per-recording legal holds, a guarded deletion queue, and append-only deletion events. The database rejects queueing while a legal hold is active or before an explicit retention deadline. No default retention duration is assumed. The migration is excluded by default and requires `--include-rls-draft --include-retention-draft` in a disposable reviewed database.

No deletion worker, scheduler, storage-object deletion, tombstone execution, two-person approval workflow, or media API activation is included. See `docs/STEP-43-MEDIA-RETENTION-LIFECYCLE.md`. Synthetic data only.


## Step 44 — Media retention invariant hardening (draft)

Migration 012 enforces one open deletion request per recording and makes legal-hold placement facts immutable, with a one-time auditable release. It is excluded by default and requires `--include-rls-draft --include-retention-draft --include-retention-hardening-draft`. No deletion worker or object deletion is enabled. See `docs/STEP-44-MEDIA-RETENTION-INVARIANT-HARDENING.md`.


## Step 45 — New-media retention deadline stamping (draft)

Migration 013 stamps a retention deadline on new media only when an explicitly enabled per-hospital policy exists. The deadline is based on `created_at + retention_days`; missing/disabled policies force NULL, caller-supplied deadlines and creation timestamps are ignored in favor of server time, and existing media deadlines are immutable and are not recalculated when policy settings change. Migration 013 is excluded by default and requires `--include-rls-draft --include-retention-draft --include-retention-hardening-draft --include-retention-stamping-draft`. CI tests enabled, disabled, and missing policies. No worker or physical deletion is enabled. Synthetic data only.
