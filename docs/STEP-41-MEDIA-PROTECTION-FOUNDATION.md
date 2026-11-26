# Step 41 — Protected medical-media foundation

## Implemented in the security branch

- Uploads are capped at 50 MiB before buffering.
- File signatures are checked for JPEG, PNG, WebP, MP4, and WebM. The original filename and request MIME header are not trusted to establish the content type.
- A ClamAV INSTREAM adapter is registered. It streams bounded 64 KiB chunks to a configured daemon, enforces a 50 MiB limit and timeout, accepts only `OK`, rejects `FOUND`, and fails closed for missing configuration, daemon errors, malformed responses, and timeouts. CI tests the protocol with a local fake daemon; a real ClamAV deployment has not been tested.
- Accepted content is encrypted before it is written to the configured storage root using AES-256-GCM, a fresh 96-bit nonce, a 128-bit authentication tag, and associated data binding the hospital and media identifiers.
- The encryption key is read from `MediaStorage:EncryptionKeyBase64` and must decode to exactly 32 bytes. It is not stored in PostgreSQL or source control.
- Downloads decrypt the envelope and verify the plaintext byte count and SHA-256 digest before returning bytes.
- Storage paths are generated from server-side hospital/media identifiers and checked to remain beneath the configured root.
- The API host still does not map media routes. This code is a protected foundation, not an enabled clinical media service.

## Not yet implemented or verified

1. A production malware scanner adapter (for example, an isolated ClamAV service), including timeout, size, failure, and signature-update tests.
2. Key management through a managed secret store/KMS, key identifiers, rotation, backup, and recovery procedures. The current envelope is versioned as SCX1 but does not implement rotation.
3. Storage-volume encryption, filesystem ACLs, backup encryption, retention/deletion workflows, legal holds, and restore drills.
4. Streaming/chunked authenticated encryption for large recordings. The current bounded 50 MiB upload path buffers the file in memory and is not a substitute for a production video pipeline.
5. Integration tests for tenant isolation, corrupted ciphertext, wrong-key handling, scanner failure, storage traversal, and audit completeness.
6. Independent security review and clinical validation.

## Configuration

Configure a real ClamAV daemon on a private, isolated network using `MediaScanning:ClamAvHost`, `MediaScanning:ClamAvPort`, and optionally `MediaScanning:TimeoutSeconds` (default 30, bounded to 1–120 seconds). The ClamAV TCP protocol used here is not encrypted; do not expose the daemon publicly or across an untrusted network. Ensure the daemon's `StreamMaxLength` is at least the application upload cap and keep its signatures updated.

When protected key management is available, provide the key through protected deployment configuration under `MediaStorage:EncryptionKeyBase64`. A local development key can be generated with:

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

Do not commit the value, paste it into chat, or reuse it between environments. Do not configure a real clinical environment using this development foundation.

**Status: protected-media foundation added; media API remains disabled. Synthetic data only.**
