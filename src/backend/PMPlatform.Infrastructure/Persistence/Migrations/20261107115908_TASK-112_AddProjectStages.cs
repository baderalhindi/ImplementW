using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_AddProjectStages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_stage_definition",
                schema: "master_data_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuration_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false),
                    weight_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_stage_definition", x => x.id);
                    table.CheckConstraint("ck_project_stage_definition_weight_percent", "weight_percent BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "fk_project_stage_definition_configuration_version_configuratio",
                        column: x => x.configuration_version_id,
                        principalSchema: "master_data_config",
                        principalTable: "configuration_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_project_stage_definition_configuration_version_id_code",
                schema: "master_data_config",
                table: "project_stage_definition",
                columns: new[] { "configuration_version_id", "code" },
                unique: true);

            // A published version's rows never change, as every other family's (TASK-034).
            migrationBuilder.Sql("""
                CREATE TRIGGER guard_configuration_content
                    BEFORE INSERT OR UPDATE OR DELETE ON master_data_config.project_stage_definition
                    FOR EACH ROW EXECUTE FUNCTION master_data_config.guard_configuration_content();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_stage_definition",
                schema: "master_data_config");
        }
    }
}
