# Step 8 — Authorization and tenant-isolation review

Status: **Design + draft migration only. Not runtime validated.**

## Decision
Authorization is two-stage: (1) authenticated user identity, then (2) active hospital membership and a role grant for the requested hospital. The hospital context is resolved by server-side membership validation; client-provided hospital IDs are selectors only, never trusted claims.

## Default-deny requirements
- All endpoints except explicitly public health/metadata endpoints require authentication.
- No membership, inactive membership, inactive hospital, or missing role => deny.
- Platform roles and hospital roles are separate. A platform role must not implicitly grant clinical access to a hospital.
- Every patient/procedure/media/report query includes tenant scope, with PostgreSQL RLS as defense in depth.
- Cross-hospital foreign keys must be composite (hospital_id + entity_id) wherever tenant-bound entities link.
- Audit events are append-only to application roles; PHI, passwords, access tokens, and secrets must not be placed in audit details.

## Tenant DB session
The trusted API sets `scopx.hospital_id` with `SET LOCAL`/transaction-local semantics only after verifying membership. Missing/invalid setting returns no rows (fail closed). Connection pooling must never leak tenant context; context must be set inside each transaction and cleared by transaction end. Never set tenant context from a raw HTTP header without membership validation.

## Role outline (initial, must be refined)
- Platform Super Admin: platform administration only; no default access to patient records.
- Hospital Admin: hospital configuration, staff/membership administration; no clinical signing by default.
- Doctor: patient/procedure access, clinical report authoring and signing (within approved workflow).
- Nurse: patient/procedure workflow access as assigned; no report signing by default.
- Endoscopy Technician: procedure-room workflow and media capture; no report signing.
- Reception: patient demographics and scheduling; no clinical findings/report access by default.
- Auditor: read-only, limited to explicit scope; sensitive media access is separately controlled.

## Authorization rules for important actions
| Action | Minimum role (draft) | Additional condition |
|---|---|---|
| View patient | Doctor, Nurse, Hospital Admin only if granted, Auditor | active membership + hospital scope; Hospital Admin clinical view is not automatic |
| Edit demographics | Hospital Admin, Doctor, Nurse, Reception | hospital scope; field-level restrictions to be defined |
| Create procedure | Doctor, Nurse, Endoscopy Technician | patient belongs to selected hospital |
| Capture media | Endoscopy Technician, Doctor, Nurse | active procedure and authorized room/device |
| Draft report | Doctor | procedure in hospital scope |
| Sign report | Doctor | explicit signing permission, step-up authentication policy, no unsigned/AI-only sign-off |
| Amend signed report | Doctor | creates amendment/version; original preserved |
| Manage staff/roles | Hospital Admin | cannot grant platform roles; privilege escalation guard |
| Manage hospitals/licenses | Platform Super Admin | privileged action audit + MFA |

## RLS draft
Migration `004_tenant_rls_policies.sql` enables RLS and creates policies for tenant tables. The application role must not own these tables or have BYPASSRLS. A separate migration owner is required. FORCE ROW LEVEL SECURITY is applied so table owners are also subject to policies (except superuser/BYPASSRLS). The API should use a least-privilege runtime database role.


## Acceptance tests before implementation is considered done
1. Anonymous request to protected API => 401.
2. Authenticated but missing role => 403.
3. Inactive user/membership/hospital denied immediately.
4. User in hospital A cannot read/write hospital B by changing route, query, payload, or headers.
5. Missing tenant session setting yields zero rows / rejected writes.
6. Pooled connection reused from A to B cannot carry A's context.
7. Composite FKs reject cross-hospital procedure/media/report links.
8. Reception cannot view clinical report or findings; technician cannot sign report; hospital admin cannot grant platform role.
9. Signed report is immutable; amendment creates a new version.
10. Audit trail cannot be modified/deleted by runtime role; test tampering and event coverage.
11. All deny paths are logged without including PHI/secrets.

## Review caveats
The RLS migration is a draft, and must be reviewed against the final schema, ORM queries, connection pooling, and threat model before applying. PostgreSQL policies do not replace API authorization. No real patient data is permitted in this project until implementation, threat modeling, validation, operational safeguards, and applicable compliance review are complete.
