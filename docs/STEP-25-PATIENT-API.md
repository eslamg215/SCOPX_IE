# Step 25 — Patient Management API (development scaffold)

Implemented routes (all require an active bearer session and active hospital membership via `X-Hospital-ID`):
- `GET /api/v1/patients?q=&limit=25&offset=0` — tenant-scoped list; list DTO omits clinical notes.
- `GET /api/v1/patients/{id}` — tenant-scoped details.
- `POST /api/v1/patients` — create; patient code is unique per hospital.
- `PUT /api/v1/patients/{id}` — update basic details (hospital_admin/doctor only).
- `POST /api/v1/patients/{id}/archive` — archive (hospital_admin only; no hard delete).

Repository methods open a transaction and set `scopx.hospital_id` transaction-locally for RLS. SQL uses parameters. List paging is capped at 100. List excludes archived records. API responses use generic validation/conflict errors.

## Roles (initial conservative policy)
- Read: hospital_admin, doctor, nurse, reception, auditor, endoscopy_technician
- Create: hospital_admin, doctor, nurse, reception
- Update: hospital_admin, doctor
- Archive: hospital_admin

## Known gaps / gate before real patient data
- Runtime role grants and RLS migrations are draft; do not deploy or connect PHI.
- Hospital membership discovery currently needs a separate reviewed RLS/bootstrap path; RLS draft may block it unless trusted `scopx.user_id` is set. This implementation must be reconciled before deployment.
- Patient writes are not yet connected to append-only audit logging.
- No actual build or PostgreSQL test was possible in this environment. Offline static checks only.
- Patient-code uniqueness conflicts are mapped to 409; remaining database exceptions are intentionally not caught here.
- This is not clinical software and is not clinically validated.
