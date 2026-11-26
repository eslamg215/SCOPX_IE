# Step 44 — Media retention invariant hardening (draft)

Migration 012 adds two database-level protections on top of the retention foundation:

- A partial unique index permits only one open deletion request per hospital/media asset while its queue state is pending, processing, or failed. A new request may be created after the prior request is cancelled or completed.
- Legal-hold placement facts (hospital, media, reason, placing user, and placement time) cannot be edited. A hold can be released once only, and release requires actor, timestamp, and non-empty reason. Released hold records cannot be edited or deleted.

CI extends the synthetic PostgreSQL smoke test to prove that released legal holds cannot be changed or deleted and duplicate active deletion requests are rejected.

Migration 012 is excluded by default. It requires all three explicit flags: --include-rls-draft --include-retention-draft --include-retention-hardening-draft.

No worker, scheduler, storage-object deletion, tombstone execution, or media API mapping is enabled. This is a schema/invariant foundation only. Synthetic data only.

## Validation result

GitHub Actions run #390 passed on commit `75611b2a981f4c745db2debeaf732018bfffd1bc`. PostgreSQL 17 migrations 001-012 and the synthetic lifecycle smoke tests passed; API and Windows workstation builds also passed. This remains draft schema validation only, not production or legal approval.


## Step 45 — New-media retention deadline stamping (draft)

Migration 013 stamps `media_assets.retention_until` only when a new recording is inserted and that hospital has an explicitly enabled retention policy. The deadline is calculated from the row's `created_at` plus the configured `retention_days`. The database also assigns the creation timestamp, so callers cannot backdate a new asset. A missing or disabled policy forces the deadline to NULL, even if a caller tries to supply a value.

The stamp, creation timestamp, and hospital association are immutable after insertion. Changing a hospital policy does not recalculate existing media deadlines. No default retention period is introduced. Migration 013 is excluded by default and requires all four explicit flags: `--include-rls-draft --include-retention-draft --include-retention-hardening-draft --include-retention-stamping-draft`.

The PostgreSQL CI smoke test covers enabled, disabled, and missing policies; caller-supplied deadline override; immutability; and the guarantee that policy edits do not rewrite existing deadlines. No worker, scheduler, physical deletion, or media API mapping is enabled. Synthetic data only.


## Step 45 validation result

GitHub Actions run #45840925408 passed all jobs on commit `86b3c0934ac3d539746f4caf9b4b98a2a215e51a`. PostgreSQL 17 applied migrations 001-013 and passed the synthetic tenant/RLS and media lifecycle smoke tests, including enabled, disabled, and missing retention policies, server-controlled creation timestamps, caller-supplied deadline override, immutable stamps, and the legacy expired-media queue guard. API and Windows workstation builds, cryptography tests, and offline structural checks passed. This validates the draft implementation only; it is not clinical or production approval.
