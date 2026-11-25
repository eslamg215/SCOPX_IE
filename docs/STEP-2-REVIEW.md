# Step 2 — Database foundation review

**Status:** Draft SQL written and statically reviewed. Execution against PostgreSQL is not yet possible in this environment because Docker/PostgreSQL are unavailable.

## Deliverables
- `DATABASE-DESIGN-v0.1.md` — domain/entity overview
- `001_identity_tenancy.sql` — first SQL migration for users, hospitals, memberships, roles, and role assignments

## Review checks
- [x] UUID keys and timestamp fields present
- [x] Normalized email unique constraint
- [x] Hospital/user membership unique constraint
- [x] Composite hospital-membership key supports tenant-consistent FK design
- [x] Role scope distinguishes platform vs hospital roles
- [x] No default platform Super Admin account created by SQL
- [x] Initial hospital roles seeded idempotently
- [x] Indexes added for common membership lookups
- [ ] Apply migration to a real PostgreSQL instance
- [ ] Confirm SQL syntax/runtime behavior and migration idempotency
- [ ] Add automated migration test and tenant-boundary integration tests

## Issues/limitations caught
- SQL uses `pgcrypto` for UUID generation; check extension availability in the chosen PostgreSQL deployment.
- No RLS policies yet. RLS will be added only alongside a tenant-context transaction mechanism and tests, to avoid a false sense of security from incomplete policies.
- User authentication provider/password storage is intentionally not implemented in this migration.

## Step decision
Proceed to patient/appointment/procedure domain draft only after tenant FK patterns are reviewed. These tables must carry `hospital_id` and use tenant-consistent constraints.
