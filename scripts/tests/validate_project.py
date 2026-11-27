#!/usr/bin/env python3
"""Offline structural checks for SCOPX.I.E starter. Does not connect to PostgreSQL or compile .NET."""
from pathlib import Path
import re, sys
ROOT = Path(__file__).resolve().parents[2]
MIGRATIONS = ROOT / 'src/SCOPX.Infrastructure/Database/Migrations'
API = ROOT / 'src/SCOPX.Api'
WORKSTATION = ROOT / 'src/SCOPX.Workstation'
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
check('include-rls-draft' in text and 'include-auth-draft' in text and 'include-audit-hardening-draft' in text and 'include-tenant-hardening-draft' in text and 'include-report-signing-draft' in text and 'include-retention-draft' in text and 'include-retention-hardening-draft' in text and 'include-retention-stamping-draft' in text and 'include-media-deletion-requests-draft' in text, 'security-sensitive migration groups require explicit flags')
check('"006_user_sessions.sql"' in text and '"008_role_scope_enforcement.sql"' in text and '"009_hospital_directory_rls.sql"' in text, 'session and tenant-hardening migrations are excluded by default')
check('007_audit_immutability_hardening.sql' in text and 'include_audit_hardening_draft' in text, 'audit hardening draft is excluded by default unless explicitly opted in')
check('010_report_versioning_signatures.sql' in text and 'include_report_signing_draft' in text, 'report signing evidence migration is excluded by default unless explicitly opted in')
check('011_media_retention_lifecycle.sql' in text and 'include_retention_draft' in text, 'media retention lifecycle migration is excluded by default unless explicitly opted in')
check('012_media_retention_invariant_hardening.sql' in text and 'include_retention_hardening_draft' in text, 'media retention invariant hardening migration requires a separate explicit opt-in')
check('013_media_retention_deadline_stamping.sql' in text and 'include_retention_stamping_draft' in text, 'new-media retention deadline stamping migration requires a separate explicit opt-in')
check('args.include_retention_draft and args.include_retention_hardening_draft' in text, 'retention stamping opt-in requires both lifecycle and invariant-hardening opt-ins')
check('args.include_retention_stamping_draft' in text and '014_media_deletion_requests.sql' in text, 'media deletion request migration requires the full retention migration chain')
program=(API/'Program.cs').read_text(encoding='utf-8')
pipeline=(API/'Security/ApiSecurityPipelineExtensions.cs').read_text(encoding='utf-8')
check('UseScopxExceptionHandling' in program and 'UseExceptionHandler' in pipeline, 'API has generic exception handler')
check('AddScopxRateLimiting' in program and 'PermitLimit = 120' in pipeline and 'PermitLimit = 10' in pipeline, 'rate limiting configured in API scaffold')
check('UseScopxSecurityHeaders' in program and 'X-Correlation-ID' in pipeline and 'Content-Security-Policy' in pipeline, 'request correlation and security headers present')
check('"/health/ready"' in program and 'SELECT 1' in program, 'API readiness endpoint checks configured database connectivity')
health_start=program.index('app.MapHealthChecks("/health")')
health_end=program.index('if (!string.IsNullOrWhiteSpace(databaseConnection))', health_start)
health_routes=program[health_start:health_end]
check('RequireRateLimiting' not in health_routes, 'liveness and readiness probes are not throttled by API rate limits')
check('catch (OperationCanceledException) when (ct.IsCancellationRequested)' in program, 'database readiness probe preserves host/request cancellation')
endpoint_policy=(WORKSTATION/'ApiEndpointPolicy.cs').read_text(encoding='utf-8')
workstation_form=(WORKSTATION/'WorkstationForm.cs').read_text(encoding='utf-8')
check('Uri.UriSchemeHttps' in endpoint_policy and 'Uri.UriSchemeHttp' in endpoint_policy and 'uri.IsLoopback' in endpoint_policy, 'workstation requires HTTPS except for loopback development')
check('uri.UserInfo' in endpoint_policy and 'uri.Query' in endpoint_policy and 'uri.Fragment' in endpoint_policy, 'workstation rejects credential-bearing and decorated API base URLs')
check('ApiEndpointPolicy.IsAllowedBaseUri(baseUri)' in workstation_form, 'workstation applies the secure API endpoint policy before connecting')
auth_endpoints=(API/'Authentication/AuthEndpoints.cs').read_text(encoding='utf-8')
check('RequestSizeLimitAttribute(16 * 1024)' in auth_endpoints, 'login endpoint enforces a 16 KiB request-body limit')
security_tests=(ROOT/'src/SCOPX.SecurityTests/Program.cs').read_text(encoding='utf-8')
check('HTTP login rejects malformed JSON' in security_tests and 'HTTP login rejects unsupported content types' in security_tests and 'HTTP login rejects request bodies larger than 16 KiB' in security_tests, 'login request validation and body-size regression tests exist')
cors=(API/'Security/ApiCorsExtensions.cs').read_text(encoding='utf-8')
check('AddScopxCors' in program and 'UseCors("scopx")' in program and 'WithOrigins(origins)' in cors, 'API CORS uses an explicit configured origin allowlist')
check('AllowCredentials' not in cors and "origin.Contains('*')" in cors, 'CORS rejects wildcard origins and does not enable credentialed requests')
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
lockout=(security/'LoginLockoutPolicy.cs').read_text(encoding='utf-8')
check('MaxFailedAttemptsBeforeLockout = 5' in lockout and 'TimeSpan.FromMinutes(15)' in lockout and 'TimeSpan.FromHours(1)' in lockout and 'TimeSpan.FromHours(24)' in lockout, 'lockout helper defines the documented 5/10/15 escalation bands')
check("failed_login_count + 1 >= 15" in repo and "failed_login_count + 1 >= 10" in repo and "failed_login_count + 1 >= 5" in repo, 'database lockout thresholds match the policy escalation bands')
check('LockoutFor(4)' in security_tests and 'LockoutFor(15)' in security_tests and 'IsLocked' in security_tests, 'security tests cover lockout threshold and expiry boundaries')
check('PasswordPolicy.IsAcceptable' in security_tests and '129' in security_tests, 'security tests cover password policy boundaries')
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
    check('private static bool CanWrite(string[] roles) =>' in rt and 'private static bool CanReview(string[] roles) =>' in rt and rt.count('roles.Contains("doctor"') >= 2, 'report drafting and review are restricted to doctors')
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
    check('MaxBytes = 50 * 1024 * 1024' in ma and 'MediaFileSignatureValidator.Matches' in ma, 'media uploads are size-limited and validated by file signature')
    check('IMediaMalwareScanner scanner' in ma and 'media_scanning_unavailable' in ma, 'media uploads fail closed when malware scanning is unavailable')
    check('protector.Encrypt' in ma and 'protector.Decrypt' in ma and 'FixedTimeEquals' in ma, 'media files are authenticated-encrypted and integrity-checked on download')
    check('Path.GetFullPath' in ma and 'HospitalId:N' in ma, 'media storage keys are generated and full paths are checked')
