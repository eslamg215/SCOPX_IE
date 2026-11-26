# Step 52 — Deletion-request runtime read privilege hardening

## Status

Implemented on `fix/database-runtime-security`; PostgreSQL/RLS CI passed on the privilege-validation commit and the additional sensitive-column denial tests passed on the subsequent test commit. The full API/Windows build for the latest test commit is still being verified.

This step changes database grants only. It does not map API routes, enable request submission/review permissions, add a worker, enqueue deletion, or delete media.

## Runtime request-table access

The `scopx_runtime` role no longer receives table-wide SELECT on `media_deletion_requests`. It receives column-scoped SELECT only on:

- `id`, `hospital_id`, `media_id`
- `status`, `reason`
- `requested_by_user_id`, `requested_at`, `decided_at`

It is explicitly denied SELECT on `reviewer_user_id` and `decision_reason`.

Existing INSERT and narrow UPDATE grants remain separate and are not an approval policy. UPDATE is limited to `status` and `decision_reason`; database triggers enforce actor identity, active membership, legal transitions, reviewer separation, and mandatory reasons. No DELETE or TRUNCATE is granted.

## Runtime event-table access

The runtime role no longer receives table-wide SELECT on `media_deletion_request_events`. Column-scoped SELECT is limited to:

- `hospital_id`, `request_id`
- `from_status`, `to_status`, `occurred_at`

It is explicitly denied SELECT on `actor_user_id` and `reason`, and denied INSERT, UPDATE, DELETE, and TRUNCATE. Event creation remains trigger-only.

## Verification added

- Privilege preflight checks the exact allowed SELECT columns and rejects table-wide SELECT.
- Privilege preflight rejects SELECT on reviewer identity, decision reason, event actor identity, and event reason.
- Tenant RLS smoke test attempts to read each restricted column as `scopx_runtime` and expects `insufficient_privilege`.
- Existing tests continue to verify request status transitions, requester/reviewer separation, append-only events, tenant isolation, and no enqueue on approval.

## Security boundary

These grants are defense in depth, not a substitute for a requester-scoped SQL predicate. Any future requester status repository must filter by `hospital_id`, `requested_by_user_id`, and request ID where applicable, set transaction-local tenant/user context, and return only the approved response DTO.

No API read endpoint is mapped by this step. The requester-only read contract in Step 51 remains a draft pending owner/governance review and dedicated API authorization tests.
