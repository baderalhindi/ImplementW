using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK075_AddAuditForwardingInvocationForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_audit_forwarding_record_invocation_id",
                schema: "audit_activity",
                table: "audit_forwarding_record",
                column: "invocation_id");

            migrationBuilder.AddForeignKey(
                name: "fk_audit_forwarding_record_invocation_invocation_id",
                schema: "audit_activity",
                table: "audit_forwarding_record",
                column: "invocation_id",
                principalSchema: "integration_monitoring",
                principalTable: "invocation",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_audit_forwarding_record_invocation_invocation_id",
                schema: "audit_activity",
                table: "audit_forwarding_record");

            migrationBuilder.DropIndex(
                name: "ix_audit_forwarding_record_invocation_id",
                schema: "audit_activity",
                table: "audit_forwarding_record");
        }
    }
}
