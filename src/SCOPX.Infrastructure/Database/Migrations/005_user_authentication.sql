-- SCOPX.I.E migration 005: authentication credential and lockout persistence
-- Draft for local development. Apply only to disposable development databases.
-- Passwords must be stored as framework-generated hashes, never as plaintext.
BEGIN;

ALTER TABLE public.users
    ADD COLUMN IF NOT EXISTS password_hash text,
    ADD COLUMN IF NOT EXISTS failed_login_count integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS lockout_until timestamptz,
    ADD COLUMN IF NOT EXISTS last_login_at timestamptz;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'ck_users_failed_login_count_nonnegative'
          AND conrelid = 'public.users'::regclass
    ) THEN
        ALTER TABLE public.users
            ADD CONSTRAINT ck_users_failed_login_count_nonnegative
            CHECK (failed_login_count >= 0);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_users_lockout_until
    ON public.users(lockout_until)
    WHERE lockout_until IS NOT NULL;

COMMENT ON COLUMN public.users.password_hash IS
    'Framework password hash; null for accounts not yet provisioned. Never plaintext.';
COMMENT ON COLUMN public.users.failed_login_count IS
    'Consecutive failed authentication attempts; reset after successful login.';
COMMENT ON COLUMN public.users.lockout_until IS
    'UTC timestamp until which password login is blocked, if set.';

COMMIT;
