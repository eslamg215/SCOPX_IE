-- SCOPX.I.E migration 009: restrict hospital directory visibility to memberships.
-- Runtime sessions set scopx.user_id transaction-locally before membership lookup.
BEGIN;

ALTER TABLE public.hospitals ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.hospitals FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS user_hospital_select ON public.hospitals;
CREATE POLICY user_hospital_select ON public.hospitals
FOR SELECT
USING (
    EXISTS (
        SELECT 1
        FROM public.hospital_memberships hm
        WHERE hm.hospital_id = hospitals.id
          AND hm.user_id = NULLIF(current_setting('scopx.user_id', true), '')::uuid
          AND hm.status = 'active'
    )
);

COMMIT;
