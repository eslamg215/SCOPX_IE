# Step 75 — Capture readiness and fault-state gate

## Scope
Prevent a capture start from being forwarded unless the adapter reports a valid Ready status.

## Changes
- `ValidatedCaptureAdapter.StartAsync` now obtains and validates current status before forwarding.
- Only `CaptureState.Ready` permits a start request.
- Duplicate start while Capturing, Faulted, Disconnected, or malformed status is rejected before provider invocation.

## Tests
- Simulator duplicate-start request is blocked while preserving Capturing state.
- A deterministic faulted fake provider reports Faulted; the decorator rejects Start and the fake confirms its Start method was never called.
- Status policy rejects unknown enum values, default timestamps, oversized messages, and control characters; a well-formed Faulted status remains readable for diagnostics.

## Safety boundaries
No hardware was connected, no real video was captured, and no clinical workflow was run. This does not implement automatic reconnect, device recovery, or hardware fault diagnosis.
