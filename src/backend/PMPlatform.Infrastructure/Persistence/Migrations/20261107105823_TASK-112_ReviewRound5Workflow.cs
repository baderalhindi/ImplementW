using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_ReviewRound5Workflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE project.project DROP CONSTRAINT ck_project_declared_source; -- EXPAND-THEN-CONTRACT-REVIEWED: widened to PROGRESS_UPDATE, re-added below");

            migrationBuilder.AddColumn<Guid>(
                name: "submitted_by_user_id",
                schema: "project",
                table: "project",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "actual_source",
                schema: "progress",
                table: "progress_submission",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "WORK_BREAKDOWN");

            migrationBuilder.CreateIndex(
                name: "ix_project_submitted_by_user_id",
                schema: "project",
                table: "project",
                column: "submitted_by_user_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_project_declared_source",
                schema: "project",
                table: "project",
                sql: "\"declared_source\" IN ('IMPORT', 'MANUAL', 'ENTITY_UPDATE', 'PROGRESS_UPDATE')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_progress_submission_actual_source",
                schema: "progress",
                table: "progress_submission",
                sql: "\"actual_source\" IN ('WORK_BREAKDOWN', 'DECLARED', 'NOT_AVAILABLE')");

            migrationBuilder.AddForeignKey(
                name: "fk_project_user_submitted_by_user_id",
                schema: "project",
                table: "project",
                column: "submitted_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // A project waiting for its review keeps its submitter: the review start reads it. Nothing has written to a SUBMITTED project since its submission but that person or a follow-up contact change, which leaves no better record short of the audit trail.
            migrationBuilder.Sql("UPDATE project.project SET submitted_by_user_id = updated_by WHERE lifecycle_state = 'SUBMITTED';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_project_user_submitted_by_user_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropIndex(
                name: "ix_project_submitted_by_user_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropCheckConstraint(
                name: "ck_project_declared_source",
                schema: "project",
                table: "project");

            migrationBuilder.Sql("UPDATE project.project SET declared_source = 'MANUAL' WHERE declared_source = 'PROGRESS_UPDATE';");

            migrationBuilder.DropCheckConstraint(
                name: "ck_progress_submission_actual_source",
                schema: "progress",
                table: "progress_submission");

            migrationBuilder.DropColumn(
                name: "submitted_by_user_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "actual_source",
                schema: "progress",
                table: "progress_submission");

            migrationBuilder.AddCheckConstraint(
                name: "ck_project_declared_source",
                schema: "project",
                table: "project",
                sql: "\"declared_source\" IN ('IMPORT', 'MANUAL', 'ENTITY_UPDATE')");
        }
    }
}