protection=API/'Media/MediaContentProtection.cs'
check(protection.exists(), 'authenticated media protection module exists')
if protection.exists():
    mp=protection.read_text(encoding='utf-8')
    check('new AesGcm(key, TagSize)' in mp and 'RandomNumberGenerator.GetBytes(NonceSize)' in mp, 'media encryption uses AES-GCM with a fresh nonce')
    check('MediaStorage:ActiveKeyId' in mp and 'MediaStorage:Keys:' in mp and 'key.Length != 32' in mp, 'media encryption requires a configured 256-bit keyring and active key ID')
    check('AssociatedData' in mp and 'CryptographicOperations.ZeroMemory(key)' in mp, 'media encryption binds tenant and asset identity and clears key buffers')
    scanner_file=API/'Media/ClamAvMediaMalwareScanner.cs'
check(scanner_file.exists() and 'ClamAvMediaMalwareScanner' in program, 'ClamAV malware scanner adapter is registered')
if scanner_file.exists():
    scanner_source=scanner_file.read_text(encoding='utf-8')
    check('zINSTREAM' in scanner_source and ' FOUND' in scanner_source and ' OK' in scanner_source, 'ClamAV adapter uses INSTREAM and distinguishes clean from infected results')
    check('TimeoutSeconds' in scanner_source and 'MaximumBytes' in scanner_source, 'ClamAV adapter enforces upload limits and timeout')
