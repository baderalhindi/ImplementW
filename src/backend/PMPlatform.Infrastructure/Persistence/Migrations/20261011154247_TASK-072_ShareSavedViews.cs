using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TBC-RPT-20, enabled by TASK-072: a saved view shared with roles. The share names a canonical role (ERD D-14: a referenceable table of
    /// <c>identity_access</c>, which exists), and like a view's other parts never moves to another view (TASK-071's <c>guard_saved_view_part</c>).
    /// </summary>
    public partial class TASK072_ShareSavedViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "saved_view_share",
                schema: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    saved_view_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_with_role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saved_view_share", x => x.id);
                    table.ForeignKey(
                        name: "fk_saved_view_share_role_shared_with_role_id",
                        column: x => x.shared_with_role_id,
                        principalSchema: "identity_access",
                        principalTable: "role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_saved_view_share_saved_view_saved_view_id",
                        column: x => x.saved_view_id,
                        principalSchema: "reports",
                        principalTable: "saved_view",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_saved_view_share_saved_view_id_shared_with_role_id",
                schema: "reports",
                table: "saved_view_share",
                columns: new[] { "saved_view_id", "shared_with_role_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_saved_view_share_shared_with_role_id",
                schema: "reports",
                table: "saved_view_share",
                column: "shared_with_role_id");

            migrationBuilder.Sql("""
                CREATE TRIGGER guard_saved_view_share BEFORE UPDATE ON reports.saved_view_share
                    FOR EACH ROW EXECUTE FUNCTION reports.guard_saved_view_part();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER guard_saved_view_share ON reports.saved_view_share;");

            migrationBuilder.DropTable(
                name: "saved_view_share",
                schema: "reports");
        }
    }
}
