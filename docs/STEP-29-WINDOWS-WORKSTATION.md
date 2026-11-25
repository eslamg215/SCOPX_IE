# Step 29 — Windows Workstation (early desktop shell)

## Added
- New `src/SCOPX.Workstation` WinForms desktop project targeting `net10.0-windows`.
- Dark navy/cyan SCOPX identity and four feature placeholders (Patients, Procedures, Reports, Media).
- Configurable API base URL field and a health-only connection check against `/health`.
- Explicit non-clinical / development warning.

## Not implemented yet
- Authentication/token lifecycle, hospital selection, patient/procedure/report/media screens.
- Olympus CV-180 device capture integration; compatibility remains unverified.
- Local secure configuration, installer, signing, automatic update, clinical workstation hardening.

## Run (requires Windows and .NET 10 SDK)
```powershell
cd src/SCOPX.Workstation
dotnet run
```
The API must be running separately for the health check. This project has not been compiled in this environment; only structural checks can be performed here. Do not use real patient data.