check('UnavailableMediaMalwareScanner' in mp and 'No malware scanning engine is configured' in mp, 'fallback malware scanner implementation fails closed')
check('MapMediaEndpoints' not in program and 'IMediaRepository' not in program, 'media routes remain disabled pending scanner, retention, and operational verification')
check('MaxRequestBodySize = 60 * 1024 * 1024' in program and 'MultipartBodyLengthLimit = 60 * 1024 * 1024' in program, 'host request and multipart limits exceed the 50 MiB media-file cap with bounded overhead')
if media_api.exists():
    check('CryptographicOperations.ZeroMemory(plaintext)' in ma and 'CryptographicOperations.ZeroMemory(encrypted)' in ma, 'media upload clears plaintext and encrypted working buffers')
    check('FileMode.CreateNew' in ma and 'File.Delete(fullPath)' in ma, 'media upload avoids overwriting generated object keys and cleans up failed writes')
    check('FirstOrDefault()' in ma and 'file.Length > MaxBytes' in ma, 'media upload accepts one bounded file field and enforces the application file-size cap')
media_repository=infra/'Media/NpgsqlMediaRepository.cs'
if media_repository.exists():
    mrepo=media_repository.read_text(encoding='utf-8')
    check('NpgsqlAuditEventWriter.WriteAsync' in mrepo and 'media.uploaded' in mrepo and 'media.downloaded' in mrepo, 'media upload and verified download audit writes are transactional')
    check('media.read_attempt' in mrepo and "set_config('scopx.hospital_id'" in mrepo, 'media reads are tenant-scoped and access attempts are audited')
device_contracts=WORKSTATION/'Devices/DeviceCaptureContracts.cs'
check(device_contracts.exists(), 'hardware capture adapter contracts exist')
if device_contracts.exists():
    dcs=device_contracts.read_text(encoding='utf-8')
    check('IDeviceCaptureAdapter' in dcs and 'SimulatorCaptureAdapter' in dcs, 'device integration is isolated behind a capture adapter contract')
    check('no image/video is captured' in dcs and 'Simulation only' in dcs, 'simulator cannot be mistaken for a real video capture source')
    check('NetworkStream' in dcs and 'HdmiCaptureCard' in dcs, 'network and HDMI transports are declared as future adapter types only')
retention_migration=MIGRATIONS/'011_media_retention_lifecycle.sql'
check(retention_migration.exists(), 'Step 43 media retention lifecycle draft migration exists')
if retention_migration.exists():
    rm=retention_migration.read_text(encoding='utf-8')
    check('media_retention_policies' in rm and 'retention_days' in rm and 'enabled boolean NOT NULL DEFAULT false' in rm, 'retention requires an explicit per-hospital policy with no invented default period')
    check('media_legal_holds' in rm and 'released_at IS NULL' in rm and 'media deletion blocked by active legal hold' in rm, 'active legal holds block media deletion queue operations')
    check('media_deletion_queue' in rm and 'not_before' in rm and 'retention period has not elapsed' in rm, 'deletion queue enforces retention eligibility timestamps')
    check('media_deletion_events' in rm and 'append-only' in rm and 'BEFORE TRUNCATE' in rm, 'media deletion events are append-only and tenant-scoped')
    check('scopx_current_hospital_id' in rm and 'ENABLE ROW LEVEL SECURITY' in rm and 'FORCE ROW LEVEL SECURITY' in rm, 'media retention lifecycle tables enforce tenant RLS')
    check('worker' in rm.lower() and 'No worker' in rm, 'retention migration explicitly does not enable an automated deletion worker')
