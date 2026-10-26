using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-071, 2 of 3: the foreign keys that leave the <c>reports</c> schema, to referenceable tables only (ERD D-14): a definition's reviewer and
    /// publisher, a saved view's owner and a job's requester are users; an audience row names a canonical role; a column's classification is a
    /// DATA_CLASSIFICATION item (ADR-010); and a composition's columns and filters name REPORT_RULES allowlist entries — the allowlist's enforcement
    /// in the database (ERD §5.20).
    /// </summary>
    public partial class TASK071_AddReportCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_saved_view_filter_report_allowlist_entry_id",
                schema: "reports",
                table: "saved_view_filter",
                column: "report_allowlist_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_saved_view_column_report_allowlist_entry_id",
                schema: "reports",
                table: "saved_view_column",
                column: "report_allowlist_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_definition_published_by_user_id",
                schema: "reports",
                table: "report_definition",
                column: "published_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_definition_validated_by_user_id",
                schema: "reports",
                table: "report_definition",
                column: "validated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_column_data_classification_item_id",
                schema: "reports",
                table: "report_column",
                column: "data_classification_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_audience_role_role_id",
                schema: "reports",
                table: "report_audience_role",
                column: "role_id");

            migrationBuilder.AddForeignKey(
                name: "fk_report_audience_role_role_role_id",
                schema: "reports",
                table: "report_audience_role",
                column: "role_id",
                principalSchema: "identity_access",
                principalTable: "role",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_report_column_master_data_item_data_classification_item_id",
                schema: "reports",
                table: "report_column",
                column: "data_classification_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_report_definition_user_published_by_user_id",
                schema: "reports",
                table: "report_definition",
                column: "published_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_report_definition_user_validated_by_user_id",
                schema: "reports",
                table: "report_definition",
                column: "validated_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_report_job_user_requested_by_user_id",
                schema: "reports",
                table: "report_job",
                column: "requested_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_saved_view_user_owner_user_id",
                schema: "reports",
                table: "saved_view",
                column: "owner_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_saved_view_column_report_allowlist_entry_report_allowlist_e",
                schema: "reports",
                table: "saved_view_column",
                column: "report_allowlist_entry_id",
                principalSchema: "master_data_config",
                principalTable: "report_allowlist_entry",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_saved_view_filter_report_allowlist_entry_report_allowlist_e",
                schema: "reports",
                table: "saved_view_filter",
                column: "report_allowlist_entry_id",
                principalSchema: "master_data_config",
                principalTable: "report_allowlist_entry",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_report_audience_role_role_role_id",
                schema: "reports",
                table: "report_audience_role");

            migrationBuilder.DropForeignKey(
                name: "fk_report_column_master_data_item_data_classification_item_id",
                schema: "reports",
                table: "report_column");

            migrationBuilder.DropForeignKey(
                name: "fk_report_definition_user_published_by_user_id",
                schema: "reports",
                table: "report_definition");

            migrationBuilder.DropForeignKey(
                name: "fk_report_definition_user_validated_by_user_id",
                schema: "reports",
                table: "report_definition");

            migrationBuilder.DropForeignKey(
                name: "fk_report_job_user_requested_by_user_id",
                schema: "reports",
                table: "report_job");

            migrationBuilder.DropForeignKey(
                name: "fk_saved_view_user_owner_user_id",
                schema: "reports",
                table: "saved_view");

            migrationBuilder.DropForeignKey(
                name: "fk_saved_view_column_report_allowlist_entry_report_allowlist_e",
                schema: "reports",
                table: "saved_view_column");

            migrationBuilder.DropForeignKey(
                name: "fk_saved_view_filter_report_allowlist_entry_report_allowlist_e",
                schema: "reports",
                table: "saved_view_filter");

            migrationBuilder.DropIndex(
                name: "ix_saved_view_filter_report_allowlist_entry_id",
                schema: "reports",
                table: "saved_view_filter");

            migrationBuilder.DropIndex(
                name: "ix_saved_view_column_report_allowlist_entry_id",
                schema: "reports",
                table: "saved_view_column");

            migrationBuilder.DropIndex(
                name: "ix_report_definition_published_by_user_id",
                schema: "reports",
                table: "report_definition");

            migrationBuilder.DropIndex(
                name: "ix_report_definition_validated_by_user_id",
                schema: "reports",
                table: "report_definition");

            migrationBuilder.DropIndex(
                name: "ix_report_column_data_classification_item_id",
                schema: "reports",
                table: "report_column");

            migrationBuilder.DropIndex(
                name: "ix_report_audience_role_role_id",
                schema: "reports",
                table: "report_audience_role");
        }
    }
}
