# Step 7 — Identity implementation checklist

- [ ] Add ASP.NET Core Identity packages and PostgreSQL provider versions compatible with .NET 10.
- [ ] Choose single canonical user ID/table model (Identity entity + domain mapping).
- [ ] Generate and review EF Core migration; don't hand-edit generated identity schema without review.
- [ ] Implement secure bootstrap flow for first Platform Super Admin.
- [ ] Configure lockout, password policy, MFA, reset flow, and generic responses.
- [ ] Configure cookie/OIDC options and CSRF protection.
- [ ] Implement role and hospital membership authorization policies.
- [ ] Add audit events without credentials or patient details.
- [ ] Add automated authentication, authorization, and tenant isolation tests.
- [ ] Run `dotnet restore`, `dotnet build`, and `dotnet test` on a Windows/Linux dev machine with .NET 10 SDK.
