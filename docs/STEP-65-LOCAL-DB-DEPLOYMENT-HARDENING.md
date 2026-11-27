# Step 65 — Local database deployment configuration hardening

## Change

Docker Compose no longer supplies the known weak fallback password `local_only_change_me` for PostgreSQL. It now requires `POSTGRES_PASSWORD` to be explicitly set before the database service can start.

The existing loopback-only port binding (`127.0.0.1:5432`) and named persistent volume are unchanged.

## Operator setup

1. Create the ignored local `.env` from `.env.example` if it does not exist.
2. Replace every placeholder with distinct local development credentials.
3. Keep `.env` out of source control; never paste its contents into chat or issue reports.
4. Start the database only after setting the required password.

The repository's current local setup already uses an untracked `.env` file. This change does not rotate or read any existing local password and does not touch the user's database volume.

## Automated guard

The offline structural validator now fails if Compose reintroduces the weak fallback and verifies that password interpolation is mandatory.

## Scope and remaining deployment gaps

- This is local-development Compose hardening, not a production deployment manifest.
- PostgreSQL image is still a major/minor tag rather than a digest-pinned image; supply-chain pinning and controlled patch updates remain deployment work.
- No API container, TLS ingress, managed secrets integration, backup automation, restore drill, or production network policy is delivered by this step.
- Keep the API on the least-privilege `scopx_runtime` database role; never use the database owner as the API identity.
- No migration was applied, no database was started, no password was generated/rotated, and no persistent data was changed.

Synthetic data only. SCOPX remains a development scaffold, not clinically validated or production-ready.
