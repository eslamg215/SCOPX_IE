#!/usr/bin/env python3
"""Offline structural checks for SCOPX.I.E starter. Does not connect to PostgreSQL or compile .NET."""
from pathlib import Path
import re, sys
ROOT = Path(__file__).resolve().parents[2]
MIGRATIONS = ROOT / 'src/SCOPX.Infrastructure/Database/Migrations'
API = ROOT / 'src/SCOPX.Api'
failures=[]
def check(condition, message):
    print(('PASS: ' if condition else 'FAIL: ') + message)
    if not condition: failures.append(message)
files=sorted(MIGRATIONS.glob('[0-9][0-9][0-9]_*.sql'))
nums=[int(f.name[:3]) for f in files]
check(bool(files), 'SQL migrations exist')
check(nums==sorted(set(nums)), 'migration numbers are unique and sorted')
check(all(a<b for a,b in zip(nums,nums[1:])), 'migration ordering is strictly increasing')
runner=ROOT/'scripts/database/Apply-Migrations.py'
text=runner.read_text(encoding='utf-8')
check('--single-transaction' in text, 'migration runner wraps each migration in transaction')
check('sha256' in text and 'checksum changed' in text.lower(), 'migration runner checks historical checksums')
check('include-rls-draft' in text and 'include-auth-draft' in text and 'include-audit-hardening-draft' in text and 'include-tenant-hardening-draft' in text, 'security-sensitive migration groups require explicit flags')
check('"006_user_sessions.sql"' in text and '"008_role_scope_enforcement.sql"' in text and '"009_hospital_directory_rls.sql"' in text, 'session and tenant-hardening migrations are excluded by default')
check('007_audit_immutability_hardening.sql' in text and 'include_audit_hardening_draft' in text, 'audit hardening draft is excluded by default unless explicitly opted in')
program=(API/'Program.cs').read_text(encoding='utf-8')
check('UseExceptionHandler' in program, 'API has generic exception handler')
check('AddRateLimiter' in program and 'PermitLimit = 120' in program, 'rate limiting configured in API scaffold')
check('X-Correlation-ID' in program and 'Content-Security-Policy' in program, 'request correlation and security headers present')
security=API/'Security'
check((security/'PasswordHashService.cs').exists(), 'password hashing service file exists')
check((security/'PasswordPolicy.cs').exists(), 'password policy file exists')
check((security/'LoginLockoutPolicy.cs').exists(), 'lockout policy helper file exists')
infra=ROOT/'src/SCOPX.Infrastructure'
repo=(infra/'Users/NpgsqlUserRepository.cs').read_text(encoding='utf-8')
contract=(infra/'Users/IUserRepository.cs').read_text(encoding='utf-8')
check((infra/'SCOPX.Infrastructure.csproj').exists(), 'infrastructure project file exists')
check((infra/'Users/UserRecord.cs').exists() and 'PasswordHash' in (infra/'Users/UserRecord.cs').read_text(encoding='utf-8'), 'auth user record model exists')
check('IUserRepository' in contract and 'FindByNormalizedEmailAsync' in contract, 'repository interface includes email lookup')
check('NpgsqlConnection' in repo and 'NpgsqlCommand' in repo, 'PostgreSQL repository implementation uses Npgsql')
check('Parameters.AddWithValue' in repo and '@email' in repo and '@id' in repo, 'repository uses parameterized SQL')
check('RecordSuccessfulLoginAsync' in repo and 'failed_login_count = 0' in repo, 'successful login repository operation resets lockout state')
check('RecordFailedLoginAsync' in repo and 'lockout_until = @lockout' in repo, 'repository persists failure and lockout fields')
check('LIMIT 1' in repo, 'user lookups return at most one row')
rls=(MIGRATIONS/'004_tenant_rls_policies.sql').read_text(encoding='utf-8')
check('CREATE POLICY' in rls and 'scopx_current_hospital_id' in rls, 'draft RLS policies use tenant-context helper')
auth=(MIGRATIONS/'005_user_authentication.sql')
check(auth.exists(), 'authentication persistence draft migration exists')
sessions=(MIGRATIONS/'006_user_sessions.sql')
check(sessions.exists() and 'token_hash' in sessions.read_text(encoding='utf-8'), 'opaque session migration stores token hashes')
auth_endpoints=API/'Authentication/AuthEndpoints.cs'
check(auth_endpoints.exists(), 'login, me, logout endpoint implementation exists')
if auth_endpoints.exists():
    ae=auth_endpoints.read_text(encoding='utf-8')
    check('invalid_credentials' in ae and 'SessionToken.Hash(token)' in ae, 'auth endpoints use generic credential errors and hashed opaque tokens')
    check('SessionLifetime' in ae and 'RecordSuccessfulLoginAsync' in ae, 'login establishes bounded sessions and resets lockout on success')
