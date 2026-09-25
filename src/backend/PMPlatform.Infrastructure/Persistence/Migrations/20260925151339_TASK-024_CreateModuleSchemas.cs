using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-024 baseline: one empty schema per module (ADR-003 M-13, ERD D-4), in erd.dbml order, so every later
    /// migration writes into a schema that already exists. <c>common</c> is not here: EF Core creates it with the
    /// history table (ERD D-17), and no Down could drop it while that table is inside it.
    /// </summary>
    public partial class TASK024_CreateModuleSchemas : Migration
    {
        // Literal, not derived from code: a migration is a record of what was applied and must not change when
        // the code does.
        private static readonly string[] ModuleSchemas =
        [
            "identity_access", "master_data_config", "project", "progress", "schedule", "project_task", "milestone",
            "risk", "management_concern", "change_request", "suspension", "closure", "approval", "document_management",
            "external_participation", "financial_kpi", "notifications", "dashboards", "reports",
            "integration_monitoring", "audit_activity",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (string schema in ModuleSchemas)
            {
                migrationBuilder.EnsureSchema(schema);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string schema in ModuleSchemas.Reverse())
            {
                migrationBuilder.DropSchema(schema);
            }
        }
    }
}
