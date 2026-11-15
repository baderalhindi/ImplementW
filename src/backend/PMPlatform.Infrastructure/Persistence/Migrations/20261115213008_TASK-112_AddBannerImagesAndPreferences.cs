using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_AddBannerImagesAndPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "banner_image",
                schema: "identity_access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    caption_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    caption_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alt_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    alt_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    focal_x = table.Column<short>(type: "smallint", nullable: false),
                    focal_y = table.Column<short>(type: "smallint", nullable: false),
                    credit = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    content_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_banner_image", x => x.id);
                    table.CheckConstraint("ck_banner_image_content_size", "octet_length(content) <= 307200");
                    table.CheckConstraint("ck_banner_image_focal_point", "focal_x BETWEEN 0 AND 100 AND focal_y BETWEEN 0 AND 100");
                });

            migrationBuilder.CreateTable(
                name: "user_banner_preference",
                schema: "identity_access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    style = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    image_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_banner_preference", x => x.id);
                    table.CheckConstraint("ck_user_banner_preference_style", "\"style\" IN ('DEFAULT', 'IMAGE', 'ROTATE')");
                    table.ForeignKey(
                        name: "fk_user_banner_preference_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity_access",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_banner_image_code",
                schema: "identity_access",
                table: "banner_image",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_banner_preference_user_id",
                schema: "identity_access",
                table: "user_banner_preference",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "banner_image",
                schema: "identity_access");

            migrationBuilder.DropTable(
                name: "user_banner_preference",
                schema: "identity_access");
        }
    }
}
