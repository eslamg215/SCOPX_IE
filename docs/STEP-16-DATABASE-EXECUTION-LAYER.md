# Step 16 — Database execution layer (starter)

## Added
- `scripts/database/Apply-Migrations.py`: orders numbered SQL migrations, creates a migration ledger, records SHA-256 checksums, skips already-applied migrations, and stops if an applied file's checksum changes.
- Migration execution uses `psql --single-transaction` per migration with `ON_ERROR_STOP`.
- RLS draft migration `004_tenant_rls_policies.sql` is **excluded by default** and requires explicit `--include-rls-draft`.

## Usage (Windows PowerShell)
1. Install PostgreSQL client tools so `psql` is available, and Python 3.
2. Set connection variables for a **disposable development database** (do not place secrets in source files):
   - `$env:PGHOST='localhost'`
   - `$env:PGPORT='5432'`
   - `$env:PGDATABASE='scopx_dev'`
   - `$env:PGUSER='scopx_local'`
   - `$env:PGPASSWORD='your-local-password'`
3. From project root run: `python scripts/database/Apply-Migrations.py`
4. Draft RLS migration only after manual review in a disposable DB: `python scripts/database/Apply-Migrations.py --include-rls-draft`
5. Remove `$env:PGPASSWORD` from the session afterward: `Remove-Item Env:PGPASSWORD`.


## Important limitations
- This script was syntax-checked with Python only. PostgreSQL/`psql` are not installed in the authoring environment, so database execution has **not** been tested here.
- Migrations and ledger recording are separate transactions. If SQL succeeds but ledger insertion fails, inspect the DB before retrying.
- Migration 004 is still draft and can lock out access or expose data if context/grants are misconfigured. Test with a non-owner, non-BYPASSRLS runtime role.
- The current numbered migration files are schema drafts, not an approved production migration set.
- No patient or procedure seed data is provided. Use fictional demo data only after constraints and tenant authorization are implemented and tested.
- The runtime role and database roles must be created/secured separately. Never run API traffic as database owner, superuser, or BYPASSRLS.
