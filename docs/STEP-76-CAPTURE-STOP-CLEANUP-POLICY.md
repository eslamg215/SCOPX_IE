# Step 76 — Capture stop and cleanup-state policy

## Scope
Harden the validated adapter's stop operation so it behaves predictably across ready, capturing, faulted, and disconnected states.

## Policy
- Ready: stop is an idempotent no-op.
- Capturing: forward stop to the provider.
- Faulted: forward stop as a best-effort cleanup attempt.
- Disconnected: reject without forwarding.
- Invalid status metadata is rejected by the existing status policy.

## Tests
- Simulator stop while Capturing returns to Ready.
- Repeated stop while Ready does not fail.
- Faulted fake provider receives a cleanup stop, but start remains blocked.
- Disconnected fake provider is not asked to stop.

## Safety boundaries
No physical device or recording was used. Real device stop, buffer drain, hardware release, and reconnect behavior remain vendor-specific and unvalidated.
