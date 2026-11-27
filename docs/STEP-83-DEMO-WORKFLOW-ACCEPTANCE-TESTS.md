# Step 83 — Offline demo workflow acceptance tests

## Added browser coverage
- Create a synthetic patient and verify it appears in the UI.
- Create a synthetic procedure, transition it through in-progress to completed, and verify report preview generation.
- Upload a synthetic PNG fixture and verify it appears in the local media gallery.
- Export the local dataset and inspect the downloaded JSON to verify synthetic patient and media records are included.
- Reload the browser and verify synthetic patient and media records persist.
- Retain mobile viewport overflow and navigation checks.

## Verification boundary
These are automated browser tests for the offline local-storage demo. They do not validate backend persistence, database connectivity, medical correctness, clinical media capture, or patient-data security for production use.
