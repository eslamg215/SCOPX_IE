# Step 11 — Windows endoscopy workstation UI specification

Status: **Screen specification; desktop client not yet built.**

## Product layout
- Left navigation: Patients, Schedule, Procedure Room, Reports, Media, Settings.
- Header: hospital name (verified scope), signed-in user/role, device/capture status, network/sync indicator.
- Main canvas: page content; keyboard-first navigation; high-contrast, readable typography; large controls in procedure room.
- Persistent warning banner area: device state, unsaved changes, sync failure, license restrictions.

## Screens
### Patient list
Search by patient code/name with server paging; add patient; filter archived; no bulk export by default. List view reveals only minimum demographics.
### Patient profile
Demographics and timeline tabs; procedures; attachments/media; reports. Clinical sections gated by permissions.
### Schedule
Day/week view; procedure type, room, status. Conflicts and incomplete prep visible. No patient detail in OS notifications.
### Procedure room
Large preview; recording indicator/timer; capture still; start/stop; connection state; current procedure details. Device controls are hidden unless verified adapter capability and permission.
### Report composer
Structured sections (indication, findings, impression, recommendations); autosave draft indicator; version history; AI suggestions shown in separate labeled panel; clinician edit and explicit signing action.
### Media review
Thumbnail grid; still/video type and timestamps; selected media viewer; authorized download/export only; clear incomplete recording markers.
### Settings
User/hospital settings, device/capture configuration, storage status, network diagnostics, license information. Admin controls permission-gated.

## Usability and accessibility requirements
- Use large touch targets (minimum 44–48 logical px where practical), avoid color-only state cues, keyboard shortcuts documented, confirm irreversible actions, and visible unsaved/failed-save state.
- Procedure-room mode minimizes distraction and uses a deliberate capture state machine.
- No PHI in taskbar previews, lock screen notifications, telemetry, or diagnostic logs.
- UI must support display scaling, 16:9 and common workstation resolutions, and high-contrast mode.

## Acceptance criteria for the first desktop prototype
- User can sign in (after identity implementation), select authorized hospital, search/create synthetic patient, create a scheduled procedure, and navigate a mock procedure room.
- Capture panel clearly reports `demo/no hardware` until a capture source is actually verified.
- Draft report flow works with synthetic content only; signing is disabled until policy and audit implementation is verified.
- Errors are actionable, safe, and do not show internal details.
- All role restrictions are tested on server-side API; hiding UI buttons alone is insufficient.
