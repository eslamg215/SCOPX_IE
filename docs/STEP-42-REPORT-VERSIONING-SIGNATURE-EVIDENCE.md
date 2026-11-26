# Step 42 — Immutable report versions and signature evidence

## Added in the security branch

- Migration 010 adds a SHA-256 digest for each report body and a canonical text representation version.
- Every report insert and every content revision creates an immutable snapshot in \`procedure_report_versions\`.
- Draft edits increment the report version and update the canonical SHA-256 digest in the same database transaction.
- Historical reports are preserved with the explicit legacy canonicalization label \`LEGACY-RAW-UTF8-V1\`; they are not eligible for signing until a new canonical draft version is created.
- \`procedure_report_signatures\` stores the report/version digest, signer hospital membership, re-authentication time and method, canonicalization version, RSA-PSS-SHA256 algorithm, key identifier, and signature bytes.
- Database triggers allow a signature artifact only for the current reviewed report version, transition that report to signed, and prevent mutation of the signed report.
- Report-version and signature artifacts are append-only, including TRUNCATE protection, and are protected by tenant RLS.
- The schema has amendment linkage and a required amendment reason; the amendment creation workflow is not implemented yet.

## Explicitly not complete

- There is no signing API endpoint and no report-signing UI.
- There is no strong re-authentication flow wired to report signing.
- There is no private-key provisioning, managed key storage, rotation, public-key registry, or signature-verification service.
- The CI signature fixture uses dummy bytes solely to test database constraints and immutability. It is not a cryptographic signature and proves no signer identity.
- No legal/qualified electronic-signature compliance is claimed. Legal requirements vary by jurisdiction and must be independently reviewed.
- Amendment creation, correction/revocation workflow, PDF rendering, final document canonicalization across PDF and structured report fields, and clinical review are outstanding.

## Migration safety

Migration 010 is excluded from normal local migration runs. It requires explicit opt-in:

\`\`\`powershell
python scripts/database/Apply-Migrations.py --include-rls-draft --include-report-signing-draft
\`\`\`

Do not run this on the user's local database until the migration and full report workflow have been reviewed. CI applies it only to a disposable PostgreSQL database and tests RLS, signature-state transition, and artifact immutability.

**Status: database foundation and CI schema tests added; signing and amendments remain disabled. Synthetic data only.**
