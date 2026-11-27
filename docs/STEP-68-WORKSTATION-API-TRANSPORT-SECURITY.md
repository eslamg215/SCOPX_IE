# Step 68 — Windows workstation API transport security

## Finding
The development workstation accepted arbitrary HTTP or HTTPS API base URLs. Because the application will eventually handle authentication tokens and sensitive operational data, permitting remote plaintext HTTP would create a credential and data interception risk once authenticated workflows are introduced.

## Changes
- Added `ApiEndpointPolicy` to require HTTPS for non-loopback API hosts.
- Kept plain HTTP available only for loopback development (`localhost` / loopback IPs), preserving the current local developer workflow.
- Rejects API base URIs containing user-info credentials, query strings, or fragments.
- The workstation validates the base URI before issuing the `/health/ready` request.
- Added offline structural checks to prevent regression of the HTTPS/loopback policy and its use by the workstation form.

## Boundaries
- This protects transport selection in the workstation UI; it does not configure server TLS, certificate pinning, mutual TLS, or production ingress.
- The workstation still has no login/token storage or patient/recording workflow. No credentials or clinical data are sent by this change.
- Local loopback HTTP is intentionally retained for development only and must not be used for remote hosts.
- No database, account, session, or patient data was changed.

## Verification
- Source and structural checks are recorded; the GitHub Actions run for the resulting commit is pending.
- Windows UI runtime behavior still requires a Windows workstation smoke test.
- This remains a development scaffold, not production-ready or clinically validated.
