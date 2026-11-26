# Step 58 — Authentication security-event audit design (approval gate)

## Why a separate security-event stream is required

The existing `audit_events` writer is tenant-scoped and requires a hospital ID and authenticated actor. Login attempts happen before a hospital context is selected, and failed attempts may have no resolvable user. Forcing those events into the clinical/tenant audit table would either lose important events or create unsafe tenant attribution.

The authentication endpoints currently do not persist a dedicated global authentication security-event record. Do not claim authentication audit coverage is complete.

## Proposed minimal event taxonomy

| Event | Subject identity | Safe result metadata |
|---|---|---|
| `login_succeeded` | Resolved user ID | success |
| `login_failed` | Resolved user ID if known; otherwise null | generic invalid credentials |
| `account_locked` | Resolved user ID | lockout threshold reached |
| `logout_succeeded` | Resolved user ID if session resolves | session revoked |
| `session_rejected` | Resolved user ID only when safely known | expired or revoked |
| `tenant_context_denied` | Authenticated user ID | membership absent |

Do not emit one audit event for every malformed or random bearer token. That path is attacker-controlled and can create unbounded audit volume; rate-limit/security telemetry should handle aggregate abuse signals.

## Data minimization contract

Proposed event fields only:
- server-generated event ID
- fixed allowlisted event type
- nullable subject user ID
- server timestamp
- optional validated correlation identifier
- fixed allowlisted outcome code

Never persist:
- passwords or password hashes
- bearer tokens or token hashes
- submitted email addresses
- request bodies or arbitrary exception text
- source IP or device fingerprint until a privacy/retention policy explicitly authorizes it
- patient, procedure, report, media, or other clinical identifiers

The event record must not contain arbitrary JSON supplied by the request.

## Proposed access model

- Dedicated table separate from tenant-scoped clinical `audit_events`.
- API runtime receives INSERT only; no SELECT, UPDATE, DELETE, or TRUNCATE.
- Database-level append-only enforcement.
- No general application endpoint to search or export the global security-event stream.
- A separately authorized compliance/security review path must be designed and audited before any read access is granted.
- No automatic deletion until an approved retention schedule and legal-hold process exist.

## Decisions required before schema/API activation

1. **Retention period:** the legally approved period for global authentication/security events, including treatment of legal holds and expiry.
2. **Read/export authority:** which accountable role or designated security/records-governance function may review or export these events, and under what audit controls.
3. **Privacy basis:** whether source IP, device identifiers, or additional metadata are prohibited or permitted under the applicable privacy policy.

## Safe work completed in this step

This document records the design boundary only. No migration, table, writer, endpoint, log export, retention worker, or new runtime grant is activated. Existing login/session/tenant HTTP tests remain unchanged.

## Approval gate

Do not implement the global security-event table or expose its records until the retention period and accountable read/export authority are explicitly approved. Clinical routes remain disabled.
