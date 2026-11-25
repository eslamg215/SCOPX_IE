# Step 19 — Authentication Persistence Schema & Lockout Policy

## Implemented in this package
- Migration `005_user_authentication.sql` adds nullable `password_hash`, consecutive failure counter, lockout timestamp, and last successful login timestamp to the existing users table.
- A non-negative check constraint protects the failure counter.
- Partial index supports lockout-related lookups.
- `LoginLockoutPolicy` provides deterministic escalating lockout durations: below 5 failures no lockout; 5–9 failures 15 minutes; 10–14 failures 1 hour; 15+ failures 24 hours.

## Not yet implemented
- Login endpoint, registration, password reset, session/token issuing, atomic failure counter updates, rate-limited IP/username buckets, and recovery flow.
- DB connection, migration 005 execution, and .NET build/test have not been run in this environment.

## Security implementation requirements
- Use ASP.NET Core `IPasswordHasher<TUser>` and verify only hashes; no plaintext passwords.
- Update failed counters and lockout timestamps atomically to avoid concurrent request bypass.
- Return generic authentication failure messages; do not reveal whether an email exists or is locked.
- Keep registration disabled until a trusted hospital bootstrap/invitation workflow exists.
- Lockout must not become a denial-of-service vector; support admin recovery and avoid indefinite locks.
- Do not store real patient information or use this scaffold in clinical production.
