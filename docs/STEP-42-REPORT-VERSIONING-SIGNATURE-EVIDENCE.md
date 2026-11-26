# Step 42 — Immutable report versions and signature evidence

## Added in the security branch

- Migration 010 adds a SHA-256 digest for each report body and a canonical text representation version.
- Every report insert and every content revision creates an immutable snapshot in \`procedure_report_versions\`.
- Draft edits increment the report version and update the canonical SHA-256 digest in the same database transaction.
- Draft updates require the expected current version, preventing stale clients from silently overwriting a newer edit.
- Historical reports are preserved with the explicit legacy canonicalization label \`LEGACY-RAW-UTF8-V1\`; they are not eligible for signing until a new canonical draft version is created.
- \`procedure_report_signatures\` stores the report/version digest, signer hospital membership, re-authentication time and method, canonicalization version, RSA-PSS-SHA256 algorithm, key identifier, and signature bytes.
- Database triggers allow a signature artifact only for the current reviewed report version, transition that report to signed, and prevent mutation of the signed report.
- Report-version and signature artifacts are append-only, including TRUNCATE protection, and are protected by tenant RLS.
- The schema has amendment linkage and a required amendment reason; the amendment creation workflow is not implemented yet.

## Explicitly not complete

- A provider-neutral RSA-PSS-SHA256 signing/verification service now exists. It signs a canonical payload binding tenant, report, version, document digest, signer membership, re-authentication time/method, and key ID. It requires `Reports:SigningPrivateKeyPem` and `Reports:SigningKeyId`; without them it fails closed.
- There is still no signing API endpoint and no report-signing UI. The signing service is not yet wired to the report repository or signature table.
- There is no strong re-authentication flow wired to report signing; the service validates supplied re-authentication metadata but does not itself verify the user's password or MFA.
- There is no managed key storage, rotation, public-key registry, or persisted trusted verification-key lifecycle. Verification currently requires a public PEM supplied by the caller.
- CI runs an ephemeral RSA key-pair test for valid signing/verification, wrong-key rejection, payload/signature tampering, stale re-authentication, missing key configuration, and weak-key rejection. The separate database smoke fixture uses dummy bytes solely to test database constraints and immutability; it is not a cryptographic signature and proves no signer identity.
- No legal/qualified electronic-signature compliance is claimed. Legal requirements vary by jurisdiction and must be independently reviewed.
- Amendment creation, correction/revocation workflow, PDF rendering, final document canonicalization across PDF and structured report fields, and clinical review are outstanding.

## Migration safety

Migration 010 is excluded from normal local migration runs. It requires explicit opt-in:

\`\`\`powershell
python scripts/database/Apply-Migrations.py --include-rls-draft --include-report-signing-draft
\`\`\`

Do not run this on the user's local database until the migration and full report workflow have been reviewed. CI applies it only to a disposable PostgreSQL database and tests RLS, signature-state transition, and artifact immutability.

**Status: database foundation and CI schema tests added; signing and amendments remain disabled. Synthetic data only.**
