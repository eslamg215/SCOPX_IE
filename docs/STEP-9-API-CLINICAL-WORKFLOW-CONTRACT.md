# Step 9 — API contract for core endoscopy workflow

Status: **Contract draft only; endpoints are not implemented.**

## Common requirements
- All routes below require authenticated identity and verified active hospital membership except explicitly public operational endpoints.
- Tenant ID in URLs/payloads is never authorization. The server resolves scope from the validated membership and transaction-local tenant context.
- Use UUID identifiers, UTC timestamps (ISO 8601), paging, request validation, rate limits, correlation IDs, and structured audit events.
- Responses must not expose database internals, storage paths, stack traces, or secrets.
- PHI endpoints require `Cache-Control: no-store`; logs must avoid patient names, DOB, phone, findings, and raw report text.

## Proposed routes and policy
| Method | Route | Scope | Permission |
|---|---|---|---|
| GET | `/api/v1/me` | user | authenticated |
| GET | `/api/v1/hospitals/mine` | memberships | authenticated; own memberships only |
| GET | `/api/v1/hospitals/{hospitalId}/patients` | tenant | patient-list permission |
| POST | `/api/v1/hospitals/{hospitalId}/patients` | tenant | demographics-create permission |
| GET | `/api/v1/patients/{patientId}` | tenant | patient-view permission |
| PATCH | `/api/v1/patients/{patientId}` | tenant | demographics-edit permission + field-level policy |
| POST | `/api/v1/patients/{patientId}/procedures` | tenant | procedure-create permission |
| GET | `/api/v1/procedures/{procedureId}` | tenant | procedure-view permission |
| PATCH | `/api/v1/procedures/{procedureId}` | tenant | workflow-edit permission; transition validation |
| POST | `/api/v1/procedures/{procedureId}/media` | tenant | media-capture permission; upload session required |
| GET | `/api/v1/procedures/{procedureId}/media` | tenant | media-view permission; no direct storage paths |
| POST | `/api/v1/procedures/{procedureId}/reports` | tenant | report-draft permission |
| PATCH | `/api/v1/reports/{reportId}` | tenant | report-edit permission; draft only |
| POST | `/api/v1/reports/{reportId}/sign` | tenant | report-sign permission; step-up/reauth policy |
| POST | `/api/v1/reports/{reportId}/amendments` | tenant | report-amend permission; preserve prior signed version |

## Workflow state machine
Procedure: `scheduled -> in_progress -> completed`; `scheduled -> cancelled`. No reopening completed/cancelled records through a generic PATCH. A dedicated audited workflow action is required for any correction.

Report: `draft -> review -> signed`; `signed -> amended` only through an amendment record/new version. Signed content is immutable. An AI suggestion must be clearly marked, reviewable, and never signs or finalizes a report autonomously.

## Validation and concurrency
- PATCH requests should use optimistic concurrency (ETag/If-Match or version field); conflict returns 409.
- Duplicate capture/report creation should support idempotency keys.
- Date/time inputs validated for UTC and clinically meaningful sequencing.
- Reject file uploads that exceed configured limits or fail allowlist/virus scanning; verify content type from bytes, not client header alone.

## Safe illustrative patient DTO (synthetic only)
```json
{
  "patientCode": "DEMO-0001",
  "fullName": "Synthetic Test Patient",
  "dateOfBirth": "1980-01-01",
  "sexAtBirth": "not_recorded"
}
```
Never use real identifiers in examples, screenshots, seed data, or testing.

## Minimum integration tests
- Every endpoint checks authentication and permission independently.
- Hospital A user cannot enumerate, read, modify, or delete hospital B resources.
- Invalid workflow transitions are rejected.
- Signed report cannot be edited in place; amendments preserve previous signed document and author/time.
- Media download access is authorized and short-lived; storage is private.
- Pagination is stable and bounded; validation failures are safe and non-leaking.
- Duplicate request idempotency, concurrency conflicts, audit coverage, and upload safety tests pass.
