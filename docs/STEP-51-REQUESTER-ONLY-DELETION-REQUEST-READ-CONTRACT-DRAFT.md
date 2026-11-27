# Step 51 — Requester-only read visibility contract (draft)

## Status

Design only. No routes are mapped, no API permission is enabled, and no new database grants are introduced by this document. Clinical endpoints remain fail-closed in the current API host.

## Narrow read-only scope that can be reviewed independently

The only candidate read audience before a hospital governance matrix is approved is the authenticated requester reading their own deletion request. This proposal does not grant hospital-wide inbox access to administrators, auditors, clinicians, or other members.

Candidate endpoints for a later, separately reviewed implementation:

- `GET /api/v1/media/deletion-requests/{requestId}` — requester-only status view.
- `GET /api/v1/media/deletion-requests/mine` — requester-only paginated list, limited to the current authenticated user and hospital.

No endpoint may list all tenant requests or expose reviewer inboxes until Step 49 decisions are approved.

## Authorization invariants

1. Resolve the bearer session, active user, and active membership using the existing API authentication flow.
2. Derive actor ID from the validated session and hospital ID from the active `X-Hospital-ID` membership context; never trust request-body actor or tenant identifiers.
3. Query by all three values: request ID, hospital ID, and `requested_by_user_id`. For the list, filter by hospital ID and requester ID.
4. Keep database RLS enabled and forced; the API must not elevate to a schema-owner or bypass-RLS connection.
5. Return a generic not-found result for both nonexistent and non-owned request IDs to avoid cross-request enumeration.
6. Return only request status, request timestamp, decision timestamp, and the original reason supplied by the requester. Do not return patient names, report content, storage keys, media bytes, reviewer identity, or internal event reasons.
7. Apply pagination with bounded page size and stable ordering; never allow arbitrary SQL filters or client-supplied tenant scope.
8. Record access telemetry without logging request reasons, patient identifiers, media paths, tokens, or clinical content.

## Explicitly excluded

- Tenant-wide request list and reviewer inbox.
- Claim, approve, reject, or cancel routes.
- Queue insertion, worker dispatch, retention override, legal-hold release, or physical deletion.
- Auditor access or administrator access to other users' requests.
- Any UI that implies approval authorizes deletion.

## Tests required before implementation

- Unauthenticated, expired, revoked, and disabled-user sessions return 401.
- Missing/inactive membership returns 401.
- Requester can read their own request in the selected hospital.
- Another active member in the same hospital cannot read the request by ID.
- A requester cannot read their own request through another hospital context.
- Cross-tenant and unknown IDs return the same generic not-found response.
- List results contain only the current user's requests, are bounded, and are stable under pagination.
- Response DTO excludes reviewer identity, patient data, report body, storage key, and event reason.
- Database queries remain parameterized and tenant-scoped.
- No queue or storage API is called by either read endpoint.

## Gate

Do not map these endpoints while the host-level clinical-route fail-closed policy remains in force. Implementation requires a dedicated API security review and an explicit owner decision that requester-only visibility is acceptable under each hospital's governance and privacy policy.
