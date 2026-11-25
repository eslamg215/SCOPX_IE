# Step 21 — User Repository (development foundation)

## Added
- `SCOPX.Infrastructure` class library with Npgsql provider.
- `UserRecord` limited to identity/authentication fields (no patient data or tenant membership).
- `IUserRepository` contract and PostgreSQL implementation.
- Email lookup normalizes to uppercase to match the existing `email_normalized` convention.
- All query values are parameterized. The only SQL interpolation is the fixed column list.
- Login success clears the counter and lockout and updates timestamps; failure persistence writes the supplied count and lockout.

## Review notes / known risks
- This is a repository primitive, not a login service. It is not registered in the API and has no endpoint exposure.
- Failed-count increments are not atomic here; the future authentication service must use a single atomic DB update or transaction with row locking. Do not call this method from concurrent login requests until Step 22 replaces/guards the flow.
- Connection-string validation, pooling tuning, secret storage, integration tests, migrations, and .NET compilation remain unverified in this environment.
- Do not use real patient information or deploy clinically.