session_repo=infra/'Sessions/NpgsqlSessionRepository.cs'
check(session_repo.exists() and 'revoked_at IS NULL' in session_repo.read_text(encoding='utf-8'), 'session repository supports active-session lookup and revocation')
check('IncrementFailedLoginAsync' in repo and 'failed_login_count = failed_login_count + 1' in repo, 'failed login counter increment is atomic in SQL')
readme=(ROOT/'README.md').read_text(encoding='utf-8')
check('clinical' in readme.lower() and ('not' in readme.lower() or 'draft' in readme.lower()), 'README includes non-clinical readiness disclaimer')

# Step 23 hospital context authorization checks
hosp=infra/'Hospitals'
check((hosp/'IHospitalMembershipRepository.cs').exists() and (hosp/'NpgsqlHospitalMembershipRepository.cs').exists(), 'hospital membership repository contract and implementation exist')
hrepo=(hosp/'NpgsqlHospitalMembershipRepository.cs').read_text(encoding='utf-8')
check("hm.status = 'active'" in hrepo and "h.status = 'active'" in hrepo, 'membership query filters inactive memberships and hospitals')
check("r.scope = 'hospital'" in hrepo and 'AddWithValue' in hrepo, 'role query excludes platform roles and uses parameterized SQL')
ctx=API/'Tenancy/HospitalContextEndpoints.cs'
check(ctx.exists(), 'hospital context endpoints file exists')
if ctx.exists():
    ce=ctx.read_text(encoding='utf-8')
    check('/mine' in ce and '/context' in ce and 'X-Hospital-ID' in ce, 'hospital membership list and selected context routes exist')
    check('FindActiveForUserAndHospitalAsync' in ce and 'StatusCodes.Status403Forbidden' in ce, 'selected hospital context is validated against membership')
check('MapHospitalContextEndpoints' in program and 'IHospitalMembershipRepository' in program, 'hospital context endpoints and repository are wired in API')

# Step 25 patient API scaffold checks
patients=infra/'Patients'
check((patients/'IPatientRepository.cs').exists() and (patients/'NpgsqlPatientRepository.cs').exists(), 'patient repository contract and PostgreSQL implementation exist')
if (patients/'NpgsqlPatientRepository.cs').exists():
    pr=(patients/'NpgsqlPatientRepository.cs').read_text(encoding='utf-8')
    check('set_config(\'scopx.hospital_id\'' in pr and 'BeginTransactionAsync' in pr, 'patient repository sets tenant context transaction-locally')
    check('hospital_id=@hospital' in pr and 'Parameters.AddWithValue' in pr, 'patient SQL tenant filters and parameterized statements are present')
    check('LIMIT @limit OFFSET @offset' in pr, 'patient list supports bounded pagination query')
    check('status=\'archived\'' in pr and 'archived_at=now()' in pr, 'patient archive is soft archive, not delete')
pe=API/'Patients/PatientEndpoints.cs'
check(pe.exists(), 'patient API endpoint module exists')
if pe.exists():
    ptext=pe.read_text(encoding='utf-8')
    check('/api/v1/patients' in ptext and 'MapPatientEndpoints' in ptext, 'patient routes module mapped')
    check('FindActiveForUserAndHospitalAsync' in ptext and 'AuthHeaderReader.ReadBearer' in ptext, 'patient routes require active session and membership validation')
    check('hospital_admin' in ptext and 'doctor' in ptext and 'auditor' in ptext, 'patient route role restrictions are explicit')
    check('ValidateCreate' in ptext and 'ValidateUpdate' in ptext, 'patient create and update input validation present')
