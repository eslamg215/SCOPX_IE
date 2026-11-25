# SCOPX.I.E — Initial Role/Permission Matrix v1.0 (Draft)

Legend: **A** = allowed within assigned hospital/scope; **L** = limited; **—** = denied by default.

| Capability | Super Admin | Hospital Admin | Doctor | Nurse | Technician | Reception | Auditor |
|---|---:|---:|---:|---:|---:|---:|---:|
| Platform/hospital provisioning | A | — | — | — | — | L |
| Hospital settings | L | A | — | — | L | L |
| User management (local) | L | A | — | — | — | L |
| Role assignment | Platform roles only | Allowed local roles only | — | — | — | — |
| Register patient demographics | — | L | A | L | L | — |
| Schedule appointments | — | A | L | L | A | — |
| View patient clinical chart | exceptional audited access only | L | A | L | L | approved read-only scope |
| Clinical notes/report authoring | — | — | A | limited observations | — | — |
| Finalize/sign physician report | — | — | A | — | — | — |
| Procedure room/device setup | — | L | L | A | — | — |
| Capture/associate media | — | L | L | A | — | — |
| Media viewing | — | L | A | L | — | approved scope only |
| Media export | — | permission + audit | permission + audit | permission + audit | — | — |
| License/activation management | A | hospital license view only | — | — | — | — |
| Audit-log access | platform scope | hospital scope | own/related events limited | own/related events limited | — | A (approved scope) |

## Notes
- This matrix is a starting point for stakeholder review; it is not final workflow policy.
- Permissions must be implemented as granular policy checks, not just role-name checks.
- Assignment, consent, emergency access, and delegation rules are not yet defined.
- Super Admin patient access is denied for routine use. Any break-glass path must be separately designed with reason, approval, time limit, and immutable audit.
- No permission implies data export unless explicitly granted.
