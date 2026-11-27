-- SCOPX.I.E migration 010: immutable report versions and signing evidence (DRAFT).
-- Apply only in a disposable database after review. This schema does not itself
-- establish legal/qualified e-signature status; the signing API and key controls
-- remain disabled until independent verification.
BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

ALTER TABLE public.procedure_reports
    ADD COLUMN IF NOT EXISTS report_body_sha256 varchar(64);

UPDATE public.procedure_reports
SET report_body_sha256 = encode(digest(convert_to(report_body, 'UTF8'), 'sha256'), 'hex')
WHERE report_body_sha256 IS NULL;

ALTER TABLE public.procedure_reports
    ALTER COLUMN report_body_sha256 SET NOT NULL;

ALTER TABLE public.procedure_reports
    ADD CONSTRAINT procedure_reports_body_sha256_format
    CHECK (report_body_sha256 ~ '^[0-9a-f]{64}$') NOT VALID;
ALTER TABLE public.procedure_reports
    VALIDATE CONSTRAINT procedure_reports_body_sha256_format;

ALTER TABLE public.procedure_reports
    ADD CONSTRAINT procedure_reports_hospital_id_unique UNIQUE (hospital_id, id);
ALTER TABLE public.procedure_reports
    ADD CONSTRAINT procedure_reports_hospital_procedure_id_unique
    UNIQUE (hospital_id, procedure_id, id);

ALTER TABLE public.procedure_reports
    ADD COLUMN IF NOT EXISTS amends_report_id uuid,
    ADD COLUMN IF NOT EXISTS amendment_reason varchar(1000);

ALTER TABLE public.procedure_reports
    ADD CONSTRAINT procedure_reports_amends_same_procedure_fk
    FOREIGN KEY (hospital_id, procedure_id, amends_report_id)
    REFERENCES public.procedure_reports(hospital_id, procedure_id, id)
    ON DELETE RESTRICT;

ALTER TABLE public.procedure_reports
    ADD CONSTRAINT procedure_reports_amendment_fields_check
    CHECK (
        (amends_report_id IS NULL AND amendment_reason IS NULL)
        OR
        (amends_report_id IS NOT NULL AND amendment_reason IS NOT NULL
         AND length(trim(amendment_reason)) BETWEEN 1 AND 1000
         AND amends_report_id <> id)
    ) NOT VALID;
ALTER TABLE public.procedure_reports
    VALIDATE CONSTRAINT procedure_reports_amendment_fields_check;

