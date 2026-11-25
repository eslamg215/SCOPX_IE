# SCOPX.I.E — Security Baseline v1.0 (Draft)

> Development-only baseline. Not a compliance certification and not approved for real patient data.

## Security objectives
- Confidentiality, integrity, availability, privacy, traceability, and safe clinical workflow.
- Fail closed when authentication, authorization, tenant resolution, or required audit logging fails.
- Use synthetic data only until security, privacy, clinical, and regulatory gates are reviewed.

## Identity and tenant model
- Platform identity is separate from hospital membership.
- A user may belong to one or more hospitals through an explicit membership record.
- Tenant/hospital context is resolved server-side from the authenticated membership; client-supplied hospital IDs are never trusted as authorization.
- Every tenant-owned database row includes `HospitalId` (or equivalent tenant key) and is filtered by tenant in the data-access layer.
- Cross-tenant access is denied by default and covered by automated tests.

## Initial roles (least privilege)
- **Platform Super Admin:** manage platform configuration, hospitals, licensing, platform operators; no routine patient browsing. Any exceptional support access must be time-limited, justified, approved, and audited.
- **Hospital Admin:** manage local users, role assignments within allowed roles, hospital settings; no platform-wide administration.
- **Doctor:** view assigned/authorized patients and procedures; create/edit clinical notes and reports for permitted encounters; sign/finalize only own authorized work (delegation policy TBD).
- **Nurse:** view assigned workflow and patient context; enter permitted nursing/procedure observations; cannot finalize physician reports.
- **Endoscopy Technician:** manage room/device readiness and capture workflow; patient details limited to what is needed for procedure association; cannot edit clinical conclusions.
- **Reception:** demographic registration and scheduling; no access to procedure media or clinical notes by default.
- **Auditor (read-only):** access approved audit/reporting scope only; no clinical editing or media export.

## Data protection controls
- TLS for all network communication; local development may use loopback only.
- Secrets never committed to source control. Development secrets are disposable; production uses a managed secret/key system.
- Encryption-at-rest requirements and key ownership must be decided before deployment.
- Patient identifiers and clinical content are excluded from application logs, telemetry, error traces, and AI prompts unless a reviewed, purpose-limited design explicitly permits them.
- Backups encrypted, access controlled, retention defined, and restore tests documented.
- Media export/download requires explicit permission, purpose, and audit event.
- Data retention/deletion, consent, and legal hold policies must be documented per deployment.

## Audit events
Record at minimum: authentication outcomes, user/hospital membership changes, permission changes, patient/procedure record access where required by policy, edits/finalization, exports, deletion attempts, license changes, AI requests/results/acceptance, security configuration changes, and support-access events. Audit records should be append-oriented and protected against ordinary users altering them.

## AI guardrails
- AI is assistive, not an autonomous diagnostic authority.
- Generated content is labeled as AI-generated and requires qualified clinician review before entering a finalized record.
- AI cannot silently write, delete, sign, or export clinical records.
- AI input/output handling, provider, retention, and consent/privacy implications must be reviewed before real data use.

## Initial threat list
| Threat | Baseline mitigation | Verification planned |
|---|---|---|
| Cross-hospital data leakage | server-side tenant context, scoped queries, deny-by-default | cross-tenant integration tests |
| Privilege escalation | explicit permission policy, role separation | authorization tests |
| Credential compromise | secure password/auth provider, MFA for admins, rate limiting | security review and tests |
| Media theft | restrictive access, encryption design, export audit | access/export tests; storage review |
| Secret leakage | no secrets in repo; secret scanning | repository scan |
| Malicious/unsafe AI output | clinician review, no autonomous writes | workflow tests and policy review |
| Ransomware/data loss | backup strategy, least privilege, restore drills | restore test before pilot |
| Audit tampering | restricted append-oriented audit storage | integrity/permission tests |

## Open decisions before production
- Authentication provider and MFA implementation.
- Hospital isolation approach: application-level scoping plus PostgreSQL Row-Level Security (RLS) evaluation.
- On-premises/cloud/hybrid deployment and key management.
- Consent, retention, export, and deletion policies.
- Regulatory and medical-device/software classification review for target markets.
- Threat-model workshop and penetration testing.
