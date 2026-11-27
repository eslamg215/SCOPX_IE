# Step 87 — Web demo keyboard and navigation accessibility

## Changes
- Added a skip-to-main-content link as the first keyboard focus target.
- Added visible keyboard focus indicators to primary navigation and actions.
- Main content can receive programmatic focus.
- Active navigation buttons expose `aria-current="page"`.
- Mobile navigation button exposes and updates `aria-expanded` and `aria-controls`.
- Added accessible labels to search and toolbar controls.

## Verification
- Browser acceptance test checks keyboard focus reaches the skip link first and Enter moves focus to main content.
- Structural validation checks accessibility attributes.

## Boundaries
This is a focused accessibility improvement, not a full WCAG conformance audit. Screen-reader, high-contrast, zoom, and assistive-technology testing remains necessary.
