# Step 69 — Medical media and endoscopy capture security review

## Scope and safety boundary

Reviewed the media API implementation, media repository, authenticated-encryption envelope, ClamAV adapter, retention/deletion draft migrations, host route registration, and workstation capture contracts.

This was a source and test review only. No patient data, clinical recording, physical capture device, live ClamAV daemon, storage volume, or production database was accessed. No media endpoint was enabled and no migration was applied.

## Confirmed safeguards

- The API host deliberately does **not** map `MapMediaEndpoints` and does not register the media repository. Media upload, listing, and download routes therefore remain unavailable through the running scaffold.
- Upload implementation caps an individual file at 50 MiB. Kestrel request-body and multipart limits are 60 MiB, leaving bounded multipart overhead.
- File type is derived from an extension plus content-signature checks for JPEG, PNG, WebP, MP4, and WebM; client MIME headers are not used as the trust source.
- Uploads fail closed when malware scanning is unavailable or reports a detection. The ClamAV adapter uses bounded INSTREAM chunks, a bounded response, and a timeout.
- Accepted media is encrypted using AES-256-GCM with a fresh nonce and authentication tag. Associated data binds the ciphertext to the hospital, media ID, and key ID. Key material is required from external configuration and is not stored in the media database row.
- Downloads decrypt and verify the plaintext length and SHA-256 digest before returning the file.
- Storage keys are generated from server-side identifiers and resolved beneath the configured root. New files use create-new semantics; failed metadata writes trigger object cleanup.
- Media repository operations establish transaction-local hospital context and write upload, read-attempt, list, and verified-download audit events in their database transactions.
- Download authorization is narrower than metadata visibility: auditors can list metadata but are not included in the download role set.
- Retention and deletion migrations are draft-only. No worker, scheduler, automatic queue execution, object deletion, or metadata tombstoning is enabled.
- Workstation capture code currently provides contracts and an explicitly non-video simulator. USB, HDMI, and network transport enum values do not constitute implemented or validated hardware drivers.

## Step 69 regression coverage added

- Added synthetic file-signature tests for JPEG, PNG, WebP, MP4, and WebM.
- Added negative tests for extension/signature mismatch and unsupported extensions.
- Extended offline structural checks for host request limits, buffer cleanup on upload, create-new storage writes, transactional audit integration, tenant context, and the simulator-only capture boundary.

## Outstanding risks before any clinical activation

1. **Memory buffering:** the current upload and download implementation buffers full media content in memory. It is capped at 50 MiB per file, but it is not a scalable recording pipeline. A future implementation needs bounded streaming, concurrency limits, cancellation-safe temporary-object handling, and load testing.
2. **Download plaintext lifetime:** the current download path returns a plaintext byte array through the framework file result. Explicit zeroing after the response completes is not established. This needs a response-lifetime-safe streaming design before media routes are mapped.
3. **Storage operations:** filesystem ACLs, dedicated service identity, volume encryption, quotas, backup encryption, restore drills, object versioning, and orphan reconciliation have not been production-verified.
4. **Key management:** an external secret provider/KMS or HSM, rotation runbook, recovery escrow, and verified bulk re-encryption are outstanding. Old keys must not be removed while any object still depends on them.
5. **Malware operations:** CI uses a local protocol fixture, not a real ClamAV service. A real scanner must be isolated on a private network, monitored, patched, and configured with a stream limit compatible with the application cap.
6. **Capture hardware:** no real USB/HDMI/network capture driver, codec pipeline, device reconnect behavior, timestamp synchronization, dropped-frame handling, or clinical video quality has been validated.
7. **Governance:** each hospital must approve its retention period, legal-hold process, deletion authorization, and applicable legal requirements. The existing deletion-request workflow remains a schema draft and is not a deletion capability.
8. **Integration testing:** no end-to-end test currently exercises a real media endpoint against PostgreSQL, durable storage, and a real scanner. The API remains unmapped pending those controls and independent review.

## Outcome

The protected-media foundation has meaningful cryptographic, signature-validation, malware-scanning, tenant-scoping, and audit safeguards, but it is **not a production recording service**. The safest current state is to keep media routes disabled and use synthetic fixtures only. No clinical readiness or real-device compatibility is claimed.

## Next step

Step 70 should review the workstation capture-to-recording workflow boundary and define a safe, non-clinical adapter lifecycle and recording state machine before any hardware driver or clinical media route is connected.