retention_hardening=MIGRATIONS/'012_media_retention_invariant_hardening.sql'
check(retention_hardening.exists(), 'Step 44 media retention invariant hardening migration exists')
if retention_hardening.exists():
    rh=retention_hardening.read_text(encoding='utf-8')
    check('media_deletion_queue_one_open_per_asset_idx' in rh and "status IN ('pending','processing','failed')" in rh, 'only one active media deletion request can exist per asset')
    check('scopx_guard_media_legal_hold_mutation' in rh and 'legal hold placement facts are immutable' in rh and 'released legal hold records are immutable' in rh, 'legal hold placement and release are one-way immutable facts')

request_migration=MIGRATIONS/'014_media_deletion_requests.sql'
check(request_migration.exists(), 'Step 48 media deletion request workflow migration exists')
if request_migration.exists():
    rq=request_migration.read_text(encoding='utf-8')
    check('media_deletion_requests' in rq and 'media_deletion_request_events' in rq, 'request inbox and separate decision history tables are defined')
    check("status IN ('submitted','under_review','approved','rejected','cancelled')" in rq, 'request lifecycle states are explicit and bounded')
    check('media_deletion_requests_one_open_per_asset_idx' in rq, 'only one open request per media asset is allowed')
    check('request facts are immutable' in rq and 'append-only' in rq and 'BEFORE TRUNCATE' in rq, 'request facts and decision history are protected against tampering')
    check('requester cannot approve or reject their own request' in rq and 'active membership' in rq, 'request workflow enforces four-eyes approval and active tenant membership')
    check('ENABLE ROW LEVEL SECURITY' in rq and 'FORCE ROW LEVEL SECURITY' in rq, 'request and event tables enforce tenant RLS')
    check('No approval-to-queue action' in rq and 'execution queue' in rq.lower(), 'request workflow is separate from physical deletion execution')

retention_stamping=MIGRATIONS/'013_media_retention_deadline_stamping.sql'
check(retention_stamping.exists(), 'Step 45 new-media retention deadline stamping migration exists')
if retention_stamping.exists():
    rsm=retention_stamping.read_text(encoding='utf-8')
    check('BEFORE INSERT ON public.media_assets' in rsm and 'NEW.created_at + make_interval(days => policy_days)' in rsm, 'new media retention deadline derives from creation time and explicit policy days')
    check('NEW.created_at := transaction_timestamp()' in rsm, 'server controls media creation time so clients cannot backdate retention')
    check('IF NOT FOUND OR policy_enabled IS DISTINCT FROM true' in rsm and 'NEW.retention_until := NULL' in rsm, 'missing or disabled retention policies leave new media without a deletion deadline')
    check('BEFORE UPDATE OF retention_until, created_at, hospital_id' in rsm and 'media retention stamp and creation tenant are immutable' in rsm, 'retention stamps and source creation facts cannot be changed later')
    check('No deletion worker' in rsm or 'No worker' in rsm, 'retention stamping does not activate a deletion worker')

media_endpoints=(API/'Media/MediaEndpoints.cs').read_text(encoding='utf-8')
media_repo=(infra/'Media/NpgsqlMediaRepository.cs').read_text(encoding='utf-8')
check('DownloadAllowed(actor.Roles)' in media_endpoints and 'private static bool DownloadAllowed' in media_endpoints, 'media downloads use a distinct care-team authorization policy')
check('"auditor"' not in media_endpoints[media_endpoints.index('private static bool DownloadAllowed'):media_endpoints.index('private static bool WriteAllowed')], 'auditors cannot download clinical media')
check('RecordDownloadAsync' in media_endpoints and '"media.downloaded"' in media_repo and 'integrity_verified=true' in media_repo, 'verified media downloads create transactional audit events')
check('CryptographicOperations.ZeroMemory(encrypted)' in media_endpoints, 'encrypted media buffers are cleared after decryption')


