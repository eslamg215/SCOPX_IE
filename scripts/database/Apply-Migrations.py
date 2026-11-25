#!/usr/bin/env python3
"""SCOPX.I.E migration runner. Requires psql and standard PG* environment vars."""
from pathlib import Path
import argparse, hashlib, os, shutil, subprocess, sys

ROOT = Path(__file__).resolve().parents[2]
MIGRATIONS = ROOT / "src" / "SCOPX.Infrastructure" / "Database" / "Migrations"
parser = argparse.ArgumentParser(description="Apply ordered SCOPX.I.E SQL migrations with checksum ledger.")
parser.add_argument("--include-rls-draft", action="store_true", help="Include draft RLS migration 004 (manual review/disposable DB only).")
parser.add_argument("--include-auth-draft", action="store_true", help="Include draft authentication and session migrations 005-006 (manual review/disposable DB only).")
parser.add_argument("--include-audit-hardening-draft", action="store_true", help="Include draft audit hardening migration 007 (manual review/disposable DB only).")
parser.add_argument("--include-tenant-hardening-draft", action="store_true", help="Include tenant hardening migrations 008-009; requires --include-rls-draft and a disposable reviewed DB.")
args = parser.parse_args()
if args.include_tenant_hardening_draft and not args.include_rls_draft:
    sys.exit("ERROR: --include-tenant-hardening-draft requires --include-rls-draft.")
if not os.environ.get("PGDATABASE"):
    sys.exit("ERROR: Set PGDATABASE (and PGHOST/PGPORT/PGUSER/PGPASSWORD as needed) first.")
if not shutil.which("psql"):
    sys.exit("ERROR: psql was not found on PATH. Install PostgreSQL client tools.")

files = sorted(MIGRATIONS.glob("[0-9][0-9][0-9]_*.sql"))
if not files:
    sys.exit(f"ERROR: No migrations found in {MIGRATIONS}")
if not args.include_rls_draft:
    files = [f for f in files if f.name != "004_tenant_rls_policies.sql"]
if not args.include_auth_draft:
    files = [f for f in files if f.name not in {"005_user_authentication.sql", "006_user_sessions.sql"}]
if not args.include_audit_hardening_draft:
    files = [f for f in files if f.name != "007_audit_immutability_hardening.sql"]
if not args.include_tenant_hardening_draft:
    files = [f for f in files if f.name not in {"008_role_scope_enforcement.sql", "009_hospital_directory_rls.sql"}]

psql_base = ["psql", "--no-psqlrc", "--set", "ON_ERROR_STOP=1", "--dbname", os.environ["PGDATABASE"]]
def run_sql(sql: str):
    subprocess.run(psql_base + ["--command", sql], check=True)
def run_file(path: Path):
    subprocess.run(psql_base + ["--single-transaction", "--file", str(path)], check=True)

# Ledger bootstrap, idempotent. Migrations themselves are executed transactionally.
run_sql("CREATE TABLE IF NOT EXISTS public._scopx_schema_migrations (migration_name text PRIMARY KEY, sha256 text NOT NULL, applied_at timestamptz NOT NULL DEFAULT now())")
rows = subprocess.run(psql_base + ["--tuples-only", "--no-align", "--command", "SELECT migration_name || '|' || sha256 FROM public._scopx_schema_migrations ORDER BY migration_name"], check=True, capture_output=True, text=True).stdout
ledger = {}
for row in rows.splitlines():
    if "|" in row:
        name, checksum = row.strip().split("|", 1)
        ledger[name] = checksum

for path in files:
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    if path.name in ledger:
        if ledger[path.name] != digest:
            sys.exit(f"ERROR: Applied migration checksum changed: {path.name}. Use a forward migration; do not edit history.")
        print(f"SKIP already applied: {path.name}")
        continue
    print(f"APPLY: {path.name}")
    run_file(path)
    # Record only after successful SQL execution.
    safe_name = path.name.replace("'", "''")
    run_sql(f"INSERT INTO public._scopx_schema_migrations (migration_name, sha256) VALUES ('{safe_name}', '{digest}')")
    print(f"DONE: {path.name}")
print("Migration run completed. This confirms execution only; it is not a security or clinical validation.")
