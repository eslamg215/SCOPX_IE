# Step 67 — Authentication lockout and password-policy review

## Scope
Reviewed the login endpoint, database-backed failed-login updates, lockout helper, password policy, and security regression suite.

## Confirmed behavior
- Login rejects blank or oversized email/password input before database lookup.
- Credential failures use a generic `invalid_credentials` response for unknown, disabled, locked, and incorrect-password cases.
- Login is subject to both the API policy and a stricter per-IP login policy (10 requests per minute); the general API policy is 120 requests per minute.
- Failed-login increments and lockout timestamps are updated atomically in PostgreSQL.
- Lockout escalation is consistent between the policy helper and SQL: attempts 5–9 => 15 minutes; 10–14 => 1 hour; 15+ => 24 hours.
- A successful login clears the failed counter and lockout timestamp.
- Password policy requires 12–128 characters, with lowercase, uppercase, and numeric characters; special characters are not mandatory.
- Session tokens remain high-entropy opaque values and only SHA-256 hashes are persisted.

## Step 67 changes
- Added regression tests for lockout escalation boundaries (4, 5, 9, 10, 14, 15 failures), exact expiry behavior, and password-policy boundaries.
- Added structural checks that compare the SQL lockout thresholds with the documented policy bands, preventing silent drift between application logic and database updates.

## Operational limitations
- Login throttling is partitioned by the direct socket peer. A production reverse-proxy deployment must configure and validate trusted forwarded headers before relying on client-IP partitioning.
- Per-account lockout is not a substitute for MFA, breached-password screening, credential-stuffing detection, or a reviewed account-recovery process.
- A global authentication security-event audit store remains unimplemented pending explicit retention and authorized-reader decisions (approval gate from Step 58).
- No changes were made to password hashes, account records, sessions, or database data.

## Verification boundaries
- Regression and structural tests are added to CI; final pass/fail is pending the workflow run for this commit.
- No production credentials, real identities, patient data, or clinical workflows were used.
- This is a development security review, not production approval or clinical validation.