# Step 42 immutable report versions and signing evidence foundation
report_versions_migration=MIGRATIONS/'010_report_versioning_signatures.sql'
check(report_versions_migration.exists(), 'Step 42 report versioning and signature evidence migration exists')
if report_versions_migration.exists():
    rvm=report_versions_migration.read_text(encoding='utf-8')
    check('procedure_report_versions' in rvm and 'procedure_report_signatures' in rvm, 'report snapshots and signature artifact tables are defined')
    check('scopx_reject_report_artifact_mutation' in rvm and 'BEFORE TRUNCATE' in rvm, 'report artifacts are protected against UPDATE, DELETE and TRUNCATE')
    check('scopx_apply_report_signature' in rvm and 'reauthenticated_at' in rvm and 'RSA-PSS-SHA256' in rvm, 'signature evidence requires re-authentication metadata and a pinned algorithm')
    check('SCOPX-REPORT-TEXT-V1' in rvm and 'LEGACY-RAW-UTF8-V1' in rvm, 'report digest canonicalization versions are explicit')
report_repository=(infra/'Reports/NpgsqlReportRepository.cs').read_text(encoding='utf-8')
check('report_body_sha256' in report_repository and 'report_version=report_version+1' in report_repository and 'HashBody' in report_repository, 'report draft edits hash and increment each immutable version')
report_endpoints=(API/'Reports/ReportEndpoints.cs').read_text(encoding='utf-8')
check('ExpectedVersion' in report_endpoints and 'expectedVersion' in report_repository and 'report_version=@expected' in report_repository, 'report draft updates require an expected version to prevent lost updates')
signing_service=API/'Reports/ReportSigningService.cs'
check(signing_service.exists(), 'RSA-PSS report signing service exists')
if signing_service.exists():
    ssvc=signing_service.read_text(encoding='utf-8')
    check('RSA-PSS-SHA256' in ssvc and 'RSASignaturePadding.Pss' in ssvc, 'report signing uses RSA-PSS with SHA-256')
    check('rsa.SignData' in ssvc and 'rsa.VerifyData' in ssvc, 'report signing artifacts can be created and cryptographically verified')
    check('Reports:SigningPrivateKeyPem' in ssvc and 'Reports:SigningKeyId' in ssvc, 'report signing requires an externally configured private key and key identifier')
    check('TimeSpan.FromMinutes(5)' in ssvc and 'CanonicalizationVersion' in ssvc, 'report signature binds canonical version and recent re-authentication metadata')
check('AddSingleton<ReportSigningService>' in program, 'report signing service is registered but requires external key configuration')
check('ReportSignRequest' in (API/'Reports/ReportEndpoints.cs').read_text(encoding='utf-8') and 'passwordHashes.Verify' in (API/'Reports/ReportEndpoints.cs').read_text(encoding='utf-8'), 'disabled report signing route requires current-password re-authentication')
check('reports.SignAsync' in (API/'Reports/ReportEndpoints.cs').read_text(encoding='utf-8') and '"report.signed"' in report_repository, 'report signature persistence and audit event share one repository transaction')
check('MapReportEndpoints' not in program, 'report signing route remains unexposed by the API host')
security_tests=ROOT/'src/SCOPX.SecurityTests/Program.cs'
check(security_tests.exists() and (ROOT/'src/SCOPX.SecurityTests/SCOPX.SecurityTests.csproj').exists(), 'report-signing cryptography test project exists')
if security_tests.exists():
    st=security_tests.read_text(encoding='utf-8')
    check('tampered signature bytes are rejected' in st and 'changed document digest invalidates the signature' in st, 'report-signing tests cover signature and payload tampering')
    check('ClamAV INSTREAM clean response is accepted' in st and 'ClamAV INSTREAM infected response is rejected' in st, 'media security tests cover ClamAV clean and infected responses')
    check('rotated keyring retains decryption access' in st and 'ciphertext cannot be moved across hospital tenants' in st, 'media security tests cover key rotation and tenant-bound encryption')
    check('no private key is configured' in st and 'smaller than 2048 bits' in st, 'report-signing tests cover fail-closed key configuration and minimum strength')

