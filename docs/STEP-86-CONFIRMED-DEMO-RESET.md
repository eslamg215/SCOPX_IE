# Step 86 — Confirmed demo data reset

## Changes
- Added a clearly labeled reset control to the demo toolbar.
- Reset requires an explicit browser confirmation.
- The reset restores only the bundled synthetic seed data.
- If local storage persistence fails, the last persisted dataset is retained and the reset is not reported as successful.

## Verification
- Browser acceptance test confirms that a user-created synthetic record disappears and seeded demo data returns after confirmed reset.
- Structural checks verify the reset control, confirmation, and seed restore behavior.

## Boundaries
This action applies only to the offline demo's local browser storage. It does not touch API databases, hospital tenants, production data, or clinical records.
