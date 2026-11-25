# SCOPX.I.E — Architecture Blueprint v0.1

## Product boundaries
- **Desktop workstation:** procedure workflow and local capture; hardware integrations via adapter interfaces.
- **Backend:** authentication, hospital/tenant boundary, patients, procedures, reports, licensing, sync, audit.
- **Web Admin:** platform administration and hospital-level configuration.
- **Mobile:** scoped read/approved workflow access; no unrestricted patient-data export.

## Proposed technology
- .NET 10 LTS / ASP.NET Core API
- PostgreSQL
- Windows desktop client: WPF (initial candidate)
- Web admin: React + TypeScript (candidate, later phase)
- Mobile: Android client (technology decision deferred)
- Video capture: adapter layer; device-specific SDKs and capture cards assessed individually

## Tenant and security principles
1. Every hospital-owned record carries a tenant/hospital identifier.
2. Tenant authorization is enforced server-side on every request, not merely in UI.
3. Least-privilege role permissions; separate platform and hospital administration.
4. Audit trail for access, exports, edits, deletions, admin actions, and AI actions.
5. Encrypt in transit; evaluate encryption at rest, key management, backups, and workstation storage.
6. Use synthetic data for development and demos.
7. AI outputs are assistive, clearly labeled, and require clinician review; initial versions must not autonomously diagnose or alter records.
8. Secrets stored outside source code; environment variables only for local development, managed secret store for deployment.
9. Video/image retention and consent policies must be defined before production use.
10. Threat model and regulatory review required before handling real patient data.

## Initial domain modules
- Identity & Access
- Hospitals / Tenant Settings
- Patient Registry
- Scheduling
- Procedure Sessions
- Media Capture / Storage
- Reports
- AI Assist
- License & Activation
- Audit / Security Events
- Backup / Restore

## Decisions intentionally not locked
- Capture-card / camera SDK compatibility
- Medical interoperability (HL7/FHIR scope)
- Cloud vs on-premises vs hybrid deployment
- Mobile technology
- Licensing vendor and activation design
- Production storage and retention architecture

## Phase 1 exit criteria
- Architecture and threat model reviewed
- Roles and tenant boundaries defined
- Synthetic-data environment running
- Database migration strategy selected
- API health check and tests run
- Hardware integration plan based on actual workstation and endoscopy stack
