# Step 47 — Media deletion request workflow design (draft)

## Scope and safety boundary

This step defines a reviewable request lifecycle only. It does not create API routes, approve requests, enqueue deletion work, run a worker, tombstone metadata, or delete stored objects. Existing synthetic CI coverage remains the only exercised environment.

## Findings from migrations 011–013 and current API

- `media_deletion_queue` is an execution queue, not a user-facing request inbox. Its database guard rejects pending/processing entries while a legal hold is active, when the retention deadline is NULL, or before that deadline has elapsed.
- Migration 012 allows at most one open execution-queue item per media asset and makes legal-hold release one-way.
- `media_deletion_events` is append-only.
- The media API currently exposes list, upload, and download operations. There are no deletion-request endpoints.
- Media endpoint authorization currently resolves an active session and hospital membership, then applies broad read/write/download role predicates. Those predicates are not sufficient evidence for a distinct, high-impact deletion approval permission.
- `NpgsqlAuditEventWriter.WriteAsync` supports writing operational audit metadata within the caller's database transaction. Audit details must not contain patient identifiers, report text, media content, credentials, or tokens.

## Required separation of concerns

Do not use `media_deletion_queue` as the request record. A request may be submitted before the retention period expires, and it may need review while a legal hold is active. Putting such a request directly in the guarded execution queue would either violate the guard or weaken it.

A future implementation should introduce a separate request record (new migration, not a reinterpretation of the existing queue), with an explicit state machine:

- `submitted` — request recorded; no deletion is scheduled.
- `under_review` — an authorized reviewer has claimed/reviewed it.
- `approved` — approval recorded, but still not permission to delete immediately.
- `rejected` — terminal decision with a required reason.
- `cancelled` — requester or authorized administrator cancelled before approval.
- `expired` — optional terminal state for stale requests, only if a reviewed policy defines expiry.

Approval must re-check, in the same transaction, tenant ownership, current media state, retention deadline, active legal holds, and the reviewer's permission. If the retention deadline has not elapsed or a legal hold is active, approval must not create an execution-queue item. Once every gate is satisfied, a separate, explicit enqueue action may create the queue item and corresponding append-only event atomically. A worker and physical deletion remain out of scope.

## Proposed authorization safeguards — not yet implemented

1. Require an active session and active membership in the media asset's hospital for every operation.
2. Scope all reads and writes by the resolved hospital ID; never accept hospital ID from request payload as authority.
3. Separate permissions for submit, cancel, review, approve, and reject. Existing generic media write permission must not imply approval permission.
4. Require a reviewer distinct from the requester for approval (four-eyes control); do not allow self-approval.
5. Require a reason for submission, rejection, cancellation by an administrator, and any exceptional review decision.
6. Enforce valid state transitions in the database/service layer; reject repeated or out-of-order transitions.
7. Write the state change and audit event in the same database transaction. Record only request ID, media ID, action, actor ID, and non-sensitive reason metadata; do not copy patient or media contents into audit JSON.
8. Keep request records and decision history append-only or otherwise tamper-evident. Do not hard-delete requests.
9. Re-check legal holds and retention at approval/enqueue time, not only at initial submission, to avoid time-of-check/time-of-use gaps.
10. Maintain tenant RLS on request and event tables and test cross-tenant access with the least-privilege runtime role.

## Blocking decisions before API implementation

The repository does not yet establish a reviewed, explicit role-to-permission mapping for high-impact deletion approval. The exact roles permitted to submit, review, approve, reject, and cancel must be confirmed by the product owner and the hospital's governance/legal policy. This cannot safely be inferred from generic doctor/nurse/admin labels.

Until those permissions and the applicable retention/legal policy are approved:
- do not map deletion-request routes;
- do not add an approval-to-queue transition;
- do not enable a background worker or physical deletion.

## Verification plan for the implementation phase

Use disposable PostgreSQL and synthetic fixtures only. Add tests for every allowed and denied state transition, requester self-approval denial, missing reason, tenant isolation, inactive membership, active legal hold, NULL/future/elapsed retention deadlines, duplicate open requests, atomic audit rollback, and concurrent approval attempts. Verify no object-store operation occurs in any request/review test.

## Status

Design review artifact only. No runtime behavior changed. Not production, legal, or clinical approval.
