using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-069, 1 of 3: FG-01's five tables in the <c>dashboards</c> schema, exactly as the ERD (§5.19) has them. A definition's code is
    /// one of the three ADR-006 delivers, so the column's CHECK refuses a fourth dashboard; one version of a dashboard is on its way at a
    /// time; personalisation is the Portfolio Dashboard's alone (ADR-019); a widget names a registered projection by code and sits on a
    /// twelve-column grid. No table holds a business value (ERD §5.19, M-12). The foreign keys that leave the schema are migration 2's
    /// (README R-7).
    /// </summary>
    public partial class TASK069_CreateDashboardTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dashboard_definition",
                schema: "dashboards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    allows_personalization = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
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
                    table.PrimaryKey("pk_dashboard_definition", x => x.id);
                    table.CheckConstraint("ck_dashboard_definition_allows_personalization", "NOT allows_personalization OR code = 'PORTFOLIO'");
                    table.CheckConstraint("ck_dashboard_definition_code", "\"code\" IN ('PORTFOLIO', 'PROJECT', 'GOVERNANCE')");
                    table.CheckConstraint("ck_dashboard_definition_description", "(\"description_ar\" IS NULL) = (\"description_en\" IS NULL)");
                    table.CheckConstraint("ck_dashboard_definition_lifecycle_state", "\"lifecycle_state\" IN ('DRAFT', 'VALIDATED', 'PUBLISHED', 'RETIRED')");
                    table.CheckConstraint("ck_dashboard_definition_version_no", "version_no >= 1");
                });

            migrationBuilder.CreateTable(
                name: "dashboard_audience_role",
                schema: "dashboards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dashboard_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_default_landing = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dashboard_audience_role", x => x.id);
                    table.ForeignKey(
                        name: "fk_dashboard_audience_role_dashboard_definition_dashboard_defi",
                        column: x => x.dashboard_definition_id,
                        principalSchema: "dashboards",
                        principalTable: "dashboard_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dashboard_widget",
                schema: "dashboards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dashboard_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    widget_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_projection_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    data_classification_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_optional_visibility = table.Column<bool>(type: "boolean", nullable: false),
                    layout_row = table.Column<short>(type: "smallint", nullable: false),
                    layout_column = table.Column<short>(type: "smallint", nullable: false),
                    layout_span = table.Column<short>(type: "smallint", nullable: false),
                    title_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    title_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dashboard_widget", x => x.id);
                    table.CheckConstraint("ck_dashboard_widget_code", "code ~ '^[A-Z][A-Z0-9_]*$'");
                    table.CheckConstraint("ck_dashboard_widget_layout", "layout_row >= 1 AND layout_column BETWEEN 1 AND 12 AND layout_span BETWEEN 1 AND 12 AND layout_column + layout_span <= 13");
                    table.CheckConstraint("ck_dashboard_widget_source_projection_code", "source_projection_code ~ '^[A-Z][A-Z_]*\\.[A-Z][A-Z_]*$'");
                    table.CheckConstraint("ck_dashboard_widget_widget_type", "\"widget_type\" IN ('METRIC_CARD', 'STATUS_DISTRIBUTION', 'BAR_COLUMN', 'LINE_TREND', 'DONUT_PIE', 'PROGRESS_INDICATOR')");
                    table.ForeignKey(
                        name: "fk_dashboard_widget_dashboard_definition_dashboard_definition_",
                        column: x => x.dashboard_definition_id,
                        principalSchema: "dashboards",
                        principalTable: "dashboard_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_dashboard_preference",
                schema: "dashboards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dashboard_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_dashboard_preference", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_dashboard_preference_dashboard_definition_dashboard_de",
                        column: x => x.dashboard_definition_id,
                        principalSchema: "dashboards",
                        principalTable: "dashboard_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_dashboard_widget_preference",
                schema: "dashboards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_dashboard_preference_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dashboard_widget_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_dashboard_widget_preference", x => x.id);
                    table.CheckConstraint("ck_user_dashboard_widget_preference_sort_order", "sort_order IS NULL OR sort_order >= 1");
                    table.ForeignKey(
                        name: "fk_user_dashboard_widget_preference_dashboard_widget_dashboard",
                        column: x => x.dashboard_widget_id,
                        principalSchema: "dashboards",
                        principalTable: "dashboard_widget",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_dashboard_widget_preference_user_dashboard_preference_",
                        column: x => x.user_dashboard_preference_id,
                        principalSchema: "dashboards",
                        principalTable: "user_dashboard_preference",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dashboard_audience_role_dashboard_definition_id_role_id",
                schema: "dashboards",
                table: "dashboard_audience_role",
                columns: new[] { "dashboard_definition_id", "role_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dashboard_definition_code_version_no",
                schema: "dashboards",
                table: "dashboard_definition",
                columns: new[] { "code", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dashboard_definition_open_code",
                schema: "dashboards",
                table: "dashboard_definition",
                column: "code",
                unique: true,
                filter: "lifecycle_state IN ('DRAFT', 'VALIDATED')");

            migrationBuilder.CreateIndex(
                name: "ix_dashboard_definition_published",
                schema: "dashboards",
                table: "dashboard_definition",
                column: "code",
                filter: "lifecycle_state = 'PUBLISHED'");

            migrationBuilder.CreateIndex(
                name: "ix_dashboard_widget_dashboard_definition_id_code",
                schema: "dashboards",
                table: "dashboard_widget",
                columns: new[] { "dashboard_definition_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_dashboard_preference_dashboard_definition_id",
                schema: "dashboards",
                table: "user_dashboard_preference",
                column: "dashboard_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_dashboard_preference_user_id_dashboard_definition_id",
                schema: "dashboards",
                table: "user_dashboard_preference",
                columns: new[] { "user_id", "dashboard_definition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_dashboard_widget_preference_dashboard_widget_id",
                schema: "dashboards",
                table: "user_dashboard_widget_preference",
                column: "dashboard_widget_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_dashboard_widget_preference_user_dashboard_preference_",
                schema: "dashboards",
                table: "user_dashboard_widget_preference",
                columns: new[] { "user_dashboard_preference_id", "dashboard_widget_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dashboard_audience_role",
                schema: "dashboards");

            migrationBuilder.DropTable(
                name: "user_dashboard_widget_preference",
                schema: "dashboards");

            migrationBuilder.DropTable(
                name: "dashboard_widget",
                schema: "dashboards");

            migrationBuilder.DropTable(
                name: "user_dashboard_preference",
                schema: "dashboards");

            migrationBuilder.DropTable(
                name: "dashboard_definition",
                schema: "dashboards");
        }
    }
}
