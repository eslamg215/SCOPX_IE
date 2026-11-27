# Step 84 — Demo image upload validation

## Changes
- Demo uploads are limited to non-empty images up to 2 MiB.
- The client checks binary signatures for PNG, JPEG, GIF, and WebP.
- Browser-supplied MIME type and file extension are not trusted.
- SVG and unsupported formats are rejected.
- Backup import accepts only bounded base64 data URLs for the same raster image formats.

## Verification
- Added structural checks for signature validation and restricted backup media data URLs.
- Existing browser acceptance tests continue to validate the media workflow when available.

## Boundaries
This is validation for synthetic local demo images only. It is not a malware scanner, medical media ingestion service, or replacement for the server-side scanning/encryption pipeline. Do not upload clinical images.