check('MapPatientEndpoints' not in program and 'IPatientRepository' not in program, 'clinical patient routes remain disabled pending audit integration')

# Step 26 procedure workflow API checks
procedures=infra/'Procedures'
check((procedures/'IProcedureRepository.cs').exists() and (procedures/'NpgsqlProcedureRepository.cs').exists(), 'procedure repository contract and implementation exist')
if (procedures/'NpgsqlProcedureRepository.cs').exists():
    proc=(procedures/'NpgsqlProcedureRepository.cs').read_text(encoding='utf-8')
    check("set_config('scopx.hospital_id'" in proc and 'BeginTransactionAsync' in proc, 'procedure repository applies transaction-local tenant context')
    check("status='scheduled'" in proc and "status='in_progress'" in proc and "status='completed'" in proc, 'procedure transitions are guarded by current status')
    check('hospital_id=@h' in proc and 'Parameters.AddWithValue' in proc, 'procedure queries use tenant filter and parameterized SQL')
    check('performed_by_membership_id=@membership' in proc, 'procedure completion records the performing hospital membership')
procedure_api=API/'Procedures/ProcedureEndpoints.cs'
check(procedure_api.exists(), 'procedure workflow endpoints file exists')
if procedure_api.exists():
    pt=procedure_api.read_text(encoding='utf-8')
    check('/patient/{patientId:guid}' in pt and '/start' in pt and '/complete' in pt, 'procedure list, start, and completion routes are defined')
    check('FindActiveForUserAndHospitalAsync' in pt and 'AuthHeaderReader.ReadBearer' in pt, 'procedure routes validate active session and hospital membership')
    check('invalid_procedure_transition' in pt and 'CanCreate' in pt, 'procedure workflow transitions and role policy are checked')
    check('a.Roles.Contains("doctor",StringComparer.OrdinalIgnoreCase)' in pt, 'procedure completion is restricted to a doctor membership')
check('MapProcedureEndpoints' not in program and 'IProcedureRepository' not in program, 'clinical procedure routes remain disabled pending audit integration')

# Step 27 report draft/review workflow checks
reports=infra/'Reports'
check((reports/'ReportRecords.cs').exists() and (reports/'IReportRepository.cs').exists() and (reports/'NpgsqlReportRepository.cs').exists(), 'report records, contract, and repository files exist')
if (reports/'NpgsqlReportRepository.cs').exists():
    rpt=(reports/'NpgsqlReportRepository.cs').read_text(encoding='utf-8')
    check("set_config('scopx.hospital_id'" in rpt and 'BeginTransactionAsync' in rpt, 'report repository sets transaction-local tenant context')
    check("status='draft'" in rpt and "status='review'" in rpt and 'signed_at IS NULL' in rpt, 'report edit and review transitions are guarded')
    check('Parameters.AddWithValue' in rpt and 'hospital_id=@h' in rpt, 'report repository uses parameterized tenant-scoped queries')
    check('created_by_membership_id' in rpt, 'report drafts record the creating hospital membership')
report_api=API/'Reports/ReportEndpoints.cs'
check(report_api.exists(), 'report workflow endpoints file exists')
if report_api.exists():
    rt=report_api.read_text(encoding='utf-8')
    check('/procedure/{procedureId:guid}' in rt and 'submit-review' in rt and 'MapReportEndpoints' in rt, 'report list, draft, and review routes are present')
    check('FindActiveForUserAndHospitalAsync' in rt and 'AuthHeaderReader.ReadBearer' in rt, 'report API validates session and hospital membership')
    check('CanWrite(string[] roles)=>roles.Contains("doctor"' in rt and 'CanReview(string[] roles)=>roles.Contains("doctor"' in rt, 'report drafting and review are restricted to doctors')
check('MapReportEndpoints' not in program and 'IReportRepository' not in program, 'clinical report routes remain disabled pending audit integration')

