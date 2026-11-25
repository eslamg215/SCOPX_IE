-- SCOPX.I.E migration 001: identity and tenancy foundation
-- Draft migration for local development only. No real patient data.
BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS users (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    email_normalized varchar(320) NOT NULL UNIQUE,
    display_name varchar(160) NOT NULL,
    status varchar(24) NOT NULL DEFAULT 'active'
        CHECK (status IN ('active', 'disabled', 'pending')),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);


CREATE TABLE IF NOT EXISTS hospitals (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name varchar(200) NOT NULL,
    status varchar(24) NOT NULL DEFAULT 'active'
        CHECK (status IN ('active', 'suspended', 'closed')),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);


CREATE TABLE IF NOT EXISTS hospital_memberships (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    hospital_id uuid NOT NULL REFERENCES hospitals(id) ON DELETE RESTRICT,
    user_id uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    status varchar(24) NOT NULL DEFAULT 'active'
        CHECK (status IN ('active', 'suspended', 'invited')),
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (hospital_id, user_id),
    -- Composite key supports tenant-consistent foreign keys in later tables.
    UNIQUE (hospital_id, id)
);


CREATE TABLE IF NOT EXISTS roles (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code varchar(48) NOT NULL UNIQUE,
    display_name varchar(100) NOT NULL,
    scope varchar(16) NOT NULL CHECK (scope IN ('platform', 'hospital'))
);


CREATE TABLE IF NOT EXISTS membership_roles (
    hospital_id uuid NOT NULL,
    membership_id uuid NOT NULL,
    role_id uuid NOT NULL REFERENCES roles(id) ON DELETE RESTRICT,
    granted_at timestamptz NOT NULL DEFAULT now(),
    granted_by_user_id uuid REFERENCES users(id) ON DELETE RESTRICT,
    PRIMARY KEY (membership_id, role_id),
    FOREIGN KEY (hospital_id, membership_id)
        REFERENCES hospital_memberships(hospital_id, id) ON DELETE RESTRICT
);


CREATE INDEX IF NOT EXISTS ix_memberships_user_status
    ON hospital_memberships(user_id, status);
CREATE INDEX IF NOT EXISTS ix_memberships_hospital_status
    ON hospital_memberships(hospital_id, status);
CREATE INDEX IF NOT EXISTS ix_membership_roles_hospital
    ON membership_roles(hospital_id);


-- Initial hospital-scoped roles. Platform Super Admin is provisioned separately
-- by a secure bootstrap process; it must never be granted from ordinary UI signup.
INSERT INTO roles (code, display_name, scope) VALUES
    ('hospital_admin', 'Hospital Admin', 'hospital'),
    ('doctor', 'Doctor', 'hospital'),
    ('nurse', 'Nurse', 'hospital'),
    ('endoscopy_technician', 'Endoscopy Technician', 'hospital'),
    ('reception', 'Reception', 'hospital'),
    ('auditor', 'Auditor (Read-only)', 'hospital')
ON CONFLICT (code) DO NOTHING;

COMMIT;
