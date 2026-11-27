# Step 74 — Validated capture-adapter boundary

## Scope
Added a validating decorator around the existing capture-provider interface. This is a safe boundary for future device adapters, not a physical capture implementation.

## Behavior
- Validates the adapter descriptor at construction.
- Validates returned status enum, timestamp, and optional message bounds.
- Validates capture start options before forwarding to the underlying adapter.
- Forwards cancellation tokens to the adapter.
- Rejects operations after disposal and disposes the underlying adapter at most once.

## Tests
The portable regression suite wraps only `SimulatorCaptureAdapter` and verifies descriptor access, valid status, rejection of unsupported pixel format before forwarding, accepted start/stop, and rejection after disposal.

## Safety boundaries
No physical device enumeration, hardware connection, video frames, media files, patient data, or clinical workflow was involved. The decorator does not map or enable media API routes.
