-- SCOPX.I.E migration 008: enforce role scope at the database boundary.
-- Development hardening migration. Apply only after reviewing existing role assignments.
BEGIN;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM public.membership_roles mr
        JOIN public.roles r ON r.id = mr.role_id
        WHERE r.scope <> 'hospital'
    ) THEN
        RAISE EXCEPTION 'membership_roles contains a non-hospital role; resolve assignments before migration 008';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM public.platform_user_roles pur
        JOIN public.roles r ON r.id = pur.role_id
        WHERE r.scope <> 'platform'
    ) THEN
        RAISE EXCEPTION 'platform_user_roles contains a non-platform role; resolve assignments before migration 008';
    END IF;
END $$;

CREATE OR REPLACE FUNCTION public.scopx_enforce_membership_role_scope()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE role_scope text;
BEGIN
    SELECT scope INTO role_scope
    FROM public.roles
    WHERE id = NEW.role_id
    FOR KEY SHARE;

    IF role_scope IS DISTINCT FROM 'hospital' THEN
        RAISE EXCEPTION 'only hospital-scoped roles may be assigned to hospital memberships'
            USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION public.scopx_enforce_platform_role_scope()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE role_scope text;
BEGIN
    SELECT scope INTO role_scope
    FROM public.roles
    WHERE id = NEW.role_id
    FOR KEY SHARE;

    IF role_scope IS DISTINCT FROM 'platform' THEN
        RAISE EXCEPTION 'only platform-scoped roles may be assigned as platform roles'
            USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS membership_roles_scope_guard ON public.membership_roles;
CREATE TRIGGER membership_roles_scope_guard
BEFORE INSERT OR UPDATE OF role_id ON public.membership_roles
FOR EACH ROW EXECUTE FUNCTION public.scopx_enforce_membership_role_scope();

DROP TRIGGER IF EXISTS platform_user_roles_scope_guard ON public.platform_user_roles;
CREATE TRIGGER platform_user_roles_scope_guard
BEFORE INSERT OR UPDATE OF role_id ON public.platform_user_roles
FOR EACH ROW EXECUTE FUNCTION public.scopx_enforce_platform_role_scope();


CREATE OR REPLACE FUNCTION public.scopx_prevent_assigned_role_scope_change()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.scope IS DISTINCT FROM OLD.scope THEN
        IF NEW.scope <> 'hospital' AND EXISTS (
            SELECT 1 FROM public.membership_roles WHERE role_id = OLD.id
        ) THEN
            RAISE EXCEPTION 'cannot change scope of a role assigned to hospital memberships'
                USING ERRCODE = '23514';
        END IF;
        IF NEW.scope <> 'platform' AND EXISTS (
            SELECT 1 FROM public.platform_user_roles WHERE role_id = OLD.id
        ) THEN
            RAISE EXCEPTION 'cannot change scope of a role assigned to platform users'
                USING ERRCODE = '23514';
        END IF;
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS roles_scope_assignment_guard ON public.roles;
CREATE TRIGGER roles_scope_assignment_guard
BEFORE UPDATE OF scope ON public.roles
FOR EACH ROW EXECUTE FUNCTION public.scopx_prevent_assigned_role_scope_change();

COMMIT;
