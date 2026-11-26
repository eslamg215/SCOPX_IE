# Step 44 — Media retention invariant hardening (draft)

Migration 012 adds two database-level protections on top of the retention foundation:

- A partial unique index permits only one open deletion request per hospital/media asset while its queue state is pending, processing, or failed. A new request may be created after the prior request is cancelled or completed.
- Legal-hold placement facts (hospital, media, reason, placing user, and placement time) cannot be edited. A hold can be released once only, and release requires actor, timestamp, and non-empty reason. Released hold records cannot be edited or deleted.

CI extends the synthetic PostgreSQL smoke test to prove that released legal holds cannot be changed or deleted and duplicate active deletion requests are rejected.

Migration 012 is excluded by default. It requires all three explicit flags: --include-rls-draft --include-retention-draft --include-retention-hardening-draft.

No worker, scheduler, storage-object deletion, tombstone execution, or media API mapping is enabled. This is a schema/invariant foundation only. Synthetic data only.