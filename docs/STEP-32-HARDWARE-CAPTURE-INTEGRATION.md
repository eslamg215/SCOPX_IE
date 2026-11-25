# Step 32 — Hardware capture integration foundation

## Implemented in this starter
- Transport enum for USB, HDMI capture-card, network stream, and simulator sources.
- Capture-device descriptor/status/start-options contracts.
- `IDeviceCaptureAdapter` boundary so drivers do not leak into UI code.
- Test-only `SimulatorCaptureAdapter` for state transition checks. It produces **no video**.
- Basic bounds validation on simulator capture settings.

## Not implemented / not verified
- No DirectShow, Media Foundation, UVC, vendor SDK, or HDMI capture driver is bundled.
- Olympus CV-180 has not been tested or declared compatible.
- No image/frame ingestion, recording, preview, timestamps, or dropped-frame measurements.
- No capture card or hardware is available in this build environment.

## Integration test checklist before claiming compatibility
1. Record exact CV-180 output connectors, resolution, refresh rate, and signal format from vendor documentation/device label.
2. Confirm the capture card is supported on the target Windows version and has a vendor driver.
3. Validate signal acquisition without patient identifiers and in a non-clinical test setup.
4. Measure frame rate, resolution, latency, dropped frames, reconnect behavior, and long-run stability.
5. Test device removal, driver failure, app shutdown, and recovery.
6. Ensure media file metadata, access control, and audit records are attached before any PHI workflow.
7. Obtain clinical/biomedical engineering and regulatory review before clinical use.

**Safety:** Development scaffold only; no patient data and no clinical use. Compatibility requires hardware and vendor-driver testing.
