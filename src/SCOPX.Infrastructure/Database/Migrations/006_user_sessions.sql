-- SCOPX.I.E migration 006: opaque login sessions (development draft)
-- Store only token hashes. Do not apply to production until security review.
BEGIN;
CREATE TABLE IF NOT EXISTS public.user_sessions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id uuid NOT NULL REFERENCES public.users(id) ON DELETE CASCADE,
    token_hash char(64) NOT NULL UNIQUE,
    created_at timestamptz NOT NULL DEFAULT now(),
    expires_at timestamptz NOT NULL,
    revoked_at timestamptz,
    last_seen_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_user_sessions_expiry CHECK (expires_at > created_at)
);
CREATE INDEX IF NOT EXISTS ix_user_sessions_user_active
    ON public.user_sessions(user_id, expires_at) WHERE revoked_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_user_sessions_expiry
    ON public.user_sessions(expires_at);
COMMENT ON TABLE public.user_sessions IS 'Opaque sessions; token hashes only. Development draft, not production ready.';
COMMIT;
