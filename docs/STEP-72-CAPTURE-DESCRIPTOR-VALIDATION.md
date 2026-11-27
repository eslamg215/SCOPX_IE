# Step 72 — Capture-device descriptor validation

## Scope
Capture adapter descriptors are external metadata. This step adds a pure policy to reject malformed or misleading descriptor fields before display or downstream use.

## Validation rules
- Device ID and display name must be non-empty, trimmed, bounded, and free of control characters.
- Optional manufacturer, model, and compatibility note fields have explicit length bounds and reject control characters.
- Transport and state enum values must be defined.
- Simulator transport requires a `simulator://` identifier and an explicit “No Video” display label.
- Non-simulator transports cannot claim a simulator identifier.
- Failure returns a generic reason code rather than reflecting untrusted metadata.

## Tests
Synthetic regression cases cover valid USB and simulator descriptors, blank names, control characters, unknown transport values, simulator identity spoofing, missing simulator label, and oversized compatibility notes.

## Safety boundaries
No physical device enumeration, USB/HDMI/network connection, frame capture, file creation, patient data, or clinical workflow was involved. This policy does not establish hardware compatibility or clinical readiness.
