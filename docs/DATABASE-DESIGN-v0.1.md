# SCOPX.I.E — Database Design v0.1 (Draft)

## Database conventions
- PostgreSQL; UUID primary keys.
- UTC timestamps (`timestamptz`).
- Hospital-owned entities carry `hospital_id`.
- Avoid storing unnecessary identifiers; patient identifiers should be hospital-scoped.
- Clinical content and media metadata are not placed in application logs.
- This is a draft schema plan; migrations and automated tenant tests are still required.

## Core tables

### Platform and tenancy
- `users`: user identity, login email (normalized/unique), display name, account status, timestamps.
- `hospitals`: hospital identity, display name, status, created timestamp.
- `hospital_memberships`: user, hospital, membership status; unique user/hospital pair.
- `roles`: role definitions (system seed roles initially).
- `membership_roles`: membership-to-role assignment; scoped to hospital where applicable.

### Patient and workflow
- `patients`: hospital ID, local patient number, name, date of birth (optional based on need), sex field only if clinically/workflow required, status; unique hospital/local number.
- `appointments`: hospital ID, patient ID, assigned clinician (optional), scheduled start, status, purpose; tenant-consistent patient FK.
- `procedure_sessions`: hospital ID, patient ID, appointment ID (optional), clinician/technician membership references, room/device metadata, start/end, status.

### Media and reports
- `procedure_media`: hospital ID, procedure session ID, media type, storage reference (not a public URL), capture timestamp, device/source metadata, hash/checksum, retention status. Binary media stored separately in protected storage.
- `clinical_reports`: hospital ID, procedure session ID, author membership, report status (draft/review/final), report body, created/updated/finalized timestamps. Draft AI text is tracked separately from clinician-approved final text.
- `ai_assistance_records`: hospital ID, procedure session ID, requesting user, provider/model metadata, purpose, redacted/approved input reference, output, review status, timestamps. Detailed content retention is policy-dependent and must be reviewed.


### Security and audit
- `audit_events`: hospital ID nullable for platform events, actor user ID, event type, target type/id, timestamp, outcome, reason (when required), and metadata constrained to non-sensitive values. Append-oriented permissions.
- `license_records`: hospital ID, license status, expiry, activation metadata; secrets/activation keys are stored hashed or in a dedicated secure system as appropriate.
- `device_registry`: hospital ID, device type, manufacturer/model, serial (only if necessary), room assignment, status, configuration reference.


## Relationship rules
- Membership must be active before any hospital-scoped action.
- All patient/appointment/procedure/media/report lookups are hospital-scoped.
- A procedure session's patient, appointment, and membership references must belong to the same hospital.
- Media references must not be externally accessible without an authorization check.
- Reports must not be finalized by roles without explicit finalization permission.

## Indexes and constraints (planned)
- Unique normalized user email.
- Unique hospital/local patient number.
- Unique active membership per user/hospital.
- Index on `(hospital_id, created_at)` for audit and workflow queries.
- Index on `(hospital_id, scheduled_start)` for appointments.
- Index on `(hospital_id, patient_id)` for patient workflow tables.
- FK constraints and tenant-consistency enforcement; consider composite tenant-key foreign keys.

## Decisions still open
- Patient identifier strategy and required demographics.
- Appointment and procedure statuses and transitions.
- Clinician assignment and delegation.
- Report versioning, signature, correction/addendum model.
- Media storage location, encryption, retention, export and deletion policy.
- Whether PostgreSQL Row-Level Security is enabled in addition to application tenant filters.
- Audit-event immutability/integrity implementation.
- AI input/output minimization and retention.