report_smoke=(ROOT/'scripts/tests/tenant-rls-ci-smoke.sql').read_text(encoding='utf-8')
check('report-version RLS failed' in report_smoke and 'signed report mutation was not rejected' in report_smoke, 'PostgreSQL smoke test covers report tenant isolation and signed immutability')

# Step 29 Windows workstation shell checks
workstation=ROOT/'src/SCOPX.Workstation'
check((workstation/'SCOPX.Workstation.csproj').exists(), 'Windows workstation project exists')
wf=workstation/'WorkstationForm.cs'
check(wf.exists(), 'workstation shell form source exists')
if wf.exists():
    wtext=wf.read_text(encoding='utf-8')
    check('CheckApi' in wtext and '/health/ready' in wtext and 'connectivity only' in wtext, 'workstation shell checks database connectivity without claiming clinical readiness')
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
    check('CaptureStartOptionsPolicy.IsValid(options)' in dc, 'simulator delegates to the centralized capture options policy')
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
compose=(ROOT/'docker-compose.yml').read_text(encoding='utf-8')
check('POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:?' in compose and 'local_only_change_me' not in compose, 'Docker Compose refuses to start with an implicit weak database password')
env_example=(ROOT/'.env.example').read_text(encoding='utf-8')
check('ConnectionStrings__ScopxDb=' in env_example, 'environment example includes a local API connection string template')
bootstrap_project=ROOT/'src/SCOPX.Bootstrap/SCOPX.Bootstrap.csproj'
bootstrap_program=ROOT/'src/SCOPX.Bootstrap/Program.cs'
bootstrap_launcher=ROOT/'scripts/windows/Bootstrap-DevAdmin.ps1'
check(bootstrap_project.exists() and bootstrap_program.exists() and bootstrap_launcher.exists(), 'development first-admin bootstrap tool and launcher exist')
if bootstrap_program.exists():
    bootstrap_text=bootstrap_program.read_text(encoding='utf-8')
    check('PasswordHasher<AuthUser>' in bootstrap_text and 'ReadHidden' in bootstrap_text, 'first-admin bootstrap hashes a hidden password with the same ASP.NET hasher')
    check('database != "scopx_dev"' in bootstrap_text and "code = 'hospital_admin'" in bootstrap_text, 'first-admin bootstrap is dev-database-only and grants only hospital_admin')
if bootstrap_launcher.exists():
    bootstrap_launch_text=bootstrap_launcher.read_text(encoding='utf-8')
    check('scopx_dev' in bootstrap_launch_text and '--dev-only' in bootstrap_launch_text, 'PowerShell bootstrap launcher is restricted to the dev database')
check('Username=scopx_runtime' in env_example, 'API connection string template uses the least-privilege runtime role')
if api_launcher.exists():
    check('scopx_runtime' in launch_text and 'must connect as' in launch_text, 'API launcher refuses database-owner credentials')
    check('runtimePassword -eq $ownerPassword' in launch_text, 'API launcher rejects reuse of database-owner password')
runtime_setup=ROOT/'scripts/windows/Prepare-DevRuntimeRole.ps1'
check(runtime_setup.exists(), 'interactive least-privilege runtime role setup script exists')
if runtime_setup.exists():
    runtime_text=runtime_setup.read_text(encoding='utf-8')
    check('\\password scopx_runtime' in runtime_text and 'NOBYPASSRLS' in runtime_text, 'runtime role setup uses interactive password entry and safe role attributes')
