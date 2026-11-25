# Step 18 — Authentication Foundation (v2.9)\n

## Implemented in this scaffold
- Added a minimal password-hashing subject (`AuthUser`).
- Added a password policy helper: 12–128 characters, lower/upper/digit; special characters are optional to avoid forcing predictable patterns.
- Added a wrapper around ASP.NET Core Identity password hashing and verification.
- Hashing helper rejects passwords outside policy; verification rejects oversized/empty inputs.

## Not implemented / must not be represented as complete authentication
- No user database/store, registration, login/logout, token/session issuance, password reset, MFA, or lockout persistence.
- No rate-limit partitioning by account/IP, credential stuffing controls, session revocation, or audit events for auth.
- No hospital-role membership checks or tenant binding.
- Helpers are not yet registered in DI or exposed by endpoints.

## Security next steps
1. Implement persistent user store and migration for auth records.
2. Add login with uniform failure responses and database-backed lockout counters.
3. Bind authenticated identity to tenant membership; reject client-supplied tenant IDs.
4. Add session/cookie or token flow with revocation and CSRF protection as appropriate.
5. Audit authentication success/failure and administrative actions without logging secrets.
6. Add automated tests for policy, hash verification, lockout, cross-tenant access and role escalation.

Static source review only. .NET SDK is not available in this environment, so build and unit tests were not run.
