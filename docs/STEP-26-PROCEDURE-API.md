# Step 26 — Procedure workflow API (development scaffold)

Routes added:
- `GET /api/v1/procedures/patient/{patientId}` — tenant-scoped procedures for a patient.
- `GET /api/v1/procedures/{id}` — tenant-scoped procedure details.
- `POST /api/v1/procedures` — create a scheduled procedure.
- `POST /api/v1/procedures/{id}/start` — scheduled -> in_progress.
- `POST /api/v1/procedures/{id}/complete` — in_progress -> completed, stores findings/impression.

The PostgreSQL repository uses parameters and transaction-local `scopx.hospital_id`. Start/complete updates are conditional on the prior status; completion also requires end time not earlier than start time. Patient references are checked by the database's hospital-consistent foreign key. List is capped at 250.

Initial roles: read = hospital_admin, doctor, nurse, auditor, endoscopy_technician; create = hospital_admin, doctor, endoscopy_technician; start = hospital_admin, doctor, endoscopy_technician; complete = hospital_admin, doctor.

## Remaining gates
- Build/runtime tests are not available here; this stage only has offline structural checks.
- Audit logging is not yet integrated with procedure/patient mutations.
- RLS draft and trusted tenant membership-context issue documented in Step 25 remains unresolved and is a blocker before use with PHI.
- No clinical validation; no real patient data.
