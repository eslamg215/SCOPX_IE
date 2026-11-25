# Step 28 — Media Storage scaffold

## API
- `GET /api/v1/media/procedure/{procedureId}` — list metadata for a procedure (hospital scoped).
- `POST /api/v1/media/upload/{procedureId}` — multipart upload; accepts the first file field, maximum 50 MiB.
- `GET /api/v1/media/{id}/download` — authenticated download, range support for video.

## Storage behavior
- Storage root is configured by `MediaStorage:Root`; fallback is `media-store` under the API application's base directory.
- Files are saved under generated `{hospitalId}/{mediaId}{extension}` keys. Original filenames are stored as display metadata only.
- Allowed formats/extensions: `.jpg`, `.jpeg`, `.png`, `.webp`, `.mp4`, `.webm`. Content-type is assigned from the extension whitelist; client MIME is not trusted.
- Metadata records media type, file size, and SHA-256. DB insertion failure removes the saved file.
- Upload roles: hospital_admin, doctor, endoscopy_technician. Read roles: hospital_admin, doctor, nurse, auditor, endoscopy_technician.

## Limitations / safety
- Local filesystem only. Do not expose the storage root as a static web directory.
- Encryption at rest, malware scanning, content sniffing beyond extension allowlist, audit integration, retention controls, backup/restore verification, quotas, runtime integration tests, and operational security review remain pending.
- The environment has no .NET SDK/PostgreSQL, so runtime compile/API/database testing has not been performed. Synthetic test data only; not clinically validated.
