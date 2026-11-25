# Step 9 — API workflow contract review checklist

- [ ] Route scope and permission documented for every endpoint
- [ ] Tenant authorization is enforced server-side and database-side
- [ ] Request/response DTOs are separated from database entities
- [ ] Validation and safe error model implemented
- [ ] Paging, sorting, and query limits defined
- [ ] Optimistic concurrency implemented
- [ ] Idempotency for media/report creation implemented
- [ ] Procedure transition service implemented and tested
- [ ] Report draft/review/sign/amend lifecycle implemented and tested
- [ ] Signed reports immutable with amendment version chain
- [ ] AI output is labeled, provenance retained, clinician review required
- [ ] Private media storage; no storage key/path leakage
- [ ] Upload size/type/content scanning protections
- [ ] Audit events for sensitive actions and denied access
- [ ] Cross-tenant tests and API integration tests pass
