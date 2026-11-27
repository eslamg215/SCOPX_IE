# Step 53 — Requester-scoped read repository and response contract

## Status

Design and review artifact only. No repository, DTO, route, or endpoint is enabled by this step. Step 49 governance decisions remain pending.

## Required actor context

The API must first validate the bearer session, active user, and active membership using the existing authentication flow. The repository receives immutable server-derived values:

- `actorUserId`: user ID from the validated active session.
- `hospitalId`: hospital ID from the validated active membership selected by `X-Hospital-ID`.
- `requestId`: parsed route identifier only; it is never authority by itself.

No request body or query-string value may override actor or hospital identity.

## Detail query shape

The future detail repository query must include all three predicates in the same parameterized SQL statement:

```sql
SELECT id, status, reason, requested_at, decided_at
FROM public.media_deletion_requests
WHERE hospital_id = @hospital_id
  AND requested_by_user_id = @actor_user_id
  AND id = @request_id
LIMIT 1;
```

The transaction must set `scopx.user_id` and `scopx.hospital_id` using transaction-local `set_config(..., true)` before the query. The connection must use the least-privilege runtime role and must not own tables or bypass RLS.

A missing row, a row in another hospital, and a same-hospital row belonging to another requester all map to the same generic 404 response.

## List query shape

The future list query must always filter by `hospital_id` and `requested_by_user_id`. It must use a fixed page size (default 25, maximum 100), stable ordering by `requested_at DESC, id DESC`, and keyset pagination.

The opaque continuation cursor should bind the last returned `requested_at` and `id` to the authenticated actor and hospital. Reject malformed, oversized, expired (if expiry is used), or mismatched cursors. Do not accept arbitrary sort fields, filters, or caller-supplied tenant/user IDs.

Illustrative query:

```sql
SELECT id, status, reason, requested_at, decided_at
FROM public.media_deletion_requests
WHERE hospital_id = @hospital_id
  AND requested_by_user_id = @actor_user_id
  AND (
       @has_cursor = false
       OR (requested_at, id) < (@cursor_requested_at, @cursor_id)
  )
ORDER BY requested_at DESC, id DESC
LIMIT @bounded_page_size;
```

## Response DTO allowlist

Only these fields may be returned:

- `request_id`
- `status`
- `reason` (the original reason supplied by this requester)
- `requested_at`
- `decided_at`

Do not return `media_id`, patient identifiers, procedure identifiers, reviewer identity, `decision_reason`, event actor/reason, storage keys, file names, report contents, or media bytes from these endpoints.

## Audit and observability

If/when implemented, record a minimal access audit event with actor, tenant, request ID, outcome, timestamp, and correlation ID. Never log the reason text, bearer token, patient details, media path, or report content. Ensure audit failure is handled according to the approved security policy; do not silently downgrade an auditable read to an unaudited one.

## Required negative tests before route mapping

1. A requester can retrieve their own request in their active hospital.
2. A different active user in the same hospital receives the same 404 as an unknown ID.
3. The same requester using a different hospital context receives the same 404.
4. An expired, revoked, missing, or disabled session receives 401.
5. Missing or inactive membership receives 401/403 according to the API's consistent membership contract.
6. A list never returns another requester's rows, including when a cursor is replayed by a different actor or hospital.
7. Page size is clamped/rejected above 100; malformed cursors fail closed.
8. DTO serialization contains only the allowlisted fields.
9. Database command text remains constant and all values are parameters.
10. No queue, storage, media download, or deletion service is called.
11. Access is not cached; `Cache-Control: no-store` remains in force.

## Explicit gate

The database currently enforces hospital-level RLS, not requester-level row visibility. Requester isolation therefore depends on the exact SQL predicates above until a separately reviewed database policy is designed. This is acceptable only while no deletion-request read endpoint is mapped. Do not enable a route based solely on this document; obtain owner/governance approval and complete dedicated API integration tests first.
