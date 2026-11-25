# Step 13 — Admin portal and mobile companion architecture

Status: **Product specification; neither client is implemented.**

## Web Admin portal
Separate web client; communicates only with API. Responsive layout and platform administration focus.

### Platform Admin (SCOPX operator)
- Hospital onboarding/suspension, tenant metadata, licensing and renewal.
- Admin accounts, secure bootstrap and MFA status.
- System health, release/version inventory, incident view, AI provider configuration (secret values never shown), feature flags.
- Audit review, security alerts, usage/limits, billing-ready usage metrics (billing not in first prototype).
- No default access to clinical records or media.

### Hospital Admin
- Hospital profile and rooms/devices configuration.
- Staff invitations, membership lifecycle and hospital-scoped role grants.
- License status and seat/device usage view.
- Storage status, backup status, audit review scoped to hospital.
- Export policy and retention settings only within approved bounds; privileged changes are audited.

## Mobile companion app (Android first)
A secondary app for authorized staff; not a replacement for workstation capture.
Initial use cases:
- Secure sign-in, hospital switching only among verified memberships.
- Read authorized report summaries and finalized reports.
- Review still images only if privacy controls and secure cache are validated.
- Notifications contain no patient identifiers or sensitive clinical content.
- Optional workflow notifications with deep links that re-check server permissions.
- No offline patient-data cache in first release; offline access may be reconsidered after encryption, expiry, remote wipe, and risk review.
- Signing/approval only if re-authentication, role permissions, audit, and policy are explicitly implemented and tested.

## Sync and mobile security
- API is source of truth; mobile does not trust local role/tenant claims.
- Short session lifetime, refresh rotation/revocation according to chosen auth standard.
- TLS; device OS secure storage for tokens; jailbreak/root risk assessment; app privacy screen as appropriate.
- Download links expire and are single-purpose; access checked server-side at download.
- No clinical content in push payloads, analytics, crash reports, or logs.

## Release sequencing
1. API/auth/tenant isolation first.
2. Windows desktop prototype for core procedure-room workflow.
3. Web Admin hospital/user/license functions.
4. Android companion for reports and notifications.
5. Offline features only after a separate threat/privacy review.
