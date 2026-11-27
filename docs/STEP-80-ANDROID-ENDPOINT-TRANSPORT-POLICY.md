# Step 80 — Android API endpoint transport policy

## Scope
Harden the optional Android sign-in endpoint input before any network request is attempted.

## Policy
- HTTPS is mandatory; cleartext HTTP is rejected.
- Embedded user-info credentials, URL paths, query strings, fragments, malformed hosts, invalid ports, and surrounding whitespace are rejected.
- The accepted input is a base origin (host with optional port), with an optional trailing slash.

## Tests
JUnit tests cover accepted HTTPS origins and rejection of HTTP, embedded credentials, paths, queries, fragments, malformed hosts/ports, and whitespace. CI runs `testDebugUnitTest` before building the debug APK.

## Safety boundaries
This validates the URL supplied to the optional login scaffold only. It does not enable clinical endpoints, add patient data workflows, or make the mobile app production-ready.
