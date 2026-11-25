# Step 31 — Android Companion (early native shell)

Added `src/SCOPX.Android`, a Kotlin Android application scaffold. The first screen accepts API base URL, email, and password. It calls the existing `POST /api/v1/auth/login`, extracts the bearer token, and requests `GET /api/v1/hospitals/mine`. Network work is moved to a single-thread executor; the UI is updated on the main thread. Password is cleared after a successful login; token is memory-only and cleared when the Activity is destroyed.

Security defaults: Internet permission only; cleartext traffic disabled; app backup disabled; HTTPS expected. This is a preview scaffold, not production security: no secure token storage/refresh, pinning, device-management policy, or audit integration.

Build environment requirements: Android Studio, SDK 35, Gradle 8.9+, AGP 8.7.3, Kotlin 2.0.21. Gradle wrapper omitted. Android build/device testing unavailable here; inspect, sync, compile, and test on emulator before use. No real patient data.
