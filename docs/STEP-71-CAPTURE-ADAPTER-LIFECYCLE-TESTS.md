# Step 71 — Deterministic capture-adapter lifecycle tests

## Scope
This step verifies the existing workstation capture-provider boundary using only the in-memory `SimulatorCaptureAdapter`. The simulator is explicitly a no-video test source.

## Changes
- Linked `Devices/DeviceCaptureContracts.cs` into the portable `SCOPX.SecurityTests` project.
- Added cancellation checks to simulator status, start, and stop operations.
- The simulator now rejects start/stop after disposal, rejects starts unless ready, and preserves ready state when a start request is cancelled or invalid.
- Added regression coverage for initial ready status, successful start, active status, duplicate start rejection, stop, invalid dimensions, cancellation, and disposal.

## Safety boundaries
- No USB, HDMI capture card, network stream, camera, or endoscope was opened.
- No frames, files, media records, or patient data were created or accessed.
- The simulator does not generate video and is not evidence of hardware compatibility or clinical readiness.
- Production media routes remain unmapped/disabled.

## Remaining work
Real hardware drivers and target-device validation; frame timestamp, dropped-frame, codec and resolution checks; secure streaming/storage integration; end-to-end scanner and database validation; and clinical/regulatory validation remain separate work.
