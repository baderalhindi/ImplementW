using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_AddPortfolioVisuals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "portfolio_visual",
                schema: "project",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false),
                    title_ar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    title_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    visual_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    measures = table.Column<List<string>>(type: "text[]", nullable: false),
                    group_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    layout_column = table.Column<short>(type: "smallint", nullable: false),
                    layout_row = table.Column<short>(type: "smallint", nullable: false),
                    layout_width = table.Column<short>(type: "smallint", nullable: false),
                    layout_height = table.Column<short>(type: "smallint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portfolio_visual", x => x.id);
                    table.CheckConstraint("ck_portfolio_visual_group_by", "\"group_by\" IN ('NONE', 'PORTFOLIO', 'INITIATIVE', 'SECTOR', 'DEPARTMENT', 'ENTITY', 'STATUS', 'STAGE', 'ON_PLAN', 'REGION', 'CITY', 'PRIORITY')");
                    table.CheckConstraint("ck_portfolio_visual_layout", "layout_column BETWEEN 0 AND 11 AND layout_width BETWEEN 1 AND 12 AND layout_column + layout_width <= 12 AND layout_row >= 0 AND layout_height BETWEEN 1 AND 12");
                    table.CheckConstraint("ck_portfolio_visual_measures", "cardinality(measures) BETWEEN 1 AND 3 AND measures <@ ARRAY['PROJECTS', 'IN_PROGRESS', 'VALUE_SAR', 'AVERAGE_PLANNED', 'AVERAGE_ACTUAL', 'GAP_POINTS', 'NEEDING_FOLLOW_UP', 'OVERDUE_ACTIONS']::text[]");
                    table.CheckConstraint("ck_portfolio_visual_sort_order", "sort_order >= 1");
                    table.CheckConstraint("ck_portfolio_visual_visual_type", "\"visual_type\" IN ('CARD', 'BAR', 'COLUMN', 'PIE', 'DONUT', 'TABLE', 'GAUGE')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_portfolio_visual_sort_order",
                schema: "project",
                table: "portfolio_visual",
                column: "sort_order",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "portfolio_visual",
                schema: "project");
        }
    }
}
