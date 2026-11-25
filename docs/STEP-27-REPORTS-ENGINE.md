# Step 27 — Reports Engine (Draft + Review)

## Scope
Adds a tenant-scoped report drafting workflow for procedures. It reuses the existing `procedure_reports` table from migration 002 and does not introduce new clinical schema.

## API routes
- `GET /api/v1/reports/procedure/{procedureId}` — list report versions for a procedure.
- `GET /api/v1/reports/{id}` — report detail.
- `POST /api/v1/reports` — create a draft (procedureId, body).
- `PUT /api/v1/reports/{id}` — edit draft body only.
- `POST /api/v1/reports/{id}/submit-review` — transition non-empty draft to `review`.

All endpoints require a bearer session, `X-Hospital-ID`, and active hospital membership. The repository applies transaction-local `scopx.hospital_id` and parameterized SQL. Reads are role-gated; draft writes and review submission are limited to hospital admins and doctors in this scaffold.

## Deliberate limitations
- Signing is not implemented. No endpoint claims a report is clinically signed or legally finalized.
- Amendment/version creation is not implemented; the schema supports versions, but safe amendment flow needs audit integration and explicit review rules.
- `created_by_membership_id` is currently left nullable because the membership lookup contract does not expose membership IDs. Author attribution must be fixed before clinical use.
- Audit event integration and production RLS verification remain incomplete.
- Runtime compilation/API/DB tests were not performed: the current environment does not have the .NET SDK or PostgreSQL.
- Use synthetic data only. This is not clinically validated software.