# Step 28 media storage scaffold checks
media=infra/'Media'
check((media/'MediaRecords.cs').exists() and (media/'IMediaRepository.cs').exists() and (media/'NpgsqlMediaRepository.cs').exists(), 'media metadata records, repository contract and PostgreSQL implementation exist')
if (media/'NpgsqlMediaRepository.cs').exists():
    mr=(media/'NpgsqlMediaRepository.cs').read_text(encoding='utf-8')
    check("set_config('scopx.hospital_id'" in mr and 'BeginTransactionAsync' in mr, 'media metadata repository applies tenant-local context')
    check('hospital_id=@h' in mr and 'Parameters.AddWithValue' in mr, 'media metadata SQL is tenant-scoped and parameterized')
media_api=API/'Media/MediaEndpoints.cs'
check(media_api.exists(), 'media endpoints module exists')
if media_api.exists():
    ma=media_api.read_text(encoding='utf-8')
    check('/upload/{procedureId:guid}' in ma and '/download' in ma and '/procedure/{procedureId:guid}' in ma, 'media upload, download, and list endpoints are present')
    check('MaxBytes=50*1024*1024' in ma and 'Allowed.TryGetValue' in ma and 'SHA256.HashDataAsync' in ma, 'media uploads are size-limited, extension-whitelisted and hashed')
    check('Path.GetFullPath' in ma and 'HospitalId:N' in ma, 'media storage keys are generated and full paths are checked')
check('MapMediaEndpoints' not in program and 'IMediaRepository' not in program, 'media routes remain disabled pending storage security and audit integration')

# Step 29 Windows workstation shell checks
workstation=ROOT/'src/SCOPX.Workstation'
check((workstation/'SCOPX.Workstation.csproj').exists(), 'Windows workstation project exists')
wf=workstation/'WorkstationForm.cs'
check(wf.exists(), 'workstation shell form source exists')
if wf.exists():
    wtext=wf.read_text(encoding='utf-8')
    check('CheckApi' in wtext and '/health' in wtext, 'workstation shell provides API health-only check')
    check('NOT FOR CLINICAL USE' in wtext and 'not connected' in wtext, 'workstation clearly marks clinical warning and placeholder features')
wsproj=(workstation/'SCOPX.Workstation.csproj').read_text(encoding='utf-8')
check('net10.0-windows' in wsproj and 'UseWindowsForms' in wsproj, 'workstation is configured as Windows desktop WinForms app')
check((ROOT/'docs/STEP-29-WINDOWS-WORKSTATION.md').exists(), 'Step 29 workstation documentation exists')

# Step 30 platform admin read-only API checks
platform_admin=API/'PlatformAdmin/PlatformAdminEndpoints.cs'
check(platform_admin.exists(), 'platform admin endpoint module exists')
if platform_admin.exists():
    pa=platform_admin.read_text(encoding='utf-8')
    check('/overview' in pa and '/hospitals' in pa and '/users' in pa, 'platform admin overview, hospital, and user routes exist')
    check('platform_super_admin' in pa and 'SessionToken.Hash(token)' in pa and 'revoked_at IS NULL' in pa, 'platform admin routes require valid active session and super-admin role')
    check('Math.Clamp(limit ?? 25, 1, 100)' in pa and 'Parameters.AddWithValue' in pa, 'platform admin pagination is capped and SQL parameterized')
check('MapPlatformAdminEndpoints' not in program and 'PlatformAdminEndpoints' not in program, 'platform admin routes remain disabled pending separate privileged access')

# Step 31 Android companion scaffold checks
android=ROOT/'src/SCOPX.Android'
check((android/'settings.gradle.kts').exists() and (android/'app/build.gradle.kts').exists(), 'Android Gradle project scaffolding exists')
manifest=android/'app/src/main/AndroidManifest.xml'
activity=android/'app/src/main/java/com/scopx/mobile/MainActivity.kt'
check(manifest.exists() and activity.exists(), 'Android manifest and Kotlin Activity exist')
if manifest.exists():
    am=manifest.read_text(encoding='utf-8')
    check('usesCleartextTraffic=\"false\"' in am and 'allowBackup=\"false\"' in am, 'Android disables cleartext and app backup')
if activity.exists():
    at=activity.read_text(encoding='utf-8')
    check('/api/v1/auth/login' in at and '/api/v1/hospitals/mine' in at, 'Android sign-in and membership API calls are scaffolded')
    check('Executors.newSingleThreadExecutor' in at and 'main.post' in at, 'Android network work is off-main-thread and UI callbacks return to main')
