-- 01-schema.sql — local-stack bridge schema (TASK-014).
--
-- Nine tables of the canonical ERD (docs/architecture/erd.dbml, TASK-008), column for column, so the local
-- PostgreSQL can hold one user per role R01–R08 before the application schema exists. This file is a bridge,
-- not the schema of record:
--   * TASK-023/TASK-024 create the schema through EF Core migrations. When they land, this file is deleted and
--     docker-compose.yml runs `dotnet ef database update` instead; 02-/03- become DML against the migrated schema.
--   * Until then, docs/architecture/local-stack-check.py fails if a column here differs from erd.dbml.
--
-- Deliberately not bridged: identity_access.permission and permission_profile_grant (the catalogue is TASK-030/
-- TASK-110), so the shipped-default profile versions below carry no grants yet; and project.project, so the
-- access_relationship.project_id foreign key is deferred (column present, constraint absent).
--
-- Runs once, on first start of an empty data volume (postgres image: /docker-entrypoint-initdb.d).

CREATE SCHEMA identity_access;
CREATE SCHEMA master_data_config;

CREATE TABLE identity_access.department (
    id uuid PRIMARY KEY,
    code varchar(50) NOT NULL UNIQUE,
    name_ar varchar(200) NOT NULL,
    name_en varchar(200) NOT NULL,
    parent_department_id uuid,
    directory_reference varchar(200),
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL,
    created_by uuid NOT NULL,
    updated_at timestamptz NOT NULL,
    updated_by uuid NOT NULL
);

CREATE TABLE master_data_config.master_data_catalogue (
    id uuid PRIMARY KEY,
    code varchar(50) NOT NULL UNIQUE,
    name_ar varchar(200) NOT NULL,
    name_en varchar(200) NOT NULL,
    allows_hierarchy boolean NOT NULL DEFAULT false,
    is_system boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL,
    created_by uuid NOT NULL,
    updated_at timestamptz NOT NULL,
    updated_by uuid NOT NULL
);

CREATE TABLE master_data_config.master_data_item (
    id uuid PRIMARY KEY,
    catalogue_id uuid NOT NULL,
    code varchar(50) NOT NULL,
    label_ar varchar(200) NOT NULL,
    label_en varchar(200) NOT NULL,
    description_ar varchar(200),
    description_en varchar(200),
    parent_item_id uuid,
    sort_order integer NOT NULL DEFAULT 0,
    is_system boolean NOT NULL DEFAULT false,
    lifecycle_state varchar(50) NOT NULL,
    validated_by_user_id uuid,
    validated_at timestamptz,
    published_by_user_id uuid,
    published_at timestamptz,
    retired_at timestamptz,
    created_at timestamptz NOT NULL,
    created_by uuid NOT NULL,
    updated_at timestamptz NOT NULL,
    updated_by uuid NOT NULL,
    UNIQUE (catalogue_id, code)
);

CREATE TABLE identity_access.role (
    id uuid PRIMARY KEY,
    code varchar(50) NOT NULL UNIQUE,
    name_ar varchar(200) NOT NULL,
    name_en varchar(200) NOT NULL,
    is_system boolean NOT NULL DEFAULT true,
    is_external_eligible boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL,
    created_by uuid NOT NULL,
    updated_at timestamptz NOT NULL,
    updated_by uuid NOT NULL
);

CREATE TABLE identity_access."user" (
    id uuid PRIMARY KEY,
    user_type varchar(50) NOT NULL,
    directory_subject_id varchar(200) UNIQUE,
    username varchar(100) NOT NULL UNIQUE,
    display_name varchar(200) NOT NULL,
    email varchar(200) NOT NULL UNIQUE,
    mobile_number varchar(20),
    mobile_verified_at timestamptz,
    job_title varchar(200),
    department_id uuid,
    manager_user_id uuid,
    external_entity_id uuid,
    preferred_language char(2) NOT NULL DEFAULT 'ar',
    status varchar(50) NOT NULL,
    disabled_at timestamptz,
    mfa_enrolled_at timestamptz,
    nafath_verification_reference varchar(200),
    nafath_verified_at timestamptz,
    created_at timestamptz NOT NULL,
    created_by uuid NOT NULL,
    updated_at timestamptz NOT NULL,
    updated_by uuid NOT NULL
);

CREATE TABLE identity_access.external_entity (
    id uuid PRIMARY KEY,
    code varchar(50) NOT NULL UNIQUE,
    name_ar varchar(200) NOT NULL,
    name_en varchar(200) NOT NULL,
    entity_type_item_id uuid NOT NULL,
    status varchar(50) NOT NULL,
    sponsor_user_id uuid,
    created_at timestamptz NOT NULL,
    created_by uuid NOT NULL,
    updated_at timestamptz NOT NULL,
    updated_by uuid NOT NULL
);

CREATE TABLE identity_access.permission_profile (
    id uuid PRIMARY KEY,
    code varchar(50) NOT NULL UNIQUE,
    name_ar varchar(200) NOT NULL,
    name_en varchar(200) NOT NULL,
    base_role_id uuid NOT NULL,
    is_shipped_default boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL,
    created_by uuid NOT NULL,
    updated_at timestamptz NOT NULL,
    updated_by uuid NOT NULL
);

