using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-025, 3 of 4: the WF-01 tables of <c>project</c> (erd.dbml): the Project master aggregate with its unique
    /// Formal Project ID, and the ADR-014 intake declaration. The schema itself is TASK-024's.
    /// </summary>
    public partial class TASK025_CreateProjectTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project",
                schema: "project",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    formal_project_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    classification_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    project_manager_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lifecycle_state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    governance_profile_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    governance_profile_overridden = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    participation_mode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    registration_budget_sar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    planned_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    planned_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    region_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    city_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    legacy_intake_date = table.Column<DateOnly>(type: "date", nullable: true),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    description_lang = table.Column<string>(type: "char(2)", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    governance_profile_override_reason_lang = table.Column<string>(type: "char(2)", nullable: true),
                    governance_profile_override_reason = table.Column<string>(type: "text", nullable: true),
                    title_lang = table.Column<string>(type: "char(2)", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project", x => x.id);
                    table.CheckConstraint("ck_project_description_lang", "\"description_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_project_description_pair", "(\"description\" IS NULL) = (\"description_lang\" IS NULL)");
                    table.CheckConstraint("ck_project_formal_project_id", "lifecycle_state IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED') OR formal_project_id IS NOT NULL");
                    table.CheckConstraint("ck_project_governance_profile_override_reason_lang", "\"governance_profile_override_reason_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_project_governance_profile_override_reason_pair", "(\"governance_profile_override_reason\" IS NULL) = (\"governance_profile_override_reason_lang\" IS NULL)");
                    table.CheckConstraint("ck_project_lifecycle_state", "\"lifecycle_state\" IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED', 'APPROVED_PLANNED', 'ACTIVE', 'SUSPENDED', 'COMPLETED', 'CLOSED')");
                    table.CheckConstraint("ck_project_participation_mode", "\"participation_mode\" IN ('ENTITY_MANAGED', 'AHDA_MANAGED')");
                    table.CheckConstraint("ck_project_title_lang", "\"title_lang\" IN ('ar', 'en')");
                    table.ForeignKey(
                        name: "fk_project_department_department_id",
                        column: x => x.department_id,
                        principalSchema: "identity_access",
                        principalTable: "department",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_external_entity_external_entity_id",
                        column: x => x.external_entity_id,
                        principalSchema: "identity_access",
                        principalTable: "external_entity",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_master_data_item_city_item_id",
                        column: x => x.city_item_id,
                        principalSchema: "master_data_config",
                        principalTable: "master_data_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_master_data_item_classification_item_id",
                        column: x => x.classification_item_id,
                        principalSchema: "master_data_config",
                        principalTable: "master_data_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_master_data_item_governance_profile_item_id",
                        column: x => x.governance_profile_item_id,
                        principalSchema: "master_data_config",
                        principalTable: "master_data_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_master_data_item_region_item_id",
                        column: x => x.region_item_id,
                        principalSchema: "master_data_config",
                        principalTable: "master_data_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_user_project_manager_user_id",
                        column: x => x.project_manager_user_id,
                        principalSchema: "identity_access",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_intake",
                schema: "project",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    intake_date = table.Column<DateOnly>(type: "date", nullable: false),
                    declared_budget_sar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    declared_end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    opening_percent_complete = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    opening_spend_to_date_sar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    declared_scope_lang = table.Column<string>(type: "char(2)", nullable: false),
                    declared_scope = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_intake", x => x.id);
                    table.CheckConstraint("ck_project_intake_append_only", "updated_at = created_at AND updated_by = created_by");
                    table.CheckConstraint("ck_project_intake_declared_scope_lang", "\"declared_scope_lang\" IN ('ar', 'en')");
                    table.ForeignKey(
                        name: "fk_project_intake_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "project",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_intake_user_recorded_by_user_id",
                        column: x => x.recorded_by_user_id,
                        principalSchema: "identity_access",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_intake_milestone",
                schema: "project",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_intake_id = table.Column<Guid>(type: "uuid", nullable: false),
                    achieved_date = table.Column<DateOnly>(type: "date", nullable: false),
                    title_lang = table.Column<string>(type: "char(2)", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_intake_milestone", x => x.id);
                    table.CheckConstraint("ck_project_intake_milestone_append_only", "updated_at = created_at AND updated_by = created_by");
                    table.CheckConstraint("ck_project_intake_milestone_title_lang", "\"title_lang\" IN ('ar', 'en')");
                    table.ForeignKey(
                        name: "fk_project_intake_milestone_project_intake_project_intake_id",
                        column: x => x.project_intake_id,
                        principalSchema: "project",
                        principalTable: "project_intake",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_project_city_item_id",
                schema: "project",
                table: "project",
                column: "city_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_classification_item_id",
                schema: "project",
                table: "project",
                column: "classification_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_department_id",
                schema: "project",
                table: "project",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_external_entity_id",
                schema: "project",
                table: "project",
                column: "external_entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_formal_project_id",
                schema: "project",
                table: "project",
                column: "formal_project_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_governance_profile_item_id",
                schema: "project",
                table: "project",
                column: "governance_profile_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_project_manager_user_id",
                schema: "project",
                table: "project",
                column: "project_manager_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_region_item_id",
                schema: "project",
                table: "project",
                column: "region_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_intake_project_id",
                schema: "project",
                table: "project_intake",
                column: "project_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_intake_recorded_by_user_id",
                schema: "project",
                table: "project_intake",
                column: "recorded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_intake_milestone_project_intake_id",
                schema: "project",
                table: "project_intake_milestone",
                column: "project_intake_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_intake_milestone",
                schema: "project");

            migrationBuilder.DropTable(
                name: "project_intake",
                schema: "project");

            migrationBuilder.DropTable(
                name: "project",
                schema: "project");
        }
    }
}
