# Step 46 — Runtime-role retention stamping verification (draft)

Step 45 proved the migration trigger using owner-level synthetic fixtures. Step 46 closes that test gap by inserting a synthetic media row while the PostgreSQL session is explicitly running as `scopx_runtime` with hospital-A transaction-local identity and tenant context.

The smoke test now verifies that:

- the runtime role can insert a media row only within its selected hospital tenant;
- the trigger can read that tenant's explicitly enabled retention policy under row-level security;
- a caller-supplied expired deadline is ignored and replaced with the configured one-day deadline;
- the server-controlled creation timestamp and calculated deadline match the transaction timestamp;
- a subsequent attempt by the runtime role to rewrite the retention deadline is rejected with SQLSTATE `55000`.

This is a disposable PostgreSQL CI fixture using synthetic data. It does not enable a worker, scheduler, physical object deletion, tombstoning, or media API routes. It is not production, legal, or clinical approval.

## Validation

Pending GitHub Actions for the commit that adds the runtime-role smoke coverage.
