# Step 38 — Membership Discovery RLS Context Repair

## Problem found
The optional draft RLS migration (004) scopes membership discovery using the transaction-local `scopx.user_id` setting. The membership repository previously issued queries without setting this trusted context. It also joined `membership_roles`, which is tenant-scoped by `scopx.hospital_id`; without setting the hospital context, role codes could be filtered out under RLS.

## Change
- Membership repository now opens a transaction for each lookup.
- Sets `scopx.user_id` transaction-locally from the authenticated session-derived user ID.
- For `/mine`, first fetches memberships visible to that user, then performs role lookup separately per hospital, setting the hospital context from a trusted membership row returned by the database (not a client header).
- Selected-hospital lookup sets both user and hospital context before the RLS-protected join.
- Validator includes static assertions for both settings and the per-hospital role lookup flow.

## Limitations / safety
- Migration 004 remains a DRAFT and was not applied to a live database.
- This repair has not been compiled or run against PostgreSQL; static checks only.
- Production readiness still requires tenant cross-access tests with a non-owner runtime DB role and RLS enabled/forced, including negative tests for two tenants.
