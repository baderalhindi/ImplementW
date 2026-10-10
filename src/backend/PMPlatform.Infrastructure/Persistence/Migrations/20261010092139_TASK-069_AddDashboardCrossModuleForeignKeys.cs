using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-069, 2 of 3: the foreign keys that leave the <c>dashboards</c> schema, to referenceable tables only (ERD D-14): a definition's
    /// reviewer and publisher and a preference's owner are users, an audience row names a canonical role, and a widget's classification is
    /// a DATA_CLASSIFICATION item (ADR-010).
    /// </summary>
    public partial class TASK069_AddDashboardCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_dashboard_widget_data_classification_item_id",
                schema: "dashboards",
                table: "dashboard_widget",
                column: "data_classification_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_dashboard_definition_published_by_user_id",
                schema: "dashboards",
                table: "dashboard_definition",
                column: "published_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_dashboard_definition_validated_by_user_id",
                schema: "dashboards",
                table: "dashboard_definition",
                column: "validated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_dashboard_audience_role_role_id",
                schema: "dashboards",
                table: "dashboard_audience_role",
                column: "role_id");

            migrationBuilder.AddForeignKey(
                name: "fk_dashboard_audience_role_role_role_id",
                schema: "dashboards",
                table: "dashboard_audience_role",
                column: "role_id",
                principalSchema: "identity_access",
                principalTable: "role",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_dashboard_definition_user_published_by_user_id",
                schema: "dashboards",
                table: "dashboard_definition",
                column: "published_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_dashboard_definition_user_validated_by_user_id",
                schema: "dashboards",
                table: "dashboard_definition",
                column: "validated_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_dashboard_widget_master_data_item_data_classification_item_",
                schema: "dashboards",
                table: "dashboard_widget",
                column: "data_classification_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_user_dashboard_preference_user_user_id",
                schema: "dashboards",
                table: "user_dashboard_preference",
                column: "user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_dashboard_audience_role_role_role_id",
                schema: "dashboards",
                table: "dashboard_audience_role");

            migrationBuilder.DropForeignKey(
                name: "fk_dashboard_definition_user_published_by_user_id",
                schema: "dashboards",
                table: "dashboard_definition");

            migrationBuilder.DropForeignKey(
                name: "fk_dashboard_definition_user_validated_by_user_id",
                schema: "dashboards",
                table: "dashboard_definition");

            migrationBuilder.DropForeignKey(
                name: "fk_dashboard_widget_master_data_item_data_classification_item_",
                schema: "dashboards",
                table: "dashboard_widget");

            migrationBuilder.DropForeignKey(
                name: "fk_user_dashboard_preference_user_user_id",
                schema: "dashboards",
                table: "user_dashboard_preference");

            migrationBuilder.DropIndex(
                name: "ix_dashboard_widget_data_classification_item_id",
                schema: "dashboards",
                table: "dashboard_widget");

            migrationBuilder.DropIndex(
                name: "ix_dashboard_definition_published_by_user_id",
                schema: "dashboards",
                table: "dashboard_definition");

            migrationBuilder.DropIndex(
                name: "ix_dashboard_definition_validated_by_user_id",
                schema: "dashboards",
                table: "dashboard_definition");

            migrationBuilder.DropIndex(
                name: "ix_dashboard_audience_role_role_id",
                schema: "dashboards",
                table: "dashboard_audience_role");
        }
    }
}
