# Step 10 — Endoscopy device and capture adapter architecture

Status: **Architecture/design only. Hardware compatibility is not confirmed.**

## Objective
Keep device control and video ingestion outside the clinical UI and core patient/procedure services. A vendor/device adapter exposes capabilities through a stable interface so additional vendors can be added without rewriting workflows.

## Adapter boundaries
1. `IDeviceDiscovery` — enumerate available devices and report identifiers/capabilities without opening patient records.
2. `IDeviceSession` — connect/disconnect, health state, safe reconnect; no implicit device setting changes.
3. `ICaptureSource` — start/stop stream or recording, frame/sample delivery, timestamps, metadata.
4. `IStillCapture` — trigger still capture only when device or supported capture-card capability exists.
5. `IDeviceControl` — explicit, allowlisted control actions; disabled until vendor API/SDK and permissions are confirmed.
6. `IMediaStorage` — write encrypted/private media, checksum, durable acknowledgement, cleanup of failed partial files.
7. `ICaptureHealth` — dropped frames, source disconnect, clock drift, capture errors; show warning, never silently claim recording.

## First device investigation target
Olympus CV-180 is a requested initial compatibility target. Compatibility must be validated against the actual unit, region/version, available output ports, capture hardware, vendor documentation/SDK, and hospital network/IT rules. Do not claim direct control until the physical device has been tested.

## Capture approach to evaluate (order)
- Preferred: documented vendor SDK/API with permitted integration.
- Alternative: capture card using a verified video output and supported format; device controls may remain separate/manual.
- Avoid: undocumented control protocols, reverse engineering protected interfaces, or bypassing hospital/device safeguards.

## Capture requirements
- Store source signal/codec/resolution/frame rate as observed, not assumed.
- Verify time synchronization and timestamp semantics.
- Recording indicator visible in the workstation UI; state machine: `idle -> preparing -> recording -> stopping -> finalized` plus `error`.
- Finalized media only is attached to the procedure. Failed/partial recordings are quarantined and marked incomplete until reviewed.
- Compute SHA-256 on finalization; record capture source/device ID, software version, capture time, operator, and failure flags.
- Recording must be tested for sustained duration, disk-full behavior, disconnect/reconnect, power loss, and recovery. No silent file corruption.
- Do not store media in public web folders. Access uses authenticated, tenant-checked, short-lived retrieval authorization.

## UI states and user feedback
- Device disconnected: explicit red/amber status; no start action.
- Connected but no signal: differentiate from disconnected device.
- Recording: prominent timer and active state.
- Recording error: prominent warning, preserve recoverable file, show operator action.
- Finalizing: do not mark media as ready until container/index and checksum finalized.
- Ready: preview available only to authorized users.

## Hardware test plan (must be executed on Windows with actual hardware)
1. Confirm output connector and signal format with vendor documentation and onsite unit.
2. Verify capture-card driver signature, compatibility, and supported formats.
3. Capture test signal (non-patient), verify preview, then record 10-minute sample.
4. Compare expected duration, frames, audio if applicable, playback, file metadata, and checksum.
5. Disconnect signal during recording and confirm recovery/warning behavior.
6. Simulate storage full and verify no false-success UI.
7. Reboot workstation and inspect finalized/recoverable recordings.
8. Test permissions, local encryption, access audit, retention, and deletion policy.
9. Have clinical engineering/IT approve before clinical deployment.

## Explicit non-goals for prototype
No endoscope remote controls, light-source controls, image enhancement settings, or AI interpretation actions are implemented or authorized by this design. Initial prototype can only show a connected capture source and safely record test footage after hardware verification.
