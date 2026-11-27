# Step 43 — Medical-media retention lifecycle (draft)

## Added in migration 011

- Per-hospital retention policy record with an explicit enable switch. No retention duration is assumed or silently selected; an enabled policy requires a reviewed policy reference and a duration.
- Per-recording legal holds with the identity and timestamp of the person placing the hold, plus a controlled release record and reason.
- Tenant-scoped deletion queue with not-before time, retry count, status, and bounded error code.
- Database guard rejects queue creation or transition into pending/processing when a legal hold is active, when no retention deadline exists, or before the retention deadline has elapsed.
- Append-only deletion event history with tenant RLS and database triggers rejecting UPDATE, DELETE, and TRUNCATE.
- Runtime grants are limited to the operations required by the schema foundation; deletion event rows remain insert/read only.

## Explicitly not implemented

- No scheduled worker, background service, API route, or automatic deletion is enabled.
- No storage object is deleted by this migration. A future worker must verify authorization, re-check legal holds and retention eligibility immediately before deletion, use a lease/idempotency design, record each attempt, and tombstone metadata only after verified object deletion.
- No two-person approval workflow has been implemented. Whether it is required must be decided by the institution's policy and applicable law.
- No retention duration is prescribed by SCOPX. Each hospital must configure a reviewed period with its legal/compliance owner.
- Media endpoints remain unmapped by the API host. Do not use real patient data or recordings.

## Validation

CI applies this draft only to a disposable PostgreSQL 17 database, runs tenant-isolation checks for retention policies, legal holds, and deletion queue rows, verifies that active legal holds block queueing, verifies that an eligible synthetic item can be queued after release, and confirms deletion events are append-only. These checks do not establish production security or legal compliance.

The migration runner excludes migration 011 by default. To include it in a disposable, reviewed database, both --include-rls-draft and --include-retention-draft are required; include --include-retention-hardening-draft to apply migration 012.

**Status: schema foundation only; no deletion worker or media API activation. Synthetic data only.**