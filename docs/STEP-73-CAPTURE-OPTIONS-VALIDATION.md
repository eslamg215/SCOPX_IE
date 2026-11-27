# Step 73 — Capture stream-options validation

## Scope
This step validates capture request metadata before a future adapter can negotiate a stream or allocate frame buffers.

## Policy
- Width: 1–7680 pixels.
- Height: 1–4320 pixels.
- Frame rate: 1–120 fps.
- Pixel-format allowlist: MJPEG, YUY2, NV12 (case-insensitive; whitespace/control characters and names over 16 characters rejected).
- The no-video simulator uses the same policy and remains inactive when options are rejected.

## Regression coverage
Accepted 1080p MJPEG and upper-bound NV12 profiles; rejected over-limit width, height, and frame rate; rejected unsupported, padded, and oversized pixel-format values; verified rejected settings do not move the simulator into Capturing.

## Safety boundaries
These values are software request bounds only. They do not assert hardware support, codec compatibility, actual frame timing, or clinical readiness. No physical device, video frames, media files, or patient data were used.
