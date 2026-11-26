# Step 41 — Protected medical-media foundation

## Implemented in the security branch

- Uploads are capped at 50 MiB before buffering.
- File signatures are checked for JPEG, PNG, WebP, MP4, and WebM. The original filename and request MIME header are not trusted to establish the content type.
- A ClamAV INSTREAM adapter is registered. It streams bounded 64 KiB chunks to a configured daemon, enforces a 50 MiB limit and timeout, accepts only `OK`, rejects `FOUND`, and fails closed for missing configuration, daemon errors, malformed responses, and timeouts. CI tests the protocol with a local fake daemon; a real ClamAV deployment has not been tested.
- Accepted content is encrypted before it is written to the configured storage root using AES-256-GCM, a fresh 96-bit nonce, a 128-bit authentication tag, and associated data binding the hospital, media identifier, and key ID. The SCX2 envelope carries its key ID.
- The active key is selected by `MediaStorage:ActiveKeyId`; key material is read from `MediaStorage:Keys:<key-id>` and must decode to exactly 32 bytes. The key ID is stored in the SCX2 envelope; key material is not stored in PostgreSQL or source control.
- The envelope supports keyring rotation: make a new key active while retaining older keys for decryption. Automated bulk re-encryption is not implemented, so do not remove an old key until every object encrypted with it has been re-encrypted and verified.
- Downloads decrypt the envelope and verify the plaintext byte count and SHA-256 digest before returning bytes.
- Storage paths are generated from server-side hospital/media identifiers and checked to remain beneath the configured root.
- The API host still does not map media routes. This code is a protected foundation, not an enabled clinical media service.

## Not yet implemented or verified

1. A production ClamAV deployment has not been tested. The TCP protocol is unencrypted and must remain on a private, isolated network.
2. The download endpoint now uses a separate care-team role policy: hospital administrators, doctors, nurses, and endoscopy technicians may download; auditors may inspect metadata but cannot download recordings. Every request still requires an active membership in the selected hospital.
3. Successful integrity-verified downloads append a transactional `media.downloaded` audit event. A request/read-attempt event is recorded separately, so failed transfers and successful verification are distinguishable.
4. Managed KMS/HSM integration, key rotation automation, bulk re-encryption, encrypted backups, filesystem ACL hardening, and recovery/restore drills.
5. Retention schedules, legal holds, two-person deletion approval where required, deletion queue, secure object deletion, and auditable tombstones. No automated deletion is currently enabled.
6. Streaming/chunked authenticated encryption for large recordings. The current bounded 50 MiB upload/download path buffers content in memory and is not a production video pipeline.
7. End-to-end integration tests against a real PostgreSQL + object-storage + malware-scanner deployment, including failed disk writes, interrupted transfers, and restore verification.
8. Independent security review and clinical validation.

The media API remains deliberately unmapped by the host until the complete storage, authorization, retention, scanner operations, and deployment controls are independently reviewed.
 
## Configuration

Configure a real ClamAV daemon on a private, isolated network using `MediaScanning:ClamAvHost`, `MediaScanning:ClamAvPort`, and optionally `MediaScanning:TimeoutSeconds` (default 30, bounded to 1–120 seconds). The ClamAV TCP protocol used here is not encrypted; do not expose the daemon publicly or across an untrusted network. Ensure the daemon's `StreamMaxLength` is at least the application upload cap and keep its signatures updated.

When protected key management is available, provide the key through protected deployment configuration under `MediaStorage:ActiveKeyId` and `MediaStorage:Keys:<key-id>`. A local development key can be generated with:

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

Set `MediaStorage__ActiveKeyId` to a non-secret key ID and `MediaStorage__Keys__<key-id>` to the generated Base64 value in protected configuration. Do not commit the value, paste it into chat, or reuse it between environments. Do not configure a real clinical environment using this development foundation.

**Status: protected-media foundation and care-team download policy implemented; API remains disabled. Retention/deletion and production storage operations are not implemented. Synthetic data only.**