security_setup=ROOT/'scripts/windows/Apply-DevDatabaseSecurity.ps1'
check(security_setup.exists(), 'guarded local migration and grant script exists')
if security_setup.exists():
    security_text=security_setup.read_text(encoding='utf-8')
    check("APPLY SCOPX DEV SECURITY" in security_text and 'Validate-RuntimePrivileges.sql' in security_text, 'local database security setup requires confirmation and runs privilege validation')
preflight=(ROOT/'scripts/windows/Preflight.ps1').read_text(encoding='utf-8')
check("Check-Command 'python'" in preflight and "Check-Command 'psql'" in preflight, 'Windows preflight checks migration runner dependencies')

# Step 70 non-clinical recording lifecycle policy checks
recording_policy=WORKSTATION/'Recording/RecordingLifecyclePolicy.cs'
check(recording_policy.exists(), 'non-clinical recording lifecycle policy exists')
if recording_policy.exists():
    rp=recording_policy.read_text(encoding='utf-8')
    for state in ['Idle', 'Preparing', 'Recording', 'Stopping', 'Finalized', 'Error']:
        check(f' {state},' in rp or f' {state}' in rp, f'recording lifecycle declares {state} state')
    check('public static bool IsAllowed' in rp and 'public static RecordingLifecycleState Transition' in rp, 'recording lifecycle exposes pure transition validation')
    check('no' not in rp.lower() or 'does not open devices' in rp, 'recording lifecycle policy is not a hardware driver')
check('RecordingLifecyclePolicy.cs' in (ROOT/'src/SCOPX.SecurityTests/SCOPX.SecurityTests.csproj').read_text(encoding='utf-8'), 'recording lifecycle policy is linked into portable regression tests')
capture_contract=WORKSTATION/'Devices/DeviceCaptureContracts.cs'
check(capture_contract.exists(), 'capture adapter contract and no-video simulator exist')
if capture_contract.exists():
    cc=capture_contract.read_text(encoding='utf-8')
    check('IDeviceCaptureAdapter' in cc and 'SimulatorCaptureAdapter' in cc, 'capture provider interface and deterministic simulator are declared')
    check('cancellationToken.ThrowIfCancellationRequested()' in cc, 'simulator status/start/stop operations honor cancellation')
    check('_state == CaptureState.Disconnected' in cc and 'ObjectDisposedException' in cc, 'disposed simulator cannot be restarted or stopped')
check('DeviceCaptureContracts.cs' in (ROOT/'src/SCOPX.SecurityTests/SCOPX.SecurityTests.csproj').read_text(encoding='utf-8'), 'capture contracts are linked into portable regression tests')
descriptor_policy=WORKSTATION/'Devices/CaptureDeviceDescriptorPolicy.cs'
check(descriptor_policy.exists(), 'capture device descriptor validation policy exists')
if descriptor_policy.exists():
    dp=descriptor_policy.read_text(encoding='utf-8')
    check('Enum.IsDefined' in dp and 'char.IsControl' in dp, 'capture descriptor policy rejects unknown enums and control characters')
    check('simulator://' in dp and 'No Video' in dp, 'capture simulator metadata must be explicitly distinguished from hardware')
check('CaptureDeviceDescriptorPolicy.cs' in (ROOT/'src/SCOPX.SecurityTests/SCOPX.SecurityTests.csproj').read_text(encoding='utf-8'), 'descriptor validation policy is linked into portable regression tests')
options_policy=WORKSTATION/'Devices/CaptureStartOptionsPolicy.cs'
check(options_policy.exists(), 'capture start-options policy exists')
if options_policy.exists():
    op=options_policy.read_text(encoding='utf-8')
    check('7680' in op and '4320' in op and '120' in op, 'capture start-options policy bounds dimensions and frame rate')
    check('"MJPEG"' in op and '"YUY2"' in op and '"NV12"' in op, 'capture start-options policy uses an explicit pixel-format allowlist')
check('CaptureStartOptionsPolicy.cs' in (ROOT/'src/SCOPX.SecurityTests/SCOPX.SecurityTests.csproj').read_text(encoding='utf-8'), 'capture start-options policy is linked into portable regression tests')




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
