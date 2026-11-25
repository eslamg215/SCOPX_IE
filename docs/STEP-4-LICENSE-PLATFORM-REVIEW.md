# Step 4 review — platform roles and licensing
**Status:** drafted and statically reviewed; PostgreSQL execution not yet verified.


## Included
- Separate platform role grants, distinct from hospital-scoped roles.
- Platform Super Admin and restricted Platform Support role seeds.
- Hospital licenses with plan, status, dates, seat/device limits.
- Feature entitlements table.
- License activation records with hashed activation tokens.
- Device binding records using hashed fingerprints.

## Security findings / requirements
- Do not store plaintext license keys or activation codes; only hashes are stored in this schema.
- The license key hash must be computed using an approved server-side secret/keyed hash strategy, not a plain unsalted hash of a low-entropy key.
- Platform Super Admin must be provisioned only through a controlled bootstrap process; never self-register or grant through regular UI/API.
- Device fingerprints can be sensitive and spoofable. Minimize data, document privacy, use server-side limits, and avoid treating fingerprint as sole authentication.
- Offline grace periods and clock rollback defenses are not designed yet; license validation must be server-authoritative and signed.
- The current schema allows multiple platform roles; application authorization must verify `roles.scope='platform'` before honoring platform grants.
- No API endpoints for licensing exist yet; this migration only defines storage.
- SQL was not executed against PostgreSQL in this workspace; test in disposable DB before adoption.
