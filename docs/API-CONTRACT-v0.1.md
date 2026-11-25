# SCOPX API contract v0.1 (starter)

**This contract is informational and the scaffold is not clinically usable.** Patient endpoints remain intentionally unavailable until authentication, tenant authorization, audit, and tests are in place.


## Implemented scaffold endpoints

| Method | Route | Purpose | Auth |
|---|---|---|---|
| GET | `/` | API name and non-clinical-use notice | None |
| GET | `/health` | Framework health check | None |
| GET | `/api/v1/system` | Non-sensitive build/system metadata | None |

## Planned protected endpoints (not implemented)

- `POST /api/v1/auth/session` — authentication/session exchange via chosen identity provider.
- `GET /api/v1/hospitals/current` — active tenant context.
- `GET/POST /api/v1/patients` — tenant-scoped patient listing/creation.
- `GET /api/v1/patients/{patientId}` — tenant-scoped patient details.
- `GET/POST /api/v1/procedures` — tenant-scoped procedure scheduling/creation.
- `GET /api/v1/procedures/{procedureId}/media` — authorized media metadata listing.
- `GET/POST /api/v1/procedures/{procedureId}/reports` — report drafts/version workflow.
- `GET /api/v1/licenses/status` — server-validated license state.

## Before implementing protected routes

1. Choose an identity provider and OAuth/OIDC configuration.
2. Implement explicit tenant selection/validation on every request.
3. Use role + permission checks, not frontend-only gating.
4. Add audit events for sensitive reads/writes and administrative actions.
5. Add RLS and/or carefully scoped repository queries with integration tests.
6. Add request validation, rate limiting, and consistent error responses.
7. Add automated tests for cross-tenant access denial.
