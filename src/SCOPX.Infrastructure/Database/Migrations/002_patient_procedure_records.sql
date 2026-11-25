-- SCOPX.I.E migration 002: tenant-scoped patient and procedure records.
-- Development schema only. Do NOT load real patient information until security review,
-- access controls, retention policies, encryption, and deployment controls are complete.
BEGIN;

CREATE TABLE IF NOT EXISTS patients (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    hospital_id uuid NOT NULL REFERENCES hospitals(id) ON DELETE RESTRICT,
    patient_code varchar(80) NOT NULL,
    full_name varchar(200) NOT NULL,
    date_of_birth date,
    sex_at_birth varchar(24) CHECK (sex_at_birth IN ('female','male','intersex','unknown','not_recorded')),
    contact_phone varchar(40),
    notes text,
    status varchar(20) NOT NULL DEFAULT 'active' CHECK (status IN ('active','archived')),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    archived_at timestamptz,
    UNIQUE (hospital_id, id),
    UNIQUE (hospital_id, patient_code)
);

CREATE INDEX IF NOT EXISTS ix_patients_hospital_name ON patients(hospital_id, full_name);
CREATE INDEX IF NOT EXISTS ix_patients_hospital_status ON patients(hospital_id, status);

CREATE TABLE IF NOT EXISTS procedures (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    hospital_id uuid NOT NULL REFERENCES hospitals(id) ON DELETE RESTRICT,
    patient_id uuid NOT NULL,
    procedure_type varchar(64) NOT NULL,
    scheduled_at timestamptz,
    started_at timestamptz,
    ended_at timestamptz,
    status varchar(24) NOT NULL DEFAULT 'scheduled'
        CHECK (status IN ('scheduled','in_progress','completed','cancelled')),
    performed_by_membership_id uuid,
    room_label varchar(80),
    clinical_indication text,
    findings text,
    impression text,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (hospital_id, id),
    FOREIGN KEY (hospital_id, patient_id)
        REFERENCES patients(hospital_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (hospital_id, performed_by_membership_id)
        REFERENCES hospital_memberships(hospital_id, id) ON DELETE RESTRICT,
    CHECK (ended_at IS NULL OR started_at IS NULL OR ended_at >= started_at)
);
CREATE INDEX IF NOT EXISTS ix_procedures_hospital_schedule ON procedures(hospital_id, scheduled_at);
CREATE INDEX IF NOT EXISTS ix_procedures_patient ON procedures(hospital_id, patient_id, created_at DESC);

CREATE TABLE IF NOT EXISTS media_assets (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    hospital_id uuid NOT NULL REFERENCES hospitals(id) ON DELETE RESTRICT,
    procedure_id uuid NOT NULL,
    media_type varchar(16) NOT NULL CHECK (media_type IN ('image','video','audio')),
    storage_key text NOT NULL,
    file_name varchar(255),
    content_type varchar(120),
    file_size_bytes bigint CHECK (file_size_bytes IS NULL OR file_size_bytes >= 0),
    sha256_hex char(64),
    captured_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (hospital_id, procedure_id)
        REFERENCES procedures(hospital_id, id) ON DELETE RESTRICT,
    UNIQUE (hospital_id, id)
);
CREATE INDEX IF NOT EXISTS ix_media_procedure ON media_assets(hospital_id, procedure_id, captured_at);

CREATE TABLE IF NOT EXISTS procedure_reports (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    hospital_id uuid NOT NULL REFERENCES hospitals(id) ON DELETE RESTRICT,
    procedure_id uuid NOT NULL,
    report_version integer NOT NULL DEFAULT 1 CHECK (report_version > 0),
    status varchar(20) NOT NULL DEFAULT 'draft' CHECK (status IN ('draft','review','signed','amended')),
    report_body text NOT NULL DEFAULT '',
    created_by_membership_id uuid,
    signed_by_membership_id uuid,
    signed_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (hospital_id, procedure_id)
        REFERENCES procedures(hospital_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (hospital_id, created_by_membership_id)
        REFERENCES hospital_memberships(hospital_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (hospital_id, signed_by_membership_id)
        REFERENCES hospital_memberships(hospital_id, id) ON DELETE RESTRICT,
    UNIQUE (hospital_id, procedure_id, report_version),
    CHECK ((signed_at IS NULL) OR (signed_by_membership_id IS NOT NULL))
);


-- Append-only audit trail table. App-level authorization and write-only insert
-- permissions must be implemented before exposing this through an API.
CREATE TABLE IF NOT EXISTS audit_events (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    hospital_id uuid REFERENCES hospitals(id) ON DELETE RESTRICT,
    actor_user_id uuid REFERENCES users(id) ON DELETE RESTRICT,
    event_type varchar(100) NOT NULL,
    target_type varchar(100) NOT NULL,
    target_id text,
    occurred_at timestamptz NOT NULL DEFAULT now(),
    correlation_id uuid,
    details jsonb NOT NULL DEFAULT '{}'::jsonb
);
CREATE INDEX IF NOT EXISTS ix_audit_hospital_time ON audit_events(hospital_id, occurred_at DESC);
CREATE INDEX IF NOT EXISTS ix_audit_actor_time ON audit_events(actor_user_id, occurred_at DESC);

COMMIT;
