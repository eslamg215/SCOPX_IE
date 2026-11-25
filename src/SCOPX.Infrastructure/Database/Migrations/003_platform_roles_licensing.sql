-- SCOPX.I.E migration 003: platform-level admin grants and license lifecycle.
-- Development draft only; licensing must be backed by signed server-side validation.
BEGIN;

-- Platform roles are intentionally separate from hospital membership roles.
CREATE TABLE IF NOT EXISTS platform_user_roles (
    user_id uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    role_id uuid NOT NULL REFERENCES roles(id) ON DELETE RESTRICT,
    granted_at timestamptz NOT NULL DEFAULT now(),
    granted_by_user_id uuid REFERENCES users(id) ON DELETE RESTRICT,
    PRIMARY KEY (user_id, role_id)
);

INSERT INTO roles (code, display_name, scope) VALUES
    ('platform_super_admin', 'Platform Super Admin', 'platform'),
    ('platform_support', 'Platform Support (restricted)', 'platform')
ON CONFLICT (code) DO NOTHING;


CREATE TABLE IF NOT EXISTS hospital_licenses (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    hospital_id uuid NOT NULL REFERENCES hospitals(id) ON DELETE RESTRICT,
    license_key_hash char(64) NOT NULL UNIQUE,
    plan_code varchar(48) NOT NULL,
    status varchar(20) NOT NULL DEFAULT 'pending'
        CHECK (status IN ('pending','active','expired','revoked','suspended')),
    valid_from timestamptz,
    valid_until timestamptz,
    seat_limit integer CHECK (seat_limit IS NULL OR seat_limit >= 1),
    device_limit integer CHECK (device_limit IS NULL OR device_limit >= 1),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CHECK (valid_until IS NULL OR valid_from IS NULL OR valid_until > valid_from),
    UNIQUE (hospital_id, id)
);
CREATE INDEX IF NOT EXISTS ix_license_hospital_status ON hospital_licenses(hospital_id, status);

CREATE TABLE IF NOT EXISTS license_features (
    license_id uuid NOT NULL REFERENCES hospital_licenses(id) ON DELETE RESTRICT,
    feature_code varchar(80) NOT NULL,
    enabled boolean NOT NULL DEFAULT false,
    limit_value integer CHECK (limit_value IS NULL OR limit_value >= 0),
    PRIMARY KEY (license_id, feature_code)
);

CREATE TABLE IF NOT EXISTS license_activations (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    hospital_id uuid NOT NULL,
    license_id uuid NOT NULL,
    activation_code_hash char(64) NOT NULL UNIQUE,
    activated_at timestamptz,
    last_validated_at timestamptz,
    expires_at timestamptz,
    status varchar(20) NOT NULL DEFAULT 'pending'
        CHECK (status IN ('pending','active','expired','revoked')),
    FOREIGN KEY (hospital_id, license_id)
        REFERENCES hospital_licenses(hospital_id, id) ON DELETE RESTRICT,
    UNIQUE (hospital_id, id)
);
CREATE INDEX IF NOT EXISTS ix_activation_license_status ON license_activations(hospital_id, license_id, status);

CREATE TABLE IF NOT EXISTS licensed_devices (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    hospital_id uuid NOT NULL,
    activation_id uuid NOT NULL,
    device_fingerprint_hash char(64) NOT NULL,
    device_label varchar(120),
    bound_at timestamptz NOT NULL DEFAULT now(),
    last_seen_at timestamptz,
    status varchar(20) NOT NULL DEFAULT 'active'
        CHECK (status IN ('active','disabled','unbound')),
    FOREIGN KEY (hospital_id, activation_id)
        REFERENCES license_activations(hospital_id, id) ON DELETE RESTRICT,
    UNIQUE (hospital_id, id),
    UNIQUE (hospital_id, device_fingerprint_hash)
);
CREATE INDEX IF NOT EXISTS ix_devices_activation_status ON licensed_devices(hospital_id, activation_id, status);

COMMIT;
