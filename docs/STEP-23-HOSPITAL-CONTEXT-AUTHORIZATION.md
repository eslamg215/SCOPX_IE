# Step 23 — Hospital Context and Membership Authorization

## Implemented
- Added read-only `IHospitalMembershipRepository` and Npgsql implementation.
- Lists only active memberships in active hospitals.
- Returns only hospital-scoped role codes; platform roles are excluded.
- `GET /api/v1/hospitals/mine` lists memberships for a user authenticated by active opaque bearer session.
- `GET /api/v1/hospitals/context` requires `X-Hospital-ID` and validates active membership server-side before returning selected context.
- Missing/invalid session returns 401. A valid user without the requested active membership receives 403.
- Parameterized SQL; no clinical writes or role grants are added.

## Remaining risks / boundaries
- These endpoints only select hospital context; clinical APIs must independently enforce tenant filters and roles for every operation.
- Hospital header is untrusted until the DB membership lookup succeeds.
- Runtime PostgreSQL/.NET compilation and integration tests were unavailable here; validate in disposable local environment.
- Development scaffold only; no real patient data and no clinical validation.
