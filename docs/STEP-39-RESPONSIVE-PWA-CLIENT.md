# Step 39 — Responsive Web/PWA Client Prototype

## Objective
Provide a single responsive client shell usable from laptop browsers and Android/iOS phone browsers, avoiding two separate UI codebases for the first demonstration.

## Delivered
- `src/SCOPX.Web/` static HTML/CSS/JavaScript responsive client (Arabic RTL UI).
- Dashboard, patients, procedures, reports views.
- Local demo-only patient creation and procedure status transitions (scheduled -> in progress -> completed).
- Draft report preview generation when demo procedure completes.
- PWA manifest and service worker for app-like installation/offline shell.
- Demo-only seeded synthetic records.

## Data safety / limitations
- This is a client-side prototype. No API/backend/database connection is made.
- Records are stored in browser Local Storage on that device and are not encrypted or synchronized.
- Use synthetic data only. Never enter real patient data or clinical content.
- This PWA shell is not a medical device, does not capture video, and does not provide diagnosis or AI analysis.
- PWA installability requires serving the directory over HTTPS (or localhost for development); opening `index.html` directly does not enable service-worker install.

## Run locally
- For local desktop: from `src/SCOPX.Web`, run `python3 -m http.server 8080` and visit `http://localhost:8080`.
- For Android phone on the same Wi-Fi, serve from the laptop's LAN IP. Android PWA/service worker installation generally requires HTTPS; use a secure tunnel or HTTPS dev server for installability. Basic browser preview over local LAN may work but service-worker/PWA install may be blocked.

## Acceptance checks
- Responsive layout adapts to narrow phone and desktop widths.
- Navigation switches views.
- Synthetic patient can be created, then procedure scheduled.
- Procedure status transitions are guarded in demo UI.
- Report preview generated for completed demo procedure.
- Local data persistence and shell cache are demo-only.
