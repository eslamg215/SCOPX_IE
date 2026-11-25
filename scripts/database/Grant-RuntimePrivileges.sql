-- SCOPX.I.E least-privilege grant template. Review before applying.
-- Apply as schema/database owner in a disposable environment first.
-- Create the login separately using a secure password prompt/secret manager.
-- Example role attributes: LOGIN, NOSUPERUSER, NOCREATEDB, NOCREATEROLE,
-- NOINHERIT; never grant ownership, BYPASSRLS, or migration privileges.

-- Restrict PUBLIC on the project tables only (do not globally alter unrelated DB objects).
REVOKE ALL ON patients, procedures, media_assets, procedure_reports,
  hospital_memberships, membership_roles, audit_events, users, hospitals, roles,
  platform_user_roles, hospital_licenses, license_features,
  license_activations, licensed_devices FROM PUBLIC;


-- Runtime gets clinical tenant tables; RLS must be enabled and validated first.
GRANT SELECT, INSERT, UPDATE, DELETE ON patients, procedures, media_assets, procedure_reports TO scopx_runtime;
GRANT SELECT ON hospital_memberships, membership_roles TO scopx_runtime;
GRANT SELECT, INSERT ON audit_events TO scopx_runtime;
REVOKE UPDATE, DELETE ON audit_events FROM scopx_runtime;

-- Identity, platform, and licensing tables stay inaccessible until their specific
-- API and permissions are designed and tested.
REVOKE ALL ON users, hospitals, roles, platform_user_roles, hospital_licenses,
  license_features, license_activations, licensed_devices FROM scopx_runtime;
-- Schema access is a separate decision. If approved, grant USAGE on the schema
-- only (not CREATE); database connect permission is also managed separately.
