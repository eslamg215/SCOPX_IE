# Step 20 — Offline structural validation

Added `scripts/tests/validate_project.py`, a lightweight Python validation script that checks migration numbering, draft-migration opt-in, transaction/checksum safeguards in the runner, API scaffold security middleware markers, security helper files, tenant-context markers in draft RLS, and README non-clinical disclaimers.


## Run

From the repository root:

```bash
python scripts/tests/validate_project.py
```

## Scope and limitations

This is structural/static validation only. It does not parse SQL, start PostgreSQL, apply migrations, build .NET, validate ASP.NET middleware execution, or prove RLS/tenant isolation. Any test that expects code fragments is a maintenance tripwire, not a substitute for integration tests.