check((ROOT/'docs/STEP-31-ANDROID-COMPANION.md').exists(), 'Step 31 Android companion documentation exists')

# Step 32 capture abstraction checks (test simulator is not a video source)
devices=ROOT/'src/SCOPX.Workstation/Devices'
contracts=devices/'DeviceCaptureContracts.cs'
check(contracts.exists(), 'capture device contracts and adapter boundary exist')
if contracts.exists():
    dc=contracts.read_text(encoding='utf-8')
    check('IDeviceCaptureAdapter' in dc and 'CaptureDeviceDescriptor' in dc and 'CaptureStartOptions' in dc, 'hardware capture API exposes descriptor, status, and start options')
    check('SimulatorCaptureAdapter' in dc and 'no image/video is captured' in dc, 'simulator is explicitly non-video and test-only')
    check('7680' in dc and '4320' in dc and '120' in dc, 'simulator capture parameters are range-validated')
check((ROOT/'docs/STEP-32-HARDWARE-CAPTURE-INTEGRATION.md').exists(), 'Step 32 hardware compatibility checklist and limitations documented')


# Step 33 AI foundation checks (provider is disabled; no inference is claimed)
ai_dir=infra/'AI'
check((ai_dir/'AiAnalysisContracts.cs').exists() and (ai_dir/'AiSafetyPolicy.cs').exists(), 'AI provider-neutral contracts and fail-closed policy exist')
if (ai_dir/'AiAnalysisContracts.cs').exists():
    ac=(ai_dir/'AiAnalysisContracts.cs').read_text(encoding='utf-8')
    check('IAiAnalysisProvider' in ac and 'DisabledAiProvider' in ac and 'No analysis was performed' in ac, 'AI scaffold defaults to disabled and fail-closed')
if (ai_dir/'AiSafetyPolicy.cs').exists():
    asp=(ai_dir/'AiSafetyPolicy.cs').read_text(encoding='utf-8')
    check('MaxPromptCharacters' in asp and 'MaxContextCharacters' in asp and 'SafetyNotice' in asp, 'AI input limits and clinician verification warning are defined')
check((ROOT/'docs/STEP-33-AI-ANALYSIS-ENGINE.md').exists(), 'AI activation prerequisites and limitations are documented')

# Step 34 audit immutability foundation checks
audit_hardening=MIGRATIONS/'007_audit_immutability_hardening.sql'
check(audit_hardening.exists(), 'Step 34 audit immutability migration exists')
if audit_hardening.exists():
    ah=audit_hardening.read_text(encoding='utf-8')
    check('BEFORE UPDATE OR DELETE ON public.audit_events' in ah and 'scopx_reject_audit_mutation' in ah, 'audit event UPDATE and DELETE are blocked by DB trigger')
    check('BEFORE TRUNCATE ON public.audit_events' in ah, 'audit event TRUNCATE is blocked by DB trigger')
    check('pg_column_size(details) <= 8192' in ah, 'audit details payload size is constrained')
check((ROOT/'docs/STEP-34-SECURITY-AUDIT-HARDENING.md').exists(), 'Step 34 security review and deployment checklist exists')
audit_writer=infra/'Audit/NpgsqlAuditEventWriter.cs'
check(audit_writer.exists() and 'INSERT INTO public.audit_events' in audit_writer.read_text(encoding='utf-8'), 'transactional append-only audit writer exists')
for audit_repo in [
    infra/'Patients/NpgsqlPatientRepository.cs',
    infra/'Procedures/NpgsqlProcedureRepository.cs',
    infra/'Reports/NpgsqlReportRepository.cs',
    infra/'Media/NpgsqlMediaRepository.cs'
]:
    check(audit_repo.exists() and 'NpgsqlAuditEventWriter.WriteAsync' in audit_repo.read_text(encoding='utf-8'), f'audit event writes are integrated into {audit_repo.parent.name} repository')