CREATE TABLE public.procedure_report_versions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    hospital_id uuid NOT NULL,
    report_id uuid NOT NULL,
    procedure_id uuid NOT NULL,
    report_version integer NOT NULL CHECK (report_version > 0),
    report_status varchar(20) NOT NULL CHECK (report_status IN ('draft','review','signed','amended')),
    report_body text NOT NULL,
    document_sha256 varchar(64) NOT NULL CHECK (document_sha256 ~ '^[0-9a-f]{64}$'),
    canonicalization_version varchar(32) NOT NULL
        CHECK (canonicalization_version IN ('SCOPX-REPORT-TEXT-V1','LEGACY-RAW-UTF8-V1')),
    created_by_membership_id uuid,
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (hospital_id, report_id, report_version),
    UNIQUE (hospital_id, report_id, report_version, document_sha256),
    FOREIGN KEY (hospital_id, report_id)
        REFERENCES public.procedure_reports(hospital_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (hospital_id, procedure_id)
        REFERENCES public.procedures(hospital_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (hospital_id, created_by_membership_id)
        REFERENCES public.hospital_memberships(hospital_id, id) ON DELETE RESTRICT
);

-- Preserve historical report content; legacy hashes use raw UTF-8 and are not
-- eligible for signing until a new canonical draft version is produced.
INSERT INTO public.procedure_report_versions
    (hospital_id, report_id, procedure_id, report_version, report_status,
     report_body, document_sha256, canonicalization_version, created_by_membership_id, created_at)
SELECT hospital_id, id, procedure_id, report_version, status, report_body,
       report_body_sha256, 'LEGACY-RAW-UTF8-V1', created_by_membership_id, created_at
FROM public.procedure_reports
ON CONFLICT (hospital_id, report_id, report_version) DO NOTHING;

CREATE TABLE public.procedure_report_signatures (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    hospital_id uuid NOT NULL,
    report_id uuid NOT NULL,
    report_version integer NOT NULL,
    document_sha256 varchar(64) NOT NULL CHECK (document_sha256 ~ '^[0-9a-f]{64}$'),
    signer_membership_id uuid NOT NULL,
    signed_at timestamptz NOT NULL DEFAULT now(),
    reauthenticated_at timestamptz NOT NULL,
    authentication_method varchar(32) NOT NULL
        CHECK (authentication_method IN ('password','mfa','hardware_key')),
    canonicalization_version varchar(32) NOT NULL
        CHECK (canonicalization_version = 'SCOPX-REPORT-TEXT-V1'),
    signature_algorithm varchar(32) NOT NULL CHECK (signature_algorithm = 'RSA-PSS-SHA256'),
    signing_key_id varchar(100) NOT NULL,
    signature_value bytea NOT NULL CHECK (octet_length(signature_value) BETWEEN 128 AND 8192),
    UNIQUE (hospital_id, report_id, report_version),
    FOREIGN KEY (hospital_id, report_id, report_version, document_sha256)
        REFERENCES public.procedure_report_versions(hospital_id, report_id, report_version, document_sha256)
        ON DELETE RESTRICT,
    FOREIGN KEY (hospital_id, signer_membership_id)
        REFERENCES public.hospital_memberships(hospital_id, id) ON DELETE RESTRICT,
    CHECK (reauthenticated_at <= signed_at),
    CHECK (signed_at - reauthenticated_at <= interval '5 minutes')
);

CREATE OR REPLACE FUNCTION public.scopx_capture_report_version()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO public.procedure_report_versions
        (hospital_id, report_id, procedure_id, report_version, report_status,
         report_body, document_sha256, canonicalization_version, created_by_membership_id, created_at)
    VALUES
        (NEW.hospital_id, NEW.id, NEW.procedure_id, NEW.report_version, NEW.status,
         NEW.report_body, NEW.report_body_sha256, 'SCOPX-REPORT-TEXT-V1', NEW.created_by_membership_id, now());
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS procedure_report_version_capture ON public.procedure_reports;
CREATE TRIGGER procedure_report_version_capture
AFTER INSERT OR UPDATE OF report_body, report_body_sha256, report_version
ON public.procedure_reports
FOR EACH ROW EXECUTE FUNCTION public.scopx_capture_report_version();

CREATE OR REPLACE FUNCTION public.scopx_guard_signed_report()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF OLD.signed_at IS NOT NULL THEN
        RAISE EXCEPTION 'signed report records are immutable; create an amendment instead'
            USING ERRCODE = '55000';
    END IF;

    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'report records cannot be deleted'
            USING ERRCODE = '55000';
    END IF;

    IF NEW.signed_at IS NOT NULL AND NOT EXISTS (
        SELECT 1
        FROM public.procedure_report_signatures s
        WHERE s.hospital_id = OLD.hospital_id
          AND s.report_id = OLD.id
          AND s.report_version = NEW.report_version
          AND s.signer_membership_id = NEW.signed_by_membership_id
          AND s.signed_at = NEW.signed_at
          AND s.document_sha256 = NEW.report_body_sha256
    ) THEN
        RAISE EXCEPTION 'signed report state requires a matching immutable signature artifact'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS procedure_report_signed_immutable ON public.procedure_reports;
CREATE TRIGGER procedure_report_signed_immutable
BEFORE UPDATE OR DELETE ON public.procedure_reports
FOR EACH ROW EXECUTE FUNCTION public.scopx_guard_signed_report();

CREATE OR REPLACE FUNCTION public.scopx_apply_report_signature()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE current_status text;
DECLARE current_version integer;
DECLARE current_hash varchar(64);
BEGIN
    SELECT status, report_version, report_body_sha256
      INTO current_status, current_version, current_hash
    FROM public.procedure_reports
    WHERE hospital_id = NEW.hospital_id AND id = NEW.report_id
    FOR UPDATE;

    IF current_status IS DISTINCT FROM 'review'
       OR current_version IS DISTINCT FROM NEW.report_version
       OR current_hash IS DISTINCT FROM NEW.document_sha256
       OR NOT EXISTS (
            SELECT 1 FROM public.procedure_report_versions v
            WHERE v.hospital_id = NEW.hospital_id
              AND v.report_id = NEW.report_id
              AND v.report_version = NEW.report_version
              AND v.document_sha256 = NEW.document_sha256
              AND v.canonicalization_version = NEW.canonicalization_version
              AND v.canonicalization_version = 'SCOPX-REPORT-TEXT-V1'
       ) THEN
        RAISE EXCEPTION 'only the current reviewed report version can be signed'
            USING ERRCODE = '23514';
    END IF;

    UPDATE public.procedure_reports
       SET status = 'signed',
           signed_by_membership_id = NEW.signer_membership_id,
           signed_at = NEW.signed_at,
           updated_at = now()
     WHERE hospital_id = NEW.hospital_id AND id = NEW.report_id;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS procedure_report_signature_apply ON public.procedure_report_signatures;
CREATE TRIGGER procedure_report_signature_apply
AFTER INSERT ON public.procedure_report_signatures
FOR EACH ROW EXECUTE FUNCTION public.scopx_apply_report_signature();

CREATE OR REPLACE FUNCTION public.scopx_reject_report_artifact_mutation()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'report version and signature artifacts are append-only'
        USING ERRCODE = '55000';
END;
$$;

CREATE OR REPLACE FUNCTION public.scopx_reject_report_artifact_truncate()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'report version and signature artifacts cannot be truncated'
        USING ERRCODE = '55000';
END;
$$;

DO $$
DECLARE tbl text;
BEGIN
    FOREACH tbl IN ARRAY ARRAY['procedure_report_versions','procedure_report_signatures']
    LOOP
        EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', tbl);
        EXECUTE format('ALTER TABLE public.%I FORCE ROW LEVEL SECURITY', tbl);
        EXECUTE format('CREATE POLICY tenant_scope_select ON public.%I FOR SELECT USING (hospital_id = public.scopx_current_hospital_id())', tbl);
        EXECUTE format('CREATE POLICY tenant_scope_insert ON public.%I FOR INSERT WITH CHECK (hospital_id = public.scopx_current_hospital_id())', tbl);
        EXECUTE format('CREATE TRIGGER %I BEFORE UPDATE OR DELETE ON public.%I FOR EACH ROW EXECUTE FUNCTION public.scopx_reject_report_artifact_mutation()', tbl || '_immutable', tbl);
        EXECUTE format('CREATE TRIGGER %I BEFORE TRUNCATE ON public.%I FOR EACH STATEMENT EXECUTE FUNCTION public.scopx_reject_report_artifact_truncate()', tbl || '_no_truncate', tbl);
    END LOOP;
END $$;

COMMIT;