CREATE TABLE identity_access.permission_profile_version (
    id uuid PRIMARY KEY,
    permission_profile_id uuid NOT NULL,
    version_no integer NOT NULL,
    lifecycle_state varchar(50) NOT NULL,
    validated_by_user_id uuid,
    validated_at timestamptz,
    published_by_user_id uuid,
    published_at timestamptz,
    retired_at timestamptz,
    change_summary text,
    change_summary_lang char(2),
    created_at timestamptz NOT NULL,
    created_by uuid NOT NULL,
    updated_at timestamptz NOT NULL,
    updated_by uuid NOT NULL,
    UNIQUE (permission_profile_id, version_no)
);

CREATE TABLE identity_access.access_relationship (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL,
    permission_profile_version_id uuid NOT NULL,
    department_id uuid,
    external_entity_id uuid,
    project_id uuid,
    sponsor_user_id uuid,
    starts_at timestamptz NOT NULL,
    ends_at timestamptz,
    end_reason varchar(50),
    status varchar(50) NOT NULL,
    created_at timestamptz NOT NULL,
    created_by uuid NOT NULL,
    updated_at timestamptz NOT NULL,
    updated_by uuid NOT NULL
);

-- Foreign keys, added after all tables exist because user ↔ external_entity ↔ master_data_item reference each other.
ALTER TABLE identity_access.department ADD CONSTRAINT fk_department_parent_department_id FOREIGN KEY (parent_department_id) REFERENCES identity_access.department (id);
ALTER TABLE master_data_config.master_data_item ADD CONSTRAINT fk_master_data_item_catalogue_id FOREIGN KEY (catalogue_id) REFERENCES master_data_config.master_data_catalogue (id);
ALTER TABLE master_data_config.master_data_item ADD CONSTRAINT fk_master_data_item_parent_item_id FOREIGN KEY (parent_item_id) REFERENCES master_data_config.master_data_item (id);
ALTER TABLE master_data_config.master_data_item ADD CONSTRAINT fk_master_data_item_validated_by_user_id FOREIGN KEY (validated_by_user_id) REFERENCES identity_access."user" (id);
ALTER TABLE master_data_config.master_data_item ADD CONSTRAINT fk_master_data_item_published_by_user_id FOREIGN KEY (published_by_user_id) REFERENCES identity_access."user" (id);
ALTER TABLE identity_access."user" ADD CONSTRAINT fk_user_department_id FOREIGN KEY (department_id) REFERENCES identity_access.department (id);
ALTER TABLE identity_access."user" ADD CONSTRAINT fk_user_manager_user_id FOREIGN KEY (manager_user_id) REFERENCES identity_access."user" (id);
ALTER TABLE identity_access."user" ADD CONSTRAINT fk_user_external_entity_id FOREIGN KEY (external_entity_id) REFERENCES identity_access.external_entity (id);
ALTER TABLE identity_access.external_entity ADD CONSTRAINT fk_external_entity_entity_type_item_id FOREIGN KEY (entity_type_item_id) REFERENCES master_data_config.master_data_item (id);
ALTER TABLE identity_access.external_entity ADD CONSTRAINT fk_external_entity_sponsor_user_id FOREIGN KEY (sponsor_user_id) REFERENCES identity_access."user" (id);
ALTER TABLE identity_access.permission_profile ADD CONSTRAINT fk_permission_profile_base_role_id FOREIGN KEY (base_role_id) REFERENCES identity_access.role (id);
ALTER TABLE identity_access.permission_profile_version ADD CONSTRAINT fk_permission_profile_version_permission_profile_id FOREIGN KEY (permission_profile_id) REFERENCES identity_access.permission_profile (id);
ALTER TABLE identity_access.permission_profile_version ADD CONSTRAINT fk_permission_profile_version_validated_by_user_id FOREIGN KEY (validated_by_user_id) REFERENCES identity_access."user" (id);
ALTER TABLE identity_access.permission_profile_version ADD CONSTRAINT fk_permission_profile_version_published_by_user_id FOREIGN KEY (published_by_user_id) REFERENCES identity_access."user" (id);
ALTER TABLE identity_access.access_relationship ADD CONSTRAINT fk_access_relationship_user_id FOREIGN KEY (user_id) REFERENCES identity_access."user" (id);
ALTER TABLE identity_access.access_relationship ADD CONSTRAINT fk_access_relationship_permission_profile_version_id FOREIGN KEY (permission_profile_version_id) REFERENCES identity_access.permission_profile_version (id);
ALTER TABLE identity_access.access_relationship ADD CONSTRAINT fk_access_relationship_department_id FOREIGN KEY (department_id) REFERENCES identity_access.department (id);
ALTER TABLE identity_access.access_relationship ADD CONSTRAINT fk_access_relationship_external_entity_id FOREIGN KEY (external_entity_id) REFERENCES identity_access.external_entity (id);
-- deferred (target not bridged): ALTER TABLE identity_access.access_relationship ADD CONSTRAINT fk_access_relationship_project_id FOREIGN KEY (project_id) REFERENCES project.project (id);
ALTER TABLE identity_access.access_relationship ADD CONSTRAINT fk_access_relationship_sponsor_user_id FOREIGN KEY (sponsor_user_id) REFERENCES identity_access."user" (id);
