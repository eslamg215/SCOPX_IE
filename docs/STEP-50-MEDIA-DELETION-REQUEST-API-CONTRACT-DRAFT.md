# Step 50 — Media deletion request API contract (draft only)

## Status

Contract proposal only. No endpoint is mapped or enabled. This draft does not grant any role a deletion capability. API implementation remains gated on written approval of the role matrix in Step 49 and review of the hospital's retention/legal policy.

## Proposed endpoints

| Method | Path | Purpose | State effect |
|---|---|---|---|
| POST | `/api/v1/media/{mediaId}/deletion-requests` | Submit a request with a required reason | Creates `submitted` |
| GET | `/api/v1/media/deletion-requests/{requestId}` | Read status visible to an authorized tenant actor | None |
| GET | `/api/v1/media/deletion-requests` | List/filter authorized requests for current hospital | None |
| POST | `/api/v1/media/deletion-requests/{requestId}/claim` | Claim a request for review | `submitted` → `under_review` |
| POST | `/api/v1/media/deletion-requests/{requestId}/decision` | Record an approve/reject decision with mandatory reason | `under_review` → `approved` or `rejected` |
| POST | `/api/v1/media/deletion-requests/{requestId}/cancel` | Request cancellation before final decision | Allowed pre-decision only |

The final endpoint names and whether claim/decision should be separate remain subject to API review. A caller must never submit `hospitalId`, actor IDs, reviewer IDs, timestamps, or a target storage key as trusted authority.

## Common request handling requirements

- Resolve the bearer session and active hospital membership using the existing authentication/session flow.
- Derive hospital ID and actor ID server-side; ignore or reject client-supplied authority fields.
- Apply a dedicated permission check for each action. Generic media read/write/download roles are insufficient.
- Use a transaction with tenant context and rely on database state-transition guards.
- Return generic not-found responses where needed to avoid cross-tenant resource enumeration.
- Validate request reason length and reject whitespace-only input.
- Never include patient names, report body, media bytes, passwords, bearer tokens, or storage paths in logs/audit JSON.
- Use idempotency/concurrency handling for duplicate submissions and competing reviewer actions.
- Map authorization failures to 401/403 and invalid state transitions to stable non-sensitive 409 responses.

## Critical approval/enqueue separation

An `approved` state is only a recorded decision. It is not an instruction to delete. A future enqueue endpoint/service must be a separate capability and must re-check in one transaction:

1. Media belongs to the authenticated hospital and remains eligible.
2. No active legal hold exists.
3. Retention deadline is non-NULL and has elapsed.
4. Media is not already deleting/deleted.
5. Required governance approvals and reviewer separation are satisfied.
6. Queue insertion and the corresponding audit/deletion event are atomic.

No queue insertion, worker dispatch, storage deletion, or metadata tombstoning is part of this contract draft.

## Required API tests before mapping routes

- Unauthenticated, expired, revoked, and disabled-user requests.
- Inactive membership and cross-tenant resource IDs.
- Each role denied every action not explicitly approved in Step 49.
- Requester self-approval and reviewer mismatch.
- Missing/blank/oversized reasons.
- Invalid, repeated, stale, and concurrent state transitions.
- Legal hold active, retention NULL/future/elapsed.
- Duplicate open request race.
- Database rollback leaves no partial request/event/audit rows.
- Verify no object-store operation occurs in request, claim, or decision endpoints.
- Verify response bodies and logs do not disclose clinical content or storage paths.

## Implementation gate

Do not map these endpoints until the owner/governance policy is approved in writing and the permission matrix is translated into explicit server-side authorization tests. The current migration 014 remains a database workflow draft and does not make the API clinical-production-ready.
