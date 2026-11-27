# Step 70 — Non-clinical recording lifecycle state machine

## Scope

Added a pure state-transition policy for the future workstation recording workflow. It defines and validates lifecycle transitions only. It does not connect a capture device, read frames, create media files, start a recording, or declare a file durable or clinically finalized.

## States

- `Idle`: no recording operation is active.
- `Preparing`: a future caller is expected to validate device readiness and storage availability.
- `Recording`: a future capture pipeline would be active.
- `Stopping`: a future caller is expected to flush and close the media container.
- `Finalized`: a future caller may proceed only after durable-write, container/index, checksum, and metadata verification are implemented.
- `Error`: a failure occurred; explicit reset to `Idle` is required before another attempt.

## Allowed transitions

- Idle → Preparing
- Preparing → Recording, Idle (cancel), or Error
- Recording → Stopping or Error
- Stopping → Finalized or Error
- Finalized → Preparing (next recording session)
- Error → Idle (explicit reset)

All other transitions are rejected. In particular, Idle → Recording, Recording → Finalized, and Error → Finalized are invalid.

## Regression coverage

Portable regression tests cover allowed transitions, rejected transitions, and exception behavior for invalid transitions. The policy source is linked into the cross-platform security-test project so it can be tested without requiring Windows hardware or the Windows desktop runtime.

## Safety boundary

This is not a recording implementation and does not change the API's disabled media-route state. No real device, patient information, clinical recording, media storage, or database was accessed. A future capture implementation must separately provide cancellation-safe device ownership, bounded streaming, temporary-file quarantine, durable finalization, checksums, crash recovery, disk-full behavior, dropped-frame reporting, and audit integration. A successful state transition alone must never be treated as proof that media exists or is safe to attach to a procedure.

## Next step

Step 71 should specify and test the capture adapter lifecycle contract (connect, readiness, start, stop, disconnect, cancellation, fault recovery) using only deterministic fake adapters before any hardware driver is implemented.
