# Step 49 — Media deletion request authorization matrix (draft)

## Status and non-negotiable boundary

This is a proposal for owner/governance review, not an approved policy. No route, grant, role assignment, approval capability, queue insertion, worker, or deletion operation is enabled by this document. The product owner and each hospital's authorized governance/legal representatives must approve the role mapping before enforcement.

The current identity schema defines these hospital-scoped roles: `hospital_admin`, `doctor`, `nurse`, `endoscopy_technician`, `reception`, and `auditor`. Their labels alone do not establish clinical or legal authority to delete medical records.

## Proposed least-privilege matrix for review

| Action | Conservative proposal | Reason |
|---|---|---|
| Submit request | Doctor, nurse, endoscopy technician; optionally hospital admin | Allows staff to raise a request without granting deletion authority |
| View request status | Requester; designated hospital admin/reviewer; auditor read-only if governance permits | Limits access to operational need |
| Claim / begin review | Hospital admin only, or a separately appointed records-governance role if one is introduced | Separates reviewer from requester |
| Approve | Two-person approval: assigned reviewer plus a second authorized approver; hospital admin is a candidate, not automatically authorized | A high-impact medical-record decision should not rest on a generic role label |
| Reject | Assigned reviewer, with mandatory reason | Creates accountable disposition |
| Cancel before review | Requester | Prevents another user from silently withdrawing a request |
| Cancel after review begins | Requester request-to-cancel only; reviewer/admin records the outcome | Preserves review history |
| Enqueue eligible deletion | Separate service identity after fresh retention/legal-hold checks and approved policy | Approval itself must not cause physical deletion |
| Execute deletion | Isolated worker with narrowly scoped storage/database permissions | Keeps API users away from destructive infrastructure privileges |

## Rules that should remain invariant regardless of the chosen role matrix

1. Requester and final approver must be different people.
2. The reviewer must have an active membership in the same hospital.
3. Every action must be tenant-scoped and audited in the same transaction.
4. The API must derive hospital identity from the authenticated membership, never trust a client-supplied hospital ID.
5. A legal hold or unexpired/unknown retention deadline blocks enqueue and physical deletion even if a request has been approved.
6. Re-check media ownership, legal holds, retention, and current state at enqueue time to prevent time-of-check/time-of-use gaps.
7. No route may interpret generic media write permission as deletion approval permission.
8. No direct database or API hard-delete path is allowed for request/event history.
9. Audit metadata must not contain patient names, report contents, media bytes, credentials, or bearer tokens.
10. Denials, repeated transitions, stale reviewer assignments, and concurrent decisions must be tested.

## Decisions required before API implementation

- Whether `hospital_admin` alone may review/reject, or whether a dedicated records-governance role is required.
- Whether approval requires one authorized approver or two distinct approvers (recommended: two-person control for actual enqueue eligibility).
- Whether doctors/nurses/technicians may submit requests and whether the submitter may view only their own request.
- Whether auditors can read request reasons or only status and event metadata.
- Who may request cancellation after review starts.
- The hospital's legal retention schedule and legal-hold release authority.
- Who is the accountable data controller/records custodian for each tenant.

## Safe work that can proceed without these decisions

- Read-only request status/list design.
- API contract drafts with all privileged operations explicitly disabled.
- Negative authorization test cases for every role.
- Audit event schema and observability that exclude sensitive clinical content.
- Operational documentation and synthetic-only integration tests.

No role-to-permission policy is activated by this draft. Until written approval exists, privileged request routes remain unmapped.
