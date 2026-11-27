# Step 77 — Capture adapter state-matrix regression completion

## Scope
Complete the deterministic regression matrix for the validated adapter's state gates and resource lifetime.

## Added tests
- Disconnected provider refuses both start and stop; neither operation reaches the provider.
- Status timestamps more than one minute in the future are rejected.
- Faulted provider refuses start but receives a stop cleanup attempt.
- Repeated disposal of the wrapper disposes the underlying provider only once.

## Safety boundaries
All tests use synthetic fake adapters and the no-video simulator. No real device, video, storage, patient data, or clinical workflow is involved.