# Step 35 deployment and licensing scaffold checks
deploy_doc=ROOT/'docs/STEP-35-DEPLOYMENT-LICENSING.md'
restore_script=ROOT/'scripts/windows/Restore-DevDatabase.ps1'
license_scaffold=infra/'Licensing/LicenseScaffold.cs'
check(deploy_doc.exists(), 'Step 35 deployment and licensing guardrails documentation exists')
check(restore_script.exists(), 'development restore helper exists')
if restore_script.exists():
    rs=restore_script.read_text(encoding='utf-8')
    check('RESTORE $TargetDatabase' in rs and 'scopx_' in rs and 'ON_ERROR_STOP=1' in rs, 'restore helper requires confirmation, limits target name, and stops on SQL errors')
check(license_scaffold.exists(), 'licensing placeholder scaffold exists')
if license_scaffold.exists():
    ls=license_scaffold.read_text(encoding='utf-8')
    check('DisabledLicenseProvider' in ls and 'IsValid => false' in ls, 'licensing scaffold fails closed and does not claim validation')
check('Step 35' in (ROOT/'README.md').read_text(encoding='utf-8'), 'README records Step 35 scope and production limitations')
api_launcher=ROOT/'scripts/windows/Start-DevApi.ps1'
check(api_launcher.exists(), 'safe local API launcher exists')
if api_launcher.exists():
    launch_text=api_launcher.read_text(encoding='utf-8')
    check('ConnectionStrings__ScopxDb' in launch_text and 'dotnet run --project' in launch_text, 'API launcher loads the local connection string and starts the API')
    check('No migrations are run automatically' in launch_text and 'never print the secret' in launch_text, 'API launcher avoids printing secrets and does not auto-apply migrations')
    check('docker inspect' in launch_text and "healthy" in launch_text, 'API launcher requires a healthy local PostgreSQL container')
env_example=(ROOT/'.env.example').read_text(encoding='utf-8')
check('ConnectionStrings__ScopxDb=' in env_example, 'environment example includes a local API connection string template')

# Membership repository tenant-context repair checks
membership_repo=hosp/'NpgsqlHospitalMembershipRepository.cs'
if membership_repo.exists():
    mrepo=membership_repo.read_text(encoding='utf-8')
    check("set_config('scopx.user_id', @user, true)" in mrepo, 'membership repository sets trusted transaction-local user context for RLS discovery')
    check("set_config('scopx.hospital_id', @hospital, true)" in mrepo, 'membership role lookup sets transaction-local tenant context before RLS-protected joins')
    check('MembershipId' in mrepo and 'rolesSql' in mrepo, 'multi-hospital membership lookup retrieves roles under per-hospital RLS context')
    check('new HospitalMembershipRecord' in mrepo and 'reader.GetGuid(3)' in mrepo, 'hospital context returns the validated membership ID for clinical provenance')

# Step 39 responsive PWA client checks
web=ROOT/'src/SCOPX.Web'
check((web/'index.html').exists() and (web/'styles.css').exists() and (web/'app.js').exists(), 'Step 39 responsive web client files exist')
if (web/'index.html').exists():
    wi=(web/'index.html').read_text(encoding='utf-8')
    check('manifest.webmanifest' in wi and 'styles.css' in wi and 'app.js' in wi, 'web client links manifest, styles and application script')
    check('نسخة عرض ببيانات وهمية' in wi and 'ممنوع إدخال بيانات مرضى حقيقية' in wi, 'web client displays synthetic-data safety warning')
if (web/'app.js').exists():
    wa=(web/'app.js').read_text(encoding='utf-8')
    check('localStorage' in wa and 'patientForm' in wa and 'procedureForm' in wa, 'demo client supports local storage and patient/procedure forms')
    check('scheduled' in wa and 'in_progress' in wa and 'completed' in wa and 'makeReport' in wa, 'demo procedure status flow and report preview generation included')
check((web/'manifest.webmanifest').exists() and (web/'sw.js').exists() and (web/'assets/icon.svg').exists(), 'PWA manifest, service worker and icon assets exist')
check((ROOT/'docs/STEP-39-RESPONSIVE-PWA-CLIENT.md').exists() and 'Step 39' in readme, 'Step 39 PWA run instructions and limitations documented')
check((ROOT/'scripts/tests/pwa_smoke_test.py').exists(), 'PWA HTTP smoke-test script is included')

if failures:
    print(f"\n{len(failures)} check(s) failed."); sys.exit(1)
print(f"\nAll offline structural checks passed. Runtime/build/clinical validation not performed.")
