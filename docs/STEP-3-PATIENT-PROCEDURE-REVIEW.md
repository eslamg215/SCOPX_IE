# Step 3 review — patient and procedure data foundation

**Status:** schema drafted and statically reviewed; not executed against PostgreSQL.

## Included
- Hospital-scoped patient records with unique patient codes per hospital.
- Hospital-scoped procedure records; composite foreign keys prevent cross-hospital patient and membership references.
- Media metadata for image/video/audio files; actual file bytes remain outside the database.
- Versioned procedure report drafts and signing metadata.
- Audit event table with indexes for hospital/time and actor/time.

## Review notes / risks to address before clinical use
- This migration has not been run against a live PostgreSQL server; SQL syntax/integration remains unverified.
- API authorization, tenant context, row-level security, encryption at rest/in transit, secret management, audit immutability, backups/restore, retention, deletion/legal hold, and incident response are not implemented by these tables.
- Patient name search index is a plain index, not a privacy-preserving search solution.
- Do not store actual media in the database. Implement a protected storage service, generated storage keys, malware scanning where applicable, and access logging.
- Report signatures are metadata placeholders; a real signing workflow must enforce authorization, integrity, timestamping, and amendment rules.
- Audit table is not append-only by itself. Database permissions/triggers and external log protection are required.
- Never use real patient data in this scaffold.

## Acceptance checklist
- [x] Tenant consistency enforced by composite foreign keys for patients/procedures/memberships.
- [x] Soft archive field on patient records.
- [x] Procedure lifecycle constraints.
- [x] Media metadata table and report versioning.
- [x] Audit event base table.
- [ ] Execute migration on disposable PostgreSQL instance.
- [ ] Integration tests for cross-hospital FK rejection.
- [ ] Security review and API authorization implementation.
