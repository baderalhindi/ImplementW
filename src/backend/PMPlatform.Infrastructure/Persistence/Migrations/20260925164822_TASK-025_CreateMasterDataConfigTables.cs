using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-025, 2 of 4: the 22 FG-04 tables of <c>master_data_config</c> (erd.dbml): master data, versioned
    /// configuration and its typed rows. Carries ADR-011 (five-level impact dimensions and the 5×5 matrix), ADR-012
    /// (bilingual labels) and ADR-008 (SAR thresholds). The schema itself is TASK-024's.
    /// </summary>
    public partial class TASK025_CreateMasterDataConfigTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "configuration_family",
                schema: "master_data_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuration_family", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "master_data_catalogue",
                schema: "master_data_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    allows_hierarchy = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_master_data_catalogue", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "configuration_version",
                schema: "master_data_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuration_family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    change_summary_lang = table.Column<string>(type: "char(2)", nullable: true),
                    change_summary = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    lifecycle_state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    validated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuration_version", x => x.id);
                    table.CheckConstraint("ck_configuration_version_change_summary_lang", "\"change_summary_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_configuration_version_change_summary_pair", "(\"change_summary\" IS NULL) = (\"change_summary_lang\" IS NULL)");
                    table.CheckConstraint("ck_configuration_version_lifecycle_state", "\"lifecycle_state\" IN ('DRAFT', 'VALIDATED', 'PUBLISHED', 'RETIRED')");
                    table.ForeignKey(
                        name: "fk_configuration_version_configuration_family_configuration_fa",
                        column: x => x.configuration_family_id,
                        principalSchema: "master_data_config",
                        principalTable: "configuration_family",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_configuration_version_user_published_by_user_id",
                        column: x => x.published_by_user_id,
                        principalSchema: "identity_access",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_configuration_version_user_validated_by_user_id",
                        column: x => x.validated_by_user_id,
                        principalSchema: "identity_access",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "master_data_item",
                schema: "master_data_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalogue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    parent_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    description_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    description_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    label_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    label_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    lifecycle_state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    validated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_master_data_item", x => x.id);
                    table.CheckConstraint("ck_master_data_item_description", "(\"description_ar\" IS NULL) = (\"description_en\" IS NULL)");
                    table.CheckConstraint("ck_master_data_item_lifecycle_state", "\"lifecycle_state\" IN ('DRAFT', 'VALIDATED', 'PUBLISHED', 'RETIRED')");
                    table.ForeignKey(
                        name: "fk_master_data_item_master_data_catalogue_catalogue_id",
                        column: x => x.catalogue_id,
                        principalSchema: "master_data_config",
                        principalTable: "master_data_catalogue",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_master_data_item_master_data_item_parent_item_id",
                        column: x => x.parent_item_id,
                        principalSchema: "master_data_config",
                        principalTable: "master_data_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_master_data_item_user_published_by_user_id",
                        column: x => x.published_by_user_id,
                        principalSchema: "identity_access",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_master_data_item_user_validated_by_user_id",
                        column: x => x.validated_by_user_id,
                        principalSchema: "identity_access",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "configuration_value",
                schema: "master_data_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuration_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    value_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuration_value", x => x.id);
                    table.CheckConstraint("ck_configuration_value_value_type", "\"value_type\" IN ('INTEGER', 'DECIMAL', 'BOOLEAN', 'TEXT', 'DURATION_DAYS', 'PERCENT')");
                    table.ForeignKey(
                        name: "fk_configuration_value_configuration_version_configuration_ver",
                        column: x => x.configuration_version_id,
                        principalSchema: "master_data_config",
                        principalTable: "configuration_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_event_family",
                schema: "master_data_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuration_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_mandatory = table.Column<bool>(type: "boolean", nullable: false),
                    label_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    label_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_event_family", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_event_family_configuration_version_configurati",
                        column: x => x.configuration_version_id,
                        principalSchema: "master_data_config",
                        principalTable: "configuration_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "probability_level_definition",
                schema: "master_data_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuration_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level = table.Column<short>(type: "smallint", nullable: false),
                    lower_pct = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    upper_pct = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    label_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    label_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_probability_level_definition", x => x.id);
                    table.CheckConstraint("ck_probability_level_definition_level", "level BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_probability_level_definition_configuration_version_configur",
                        column: x => x.configuration_version_id,
                        principalSchema: "master_data_config",
                        principalTable: "configuration_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "risk_rating_definition",
                schema: "master_data_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuration_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false),
                    label_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    label_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_risk_rating_definition", x => x.id);
                    table.ForeignKey(
                        name: "fk_risk_rating_definition_configuration_version_configuration_",
                        column: x => x.configuration_version_id,
                        principalSchema: "master_data_config",
                        principalTable: "configuration_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "approval_authority_rule",
                schema: "master_data_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuration_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_type_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    governance_profile_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    band_no = table.Column<short>(type: "smallint", nullable: true),
                    min_amount_sar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    sequence_no = table.Column<short>(type: "smallint", nullable: false),
                    approver_role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_mandatory = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approval_authority_rule", x => x.id);
                    table.CheckConstraint("ck_approval_authority_rule_band_no", "band_no BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "fk_approval_authority_rule_configuration_version_configuration",
                        column: x => x.configuration_version_id,
                        principalSchema: "master_data_config",
                        principalTable: "configuration_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_approval_authority_rule_master_data_item_governance_profile",
                        column: x => x.governance_profile_item_id,
                        principalSchema: "master_data_config",
                        principalTable: "master_data_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_approval_authority_rule_role_approver_role_id",
                        column: x => x.approver_role_id,
                        principalSchema: "identity_access",
                        principalTable: "role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evidence_requirement_rule",
                schema: "master_data_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuration_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    milestone_category_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence_type_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_mandatory = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evidence_requirement_rule", x => x.id);
                    table.ForeignKey(
                        name: "fk_evidence_requirement_rule_configuration_version_configurati",
                        column: x => x.configuration_version_id,
                        principalSchema: "master_data_config",
                        principalTable: "configuration_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_evidence_requirement_rule_master_data_item_evidence_type_it",
                        column: x => x.evidence_type_item_id,
                        principalSchema: "master_data_config",
                        principalTable: "master_data_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_evidence_requirement_rule_master_data_item_milestone_catego",
                        column: x => x.milestone_category_item_id,
                        principalSchema: "master_data_config",
                        principalTable: "master_data_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "field_classification_rule",
                schema: "master_data_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuration_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    field_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    data_classification_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    masking_rule = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_field_classification_rule", x => x.id);
                    table.CheckConstraint("ck_field_classification_rule_masking_rule", "\"masking_rule\" IN ('WITHHOLD', 'MASK', 'REVEAL')");
                    table.ForeignKey(
                        name: "fk_field_classification_rule_configuration_version_configurati",
                        column: x => x.configuration_version_id,
                        principalSchema: "master_data_config",
                        principalTable: "configuration_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_field_classification_rule_master_data_item_data_classificat",
                        column: x => x.data_classification_item_id,
                        principalSchema: "master_data_config",
                        principalTable: "master_data_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "governance_profile_setting",
                schema: "master_data_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuration_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    governance_profile_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requires_baseline_approval = table.Column<bool>(type: "boolean", nullable: false),
                    risk_management_required = table.Column<bool>(type: "boolean", nullable: false),
                    change_band_count = table.Column<short>(type: "smallint", nullable: false),
                    update_cadence_days = table.Column<int>(type: "integer", nullable: false),
                    document_control_level_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    included_in_reporting = table.Column<bool>(type: "boolean", nullable: false),
                    assignment_min_budget_sar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    assignment_min_duration_days = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_governance_profile_setting", x => x.id);
                    table.ForeignKey(
                        name: "fk_governance_profile_setting_configuration_version_configurat",
                        column: x => x.configuration_version_id,
                        principalSchema: "master_data_config",
                        principalTable: "configuration_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_governance_profile_setting_master_data_item_document_contro",
                        column: x => x.document_control_level_item_id,
                        principalSchema: "master_data_config",
                        principalTable: "master_data_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_governance_profile_setting_master_data_item_governance_prof",
                        column: x => x.governance_profile_item_id,
                        principalSchema: "master_data_config",
                        principalTable: "master_data_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "impact_level_definition",
                schema: "master_data_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuration_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    impact_dimension_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level = table.Column<short>(type: "smallint", nullable: false),
                    lower_bound = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    upper_bound = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    description_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    description_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    label_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    label_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_impact_level_definition", x => x.id);
                    table.CheckConstraint("ck_impact_level_definition_description", "(\"description_ar\" IS NULL) = (\"description_en\" IS NULL)");
                    table.CheckConstraint("ck_impact_level_definition_level", "level BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_impact_level_definition_configuration_version_configuration",
                        column: x => x.configuration_version_id,
                        principalSchema: "master_data_config",
                        principalTable: "configuration_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_impact_level_definition_master_data_item_impact_dimension_i",
                        column: x => x.impact_dimension_item_id,
                        principalSchema: "master_data_config",
                        principalTable: "master_data_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "kpi_definition",
                schema: "master_data_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    unit_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    description_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    lifecycle_state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    validated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kpi_definition", x => x.id);
                    table.CheckConstraint("ck_kpi_definition_description", "(\"description_ar\" IS NULL) = (\"description_en\" IS NULL)");
                    table.CheckConstraint("ck_kpi_definition_direction", "\"direction\" IN ('HIGHER_IS_BETTER', 'LOWER_IS_BETTER', 'TARGET_BAND')");
                    table.CheckConstraint("ck_kpi_definition_lifecycle_state", "\"lifecycle_state\" IN ('DRAFT', 'VALIDATED', 'PUBLISHED', 'RETIRED')");
                    table.ForeignKey(
                        name: "fk_kpi_definition_master_data_item_unit_item_id",
                        column: x => x.unit_item_id,
                        principalSchema: "master_data_config",
                        principalTable: "master_data_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_kpi_definition_user_published_by_user_id",
                        column: x => x.published_by_user_id,
                        principalSchema: "identity_access",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_kpi_definition_user_validated_by_user_id",
                        column: x => x.validated_by_user_id,
                        principalSchema: "identity_access",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "materiality_band",
                schema: "master_data_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuration_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    governance_profile_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    band_no = table.Column<short>(type: "smallint", nullable: false),
                    cost_threshold_pct = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    cost_threshold_sar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    schedule_threshold_pct = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    schedule_threshold_days = table.Column<int>(type: "integer", nullable: true),
                    scope_rule_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    requires_approval = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_materiality_band", x => x.id);
                    table.CheckConstraint("ck_materiality_band_band_no", "band_no BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "fk_materiality_band_configuration_version_configuration_versio",
                        column: x => x.configuration_version_id,
                        principalSchema: "master_data_config",
                        principalTable: "configuration_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_materiality_band_master_data_item_governance_profile_item_id",
                        column: x => x.governance_profile_item_id,
                        principalSchema: "master_data_config",
                        principalTable: "master_data_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "participation_contribution_rule",
                schema: "master_data_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuration_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participation_mode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    contribution_type_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_participation_contribution_rule", x => x.id);
                    table.CheckConstraint("ck_participation_contribution_rule_participation_mode", "\"participation_mode\" IN ('ENTITY_MANAGED', 'AHDA_MANAGED')");
                    table.ForeignKey(
                        name: "fk_participation_contribution_rule_configuration_version_confi",
                        column: x => x.configuration_version_id,
                        principalSchema: "master_data_config",
                        principalTable: "configuration_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_participation_contribution_rule_master_data_item_contributi",
                        column: x => x.contribution_type_item_id,
                        principalSchema: "master_data_config",
                        principalTable: "master_data_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "report_allowlist_entry",
                schema: "master_data_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuration_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_entity_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    field_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_filterable = table.Column<bool>(type: "boolean", nullable: false),
                    is_sortable = table.Column<bool>(type: "boolean", nullable: false),
                    data_classification_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    label_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    label_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_allowlist_entry", x => x.id);
                    table.ForeignKey(
                        name: "fk_report_allowlist_entry_configuration_version_configuration_",
                        column: x => x.configuration_version_id,
                        principalSchema: "master_data_config",
                        principalTable: "configuration_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_report_allowlist_entry_master_data_item_data_classification",
                        column: x => x.data_classification_item_id,
                        principalSchema: "master_data_config",
                        principalTable: "master_data_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_channel_rule",
                schema: "master_data_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_event_family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    enabled_by_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_channel_rule", x => x.id);
                    table.CheckConstraint("ck_notification_channel_rule_channel", "\"channel\" IN ('IN_APP', 'EMAIL', 'SMS')");
                    table.ForeignKey(
                        name: "fk_notification_channel_rule_notification_event_family_notific",
                        column: x => x.notification_event_family_id,
                        principalSchema: "master_data_config",
                        principalTable: "notification_event_family",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_recipient_rule",
                schema: "master_data_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_event_family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_recipient_rule", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_recipient_rule_notification_event_family_notif",
                        column: x => x.notification_event_family_id,
                        principalSchema: "master_data_config",
                        principalTable: "notification_event_family",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notification_recipient_rule_role_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity_access",
                        principalTable: "role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "risk_matrix_cell",
                schema: "master_data_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuration_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    probability_level = table.Column<short>(type: "smallint", nullable: false),
                    impact_level = table.Column<short>(type: "smallint", nullable: false),
                    risk_rating_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_risk_matrix_cell", x => x.id);
                    table.CheckConstraint("ck_risk_matrix_cell_impact_level", "impact_level BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_risk_matrix_cell_probability_level", "probability_level BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_risk_matrix_cell_configuration_version_configuration_versio",
                        column: x => x.configuration_version_id,
                        principalSchema: "master_data_config",
                        principalTable: "configuration_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_risk_matrix_cell_risk_rating_definition_risk_rating_definit",
                        column: x => x.risk_rating_definition_id,
                        principalSchema: "master_data_config",
                        principalTable: "risk_rating_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "governance_profile_mandatory_field",
                schema: "master_data_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    governance_profile_setting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_governance_profile_mandatory_field", x => x.id);
                    table.ForeignKey(
                        name: "fk_governance_profile_mandatory_field_governance_profile_setti",
                        column: x => x.governance_profile_setting_id,
                        principalSchema: "master_data_config",
                        principalTable: "governance_profile_setting",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "kpi_policy_rule",
                schema: "master_data_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuration_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kpi_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    calculation_expression = table.Column<string>(type: "text", nullable: true),
                    green_threshold = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    amber_threshold = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kpi_policy_rule", x => x.id);
                    table.ForeignKey(
                        name: "fk_kpi_policy_rule_configuration_version_configuration_version",
                        column: x => x.configuration_version_id,
                        principalSchema: "master_data_config",
                        principalTable: "configuration_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_kpi_policy_rule_kpi_definition_kpi_definition_id",
                        column: x => x.kpi_definition_id,
                        principalSchema: "master_data_config",
                        principalTable: "kpi_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_approval_authority_rule_approver_role_id",
                schema: "master_data_config",
                table: "approval_authority_rule",
                column: "approver_role_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_authority_rule_configuration_version_id",
                schema: "master_data_config",
                table: "approval_authority_rule",
                column: "configuration_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_authority_rule_governance_profile_item_id",
                schema: "master_data_config",
                table: "approval_authority_rule",
                column: "governance_profile_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_configuration_family_code",
                schema: "master_data_config",
                table: "configuration_family",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_configuration_value_configuration_version_id_value_key",
                schema: "master_data_config",
                table: "configuration_value",
                columns: new[] { "configuration_version_id", "value_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_configuration_version_configuration_family_id_version_no",
                schema: "master_data_config",
                table: "configuration_version",
                columns: new[] { "configuration_family_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_configuration_version_published_by_user_id",
                schema: "master_data_config",
                table: "configuration_version",
                column: "published_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_configuration_version_validated_by_user_id",
                schema: "master_data_config",
                table: "configuration_version",
                column: "validated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_requirement_rule_configuration_version_id_mileston",
                schema: "master_data_config",
                table: "evidence_requirement_rule",
                columns: new[] { "configuration_version_id", "milestone_category_item_id", "evidence_type_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evidence_requirement_rule_evidence_type_item_id",
                schema: "master_data_config",
                table: "evidence_requirement_rule",
                column: "evidence_type_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_requirement_rule_milestone_category_item_id",
                schema: "master_data_config",
                table: "evidence_requirement_rule",
                column: "milestone_category_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_field_classification_rule_configuration_version_id_entity_c",
                schema: "master_data_config",
                table: "field_classification_rule",
                columns: new[] { "configuration_version_id", "entity_code", "field_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_field_classification_rule_data_classification_item_id",
                schema: "master_data_config",
                table: "field_classification_rule",
                column: "data_classification_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_governance_profile_mandatory_field_governance_profile_setti",
                schema: "master_data_config",
                table: "governance_profile_mandatory_field",
                columns: new[] { "governance_profile_setting_id", "field_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_governance_profile_setting_configuration_version_id_governa",
                schema: "master_data_config",
                table: "governance_profile_setting",
                columns: new[] { "configuration_version_id", "governance_profile_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_governance_profile_setting_document_control_level_item_id",
                schema: "master_data_config",
                table: "governance_profile_setting",
                column: "document_control_level_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_governance_profile_setting_governance_profile_item_id",
                schema: "master_data_config",
                table: "governance_profile_setting",
                column: "governance_profile_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_impact_level_definition_configuration_version_id_impact_dim",
                schema: "master_data_config",
                table: "impact_level_definition",
                columns: new[] { "configuration_version_id", "impact_dimension_item_id", "level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_impact_level_definition_impact_dimension_item_id",
                schema: "master_data_config",
                table: "impact_level_definition",
                column: "impact_dimension_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_kpi_definition_code",
                schema: "master_data_config",
                table: "kpi_definition",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kpi_definition_published_by_user_id",
                schema: "master_data_config",
                table: "kpi_definition",
                column: "published_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_kpi_definition_unit_item_id",
                schema: "master_data_config",
                table: "kpi_definition",
                column: "unit_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_kpi_definition_validated_by_user_id",
                schema: "master_data_config",
                table: "kpi_definition",
                column: "validated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_kpi_policy_rule_configuration_version_id_kpi_definition_id",
                schema: "master_data_config",
                table: "kpi_policy_rule",
                columns: new[] { "configuration_version_id", "kpi_definition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kpi_policy_rule_kpi_definition_id",
                schema: "master_data_config",
                table: "kpi_policy_rule",
                column: "kpi_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_master_data_catalogue_code",
                schema: "master_data_config",
                table: "master_data_catalogue",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_master_data_item_catalogue_id_code",
                schema: "master_data_config",
                table: "master_data_item",
                columns: new[] { "catalogue_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_master_data_item_parent_item_id",
                schema: "master_data_config",
                table: "master_data_item",
                column: "parent_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_master_data_item_published_by_user_id",
                schema: "master_data_config",
                table: "master_data_item",
                column: "published_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_master_data_item_validated_by_user_id",
                schema: "master_data_config",
                table: "master_data_item",
                column: "validated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_materiality_band_configuration_version_id_governance_profil",
                schema: "master_data_config",
                table: "materiality_band",
                columns: new[] { "configuration_version_id", "governance_profile_item_id", "band_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_materiality_band_governance_profile_item_id",
                schema: "master_data_config",
                table: "materiality_band",
                column: "governance_profile_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_channel_rule_notification_event_family_id_chan",
                schema: "master_data_config",
                table: "notification_channel_rule",
                columns: new[] { "notification_event_family_id", "channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_event_family_configuration_version_id_code",
                schema: "master_data_config",
                table: "notification_event_family",
                columns: new[] { "configuration_version_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_recipient_rule_notification_event_family_id_ro",
                schema: "master_data_config",
                table: "notification_recipient_rule",
                columns: new[] { "notification_event_family_id", "role_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_recipient_rule_role_id",
                schema: "master_data_config",
                table: "notification_recipient_rule",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_participation_contribution_rule_configuration_version_id_pa",
                schema: "master_data_config",
                table: "participation_contribution_rule",
                columns: new[] { "configuration_version_id", "participation_mode", "contribution_type_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_participation_contribution_rule_contribution_type_item_id",
                schema: "master_data_config",
                table: "participation_contribution_rule",
                column: "contribution_type_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_probability_level_definition_configuration_version_id_level",
                schema: "master_data_config",
                table: "probability_level_definition",
                columns: new[] { "configuration_version_id", "level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_report_allowlist_entry_configuration_version_id_source_enti",
                schema: "master_data_config",
                table: "report_allowlist_entry",
                columns: new[] { "configuration_version_id", "source_entity_code", "field_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_report_allowlist_entry_data_classification_item_id",
                schema: "master_data_config",
                table: "report_allowlist_entry",
                column: "data_classification_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_risk_matrix_cell_configuration_version_id_probability_level",
                schema: "master_data_config",
                table: "risk_matrix_cell",
                columns: new[] { "configuration_version_id", "probability_level", "impact_level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_risk_matrix_cell_risk_rating_definition_id",
                schema: "master_data_config",
                table: "risk_matrix_cell",
                column: "risk_rating_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_risk_rating_definition_configuration_version_id_code",
                schema: "master_data_config",
                table: "risk_rating_definition",
                columns: new[] { "configuration_version_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_authority_rule",
                schema: "master_data_config");

            migrationBuilder.DropTable(
                name: "configuration_value",
                schema: "master_data_config");

            migrationBuilder.DropTable(
                name: "evidence_requirement_rule",
                schema: "master_data_config");

            migrationBuilder.DropTable(
                name: "field_classification_rule",
                schema: "master_data_config");

            migrationBuilder.DropTable(
                name: "governance_profile_mandatory_field",
                schema: "master_data_config");

            migrationBuilder.DropTable(
                name: "impact_level_definition",
                schema: "master_data_config");

            migrationBuilder.DropTable(
                name: "kpi_policy_rule",
                schema: "master_data_config");

            migrationBuilder.DropTable(
                name: "materiality_band",
                schema: "master_data_config");

            migrationBuilder.DropTable(
                name: "notification_channel_rule",
                schema: "master_data_config");

            migrationBuilder.DropTable(
                name: "notification_recipient_rule",
                schema: "master_data_config");

            migrationBuilder.DropTable(
                name: "participation_contribution_rule",
                schema: "master_data_config");

            migrationBuilder.DropTable(
                name: "probability_level_definition",
                schema: "master_data_config");

            migrationBuilder.DropTable(
                name: "report_allowlist_entry",
                schema: "master_data_config");

            migrationBuilder.DropTable(
                name: "risk_matrix_cell",
                schema: "master_data_config");

            migrationBuilder.DropTable(
                name: "governance_profile_setting",
                schema: "master_data_config");

            migrationBuilder.DropTable(
                name: "kpi_definition",
                schema: "master_data_config");

            migrationBuilder.DropTable(
                name: "notification_event_family",
                schema: "master_data_config");

            migrationBuilder.DropTable(
                name: "risk_rating_definition",
                schema: "master_data_config");

            migrationBuilder.DropTable(
                name: "master_data_item",
                schema: "master_data_config");

            migrationBuilder.DropTable(
                name: "configuration_version",
                schema: "master_data_config");

            migrationBuilder.DropTable(
                name: "master_data_catalogue",
                schema: "master_data_config");

            migrationBuilder.DropTable(
                name: "configuration_family",
                schema: "master_data_config");
        }
    }
}
