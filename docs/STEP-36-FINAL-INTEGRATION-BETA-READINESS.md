# Step 36 — Final Integration & Beta Readiness Gate

**Outcome: development milestone package created; NOT cleared for clinical beta.**

## Verified in this environment
- Offline structural validator passes (109 checks).
- Archive integrity test passes for the milestone ZIP.
- Required source folders and milestone documents are present.

## Not verified / release blockers
- .NET compilation or API execution (SDK/runtime unavailable here).
- PostgreSQL migrations, RLS policies, grants, tenant-crossing tests, and real DB behavior.
- Android Gradle build, APK install, and device tests.
- Windows desktop compilation, installer, signing, and upgrade/uninstall tests.
- Endoscope video capture, audio, capture card drivers, or Olympus CV-180 compatibility.
- AI provider integration, image inference, accuracy/robustness evaluation, or clinical validation.
- End-to-end authentication, patient/procedure/report/media audit linkage, privacy/security penetration testing.
- Backup restoration drill, monitoring, deployment rollback, and operational support plan.

## Safe test checklist
1. Use synthetic data only, with isolated disposable database.
2. Install dependencies and run the API build; resolve compiler warnings/errors.
3. Apply migrations to a disposable PostgreSQL instance, beginning with reviewed migration selection.
4. Run tenant-isolation tests with two hospitals, including negative cross-tenant cases.
5. Exercise API workflow from login to patient, procedure, draft report/review, media upload/download.
6. Build/install Android and Windows clients; validate API connectivity and logout/session behavior.
7. Test device capture with a physical capture card and the target processor; do not assume compatibility.
8. Validate backups by restoring to a fresh disposable DB and running integrity/smoke tests.
9. Conduct security/privacy review and usability/clinical-safety review; record findings and sign-off.

## Release disposition
Version 2.28 is an integration checkpoint archive for development review. It must not be advertised as a functioning clinical beta, production release, medical device, or validated diagnostic software. Keep the system offline from patient data until blockers are closed and responsible reviewers approve.
