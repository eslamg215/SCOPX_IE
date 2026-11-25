# Step 8 — Authorization / tenant isolation implementation checklist

- [ ] ASP.NET authentication fully implemented and verified
- [ ] Identity-to-existing-user schema reconciliation decided
- [ ] Hospital context resolved from verified membership
- [ ] User session context set transaction-locally for every tenant DB transaction
- [ ] Tenant context/user context cleared by transaction boundary; pooling tests pass
- [ ] RLS migration reviewed by PostgreSQL security reviewer and tested against actual ORM
- [ ] Runtime DB role is non-owner, least privilege, no BYPASSRLS
- [ ] Membership resolver can safely discover only current user's memberships
- [ ] Default-deny endpoint authorization policies
- [ ] Role permissions mapped to named policies, not scattered string checks
- [ ] Field-level permissions (reception vs clinical) defined and tested
- [ ] Report signing and amendment lifecycle implemented with audit/version history
- [ ] Platform admin separation, MFA, and privileged-action auditing verified
- [ ] Cross-tenant test fixtures with at least two hospitals and users
- [ ] Anonymous, forbidden, inactive, and cross-tenant negative tests pass
- [ ] No real patient data in development, demo, or test environments

## Required test matrix
| Test | Expected |
|---|---|
| Missing hospital context, tenant query | 0 rows / denied write |
| Missing user context, membership discovery | 0 rows |
| User with membership in A, request B | denied |
| User disabled or membership suspended | denied immediately |
| Reception reads report/findings | forbidden |
| Technician signs report | forbidden |
| Hospital admin grants platform role | forbidden |
| Runtime role updates/deletes audit row | database permission denied |
| Cross-hospital FK link attempt | rejected |
| Pooled connection reused between tenants | no context leakage |
