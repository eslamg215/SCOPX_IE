-- SCOPX.I.E migration 004: tenant RLS defense-in-depth (DRAFT)
-- Apply only after security review. Runtime API role must not own tables or BYPASSRLS.
-- Context must be set transaction-locally by trusted API code after membership validation.
-- Missing/empty context intentionally matches no rows.
BEGIN;

CREATE OR REPLACE FUNCTION scopx_current_hospital_id()
RETURNS uuid
LANGUAGE sql
STABLE
AS $$
  SELECT NULLIF(current_setting('scopx.hospital_id', true), '')::uuid
$$;

DO $$
DECLARE tbl text;
BEGIN
  FOREACH tbl IN ARRAY ARRAY['membership_roles','patients','procedures','media_assets','procedure_reports']
  LOOP
    EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', tbl);
    EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY', tbl);
    EXECUTE format('DROP POLICY IF EXISTS tenant_scope_select ON %I', tbl);
    EXECUTE format('DROP POLICY IF EXISTS tenant_scope_insert ON %I', tbl);
    EXECUTE format('DROP POLICY IF EXISTS tenant_scope_update ON %I', tbl);
    EXECUTE format('DROP POLICY IF EXISTS tenant_scope_delete ON %I', tbl);
    EXECUTE format('CREATE POLICY tenant_scope_select ON %I FOR SELECT USING (hospital_id = scopx_current_hospital_id())', tbl);
    EXECUTE format('CREATE POLICY tenant_scope_insert ON %I FOR INSERT WITH CHECK (hospital_id = scopx_current_hospital_id())', tbl);
    EXECUTE format('CREATE POLICY tenant_scope_update ON %I FOR UPDATE USING (hospital_id = scopx_current_hospital_id()) WITH CHECK (hospital_id = scopx_current_hospital_id())', tbl);
    EXECUTE format('CREATE POLICY tenant_scope_delete ON %I FOR DELETE USING (hospital_id = scopx_current_hospital_id())', tbl);
  END LOOP;
END $$;

-- Membership discovery is needed before hospital context is established. It is scoped
-- to the authenticated user set by trusted API code, not to a client header.
ALTER TABLE hospital_memberships ENABLE ROW LEVEL SECURITY;
ALTER TABLE hospital_memberships FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS user_membership_select ON hospital_memberships;
DROP POLICY IF EXISTS tenant_membership_write ON hospital_memberships;
DROP POLICY IF EXISTS tenant_membership_insert ON hospital_memberships;
DROP POLICY IF EXISTS tenant_membership_update ON hospital_memberships;
DROP POLICY IF EXISTS tenant_membership_delete ON hospital_memberships;
CREATE POLICY user_membership_select ON hospital_memberships FOR SELECT
  USING (user_id = NULLIF(current_setting('scopx.user_id', true), '')::uuid);
CREATE POLICY tenant_membership_insert ON hospital_memberships FOR INSERT
  WITH CHECK (hospital_id = scopx_current_hospital_id());
CREATE POLICY tenant_membership_update ON hospital_memberships FOR UPDATE
  USING (hospital_id = scopx_current_hospital_id())
  WITH CHECK (hospital_id = scopx_current_hospital_id());
CREATE POLICY tenant_membership_delete ON hospital_memberships FOR DELETE
  USING (hospital_id = scopx_current_hospital_id());

-- Audit events: tenant reads only; application runtime should receive INSERT only,
-- not UPDATE/DELETE. Platform/system events with NULL hospital require a separate
-- privileged access path and are not visible through tenant-scoped sessions.
ALTER TABLE audit_events ENABLE ROW LEVEL SECURITY;
ALTER TABLE audit_events FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_scope_select ON audit_events;
DROP POLICY IF EXISTS tenant_scope_insert ON audit_events;
CREATE POLICY tenant_scope_select ON audit_events FOR SELECT
  USING (hospital_id = scopx_current_hospital_id());
CREATE POLICY tenant_scope_insert ON audit_events FOR INSERT
  WITH CHECK (hospital_id = scopx_current_hospital_id());

COMMIT;
