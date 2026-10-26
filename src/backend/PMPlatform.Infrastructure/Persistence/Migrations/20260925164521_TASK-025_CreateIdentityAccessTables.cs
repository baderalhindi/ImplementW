using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-025, 1 of 4: the nine FG-03 tables of <c>identity_access</c> (erd.dbml), with every foreign key inside the
    /// schema. The three that leave it — to <c>master_data_config.master_data_item</c> and <c>project.project</c> —
    /// are added by <see cref="TASK025_AddIdentityAccessCrossModuleForeignKeys"/> once those tables exist, because each
    /// migration touches one module's schema (README R-7). The schema itself is TASK-024's.
    /// </summary>
    public partial class TASK025_CreateIdentityAccessTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "department",
                schema: "identity_access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    parent_department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    directory_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_department", x => x.id);
                    table.ForeignKey(
                        name: "fk_department_department_parent_department_id",
                        column: x => x.parent_department_id,
                        principalSchema: "identity_access",
                        principalTable: "department",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "permission",
                schema: "identity_access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    permission_group = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    data_classification_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_privileged = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permission", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "role",
                schema: "identity_access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_external_eligible = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permission_profile",
                schema: "identity_access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    base_role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_shipped_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permission_profile", x => x.id);
                    table.ForeignKey(
                        name: "fk_permission_profile_role_base_role_id",
                        column: x => x.base_role_id,
                        principalSchema: "identity_access",
                        principalTable: "role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "access_relationship",
                schema: "identity_access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_profile_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    external_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sponsor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    end_reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_access_relationship", x => x.id);
                    table.CheckConstraint("ck_access_relationship_end_reason", "\"end_reason\" IN ('PROJECT_CLOSED', 'ROLE_CHANGE', 'MIGRATED', 'MANUAL', 'EXPIRED')");
                    table.CheckConstraint("ck_access_relationship_status", "\"status\" IN ('ACTIVE', 'ENDED')");
                    table.ForeignKey(
                        name: "fk_access_relationship_department_department_id",
                        column: x => x.department_id,
                        principalSchema: "identity_access",
                        principalTable: "department",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "external_entity",
                schema: "identity_access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_type_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sponsor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_entity", x => x.id);
                    table.CheckConstraint("ck_external_entity_status", "\"status\" IN ('ACTIVE', 'SUSPENDED', 'RETIRED')");
                });

            migrationBuilder.CreateTable(
                name: "user",
                schema: "identity_access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    directory_subject_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mobile_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    mobile_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    job_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    manager_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    external_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    preferred_language = table.Column<string>(type: "char(2)", nullable: false, defaultValue: "ar"),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    disabled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    mfa_enrolled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    nafath_verification_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    nafath_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user", x => x.id);
                    table.CheckConstraint("ck_user_external_entity", "user_type <> 'EXTERNAL' OR external_entity_id IS NOT NULL");
                    table.CheckConstraint("ck_user_mobile_number", "mobile_number ~ '^\\+[1-9][0-9]{1,14}$'");
                    table.CheckConstraint("ck_user_preferred_language", "\"preferred_language\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_user_status", "\"status\" IN ('ACTIVE', 'DISABLED')");
                    table.CheckConstraint("ck_user_user_type", "\"user_type\" IN ('INTERNAL', 'EXTERNAL', 'SERVICE')");
                    table.ForeignKey(
                        name: "fk_user_department_department_id",
                        column: x => x.department_id,
                        principalSchema: "identity_access",
                        principalTable: "department",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_external_entity_external_entity_id",
                        column: x => x.external_entity_id,
                        principalSchema: "identity_access",
                        principalTable: "external_entity",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_user_manager_user_id",
                        column: x => x.manager_user_id,
                        principalSchema: "identity_access",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "permission_profile_version",
                schema: "identity_access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    change_summary_lang = table.Column<string>(type: "char(2)", nullable: true),
                    change_summary = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("pk_permission_profile_version", x => x.id);
                    table.CheckConstraint("ck_permission_profile_version_change_summary_lang", "\"change_summary_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_permission_profile_version_change_summary_pair", "(\"change_summary\" IS NULL) = (\"change_summary_lang\" IS NULL)");
                    table.CheckConstraint("ck_permission_profile_version_lifecycle_state", "\"lifecycle_state\" IN ('DRAFT', 'VALIDATED', 'PUBLISHED', 'RETIRED')");
                    table.ForeignKey(
                        name: "fk_permission_profile_version_permission_profile_permission_pr",
                        column: x => x.permission_profile_id,
                        principalSchema: "identity_access",
                        principalTable: "permission_profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_permission_profile_version_user_published_by_user_id",
                        column: x => x.published_by_user_id,
                        principalSchema: "identity_access",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_permission_profile_version_user_validated_by_user_id",
                        column: x => x.validated_by_user_id,
                        principalSchema: "identity_access",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "permission_profile_grant",
                schema: "identity_access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_profile_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permission_profile_grant", x => x.id);
                    table.CheckConstraint("ck_permission_profile_grant_data_scope", "\"data_scope\" IN ('ALL', 'DEPT', 'OWN', 'ASSIGNED', 'ENTITY', 'READ_ONLY')");
                    table.ForeignKey(
                        name: "fk_permission_profile_grant_permission_permission_id",
                        column: x => x.permission_id,
                        principalSchema: "identity_access",
                        principalTable: "permission",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_permission_profile_grant_permission_profile_version_permiss",
                        column: x => x.permission_profile_version_id,
                        principalSchema: "identity_access",
                        principalTable: "permission_profile_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_access_relationship_department_id",
                schema: "identity_access",
                table: "access_relationship",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_access_relationship_external_entity_id",
                schema: "identity_access",
                table: "access_relationship",
                column: "external_entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_access_relationship_permission_profile_version_id",
                schema: "identity_access",
                table: "access_relationship",
                column: "permission_profile_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_access_relationship_sponsor_user_id",
                schema: "identity_access",
                table: "access_relationship",
                column: "sponsor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_access_relationship_user_id",
                schema: "identity_access",
                table: "access_relationship",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_department_code",
                schema: "identity_access",
                table: "department",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_department_parent_department_id",
                schema: "identity_access",
                table: "department",
                column: "parent_department_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_entity_code",
                schema: "identity_access",
                table: "external_entity",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_external_entity_sponsor_user_id",
                schema: "identity_access",
                table: "external_entity",
                column: "sponsor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_permission_code",
                schema: "identity_access",
                table: "permission",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_permission_profile_base_role_id",
                schema: "identity_access",
                table: "permission_profile",
                column: "base_role_id");

            migrationBuilder.CreateIndex(
                name: "ix_permission_profile_code",
                schema: "identity_access",
                table: "permission_profile",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_permission_profile_grant_permission_id",
                schema: "identity_access",
                table: "permission_profile_grant",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "ix_permission_profile_grant_permission_profile_version_id_perm",
                schema: "identity_access",
                table: "permission_profile_grant",
                columns: new[] { "permission_profile_version_id", "permission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_permission_profile_version_permission_profile_id_version_no",
                schema: "identity_access",
                table: "permission_profile_version",
                columns: new[] { "permission_profile_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_permission_profile_version_published_by_user_id",
                schema: "identity_access",
                table: "permission_profile_version",
                column: "published_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_permission_profile_version_validated_by_user_id",
                schema: "identity_access",
                table: "permission_profile_version",
                column: "validated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_code",
                schema: "identity_access",
                table: "role",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_department_id",
                schema: "identity_access",
                table: "user",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_directory_subject_id",
                schema: "identity_access",
                table: "user",
                column: "directory_subject_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_email",
                schema: "identity_access",
                table: "user",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_external_entity_id",
                schema: "identity_access",
                table: "user",
                column: "external_entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_manager_user_id",
                schema: "identity_access",
                table: "user",
                column: "manager_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_username",
                schema: "identity_access",
                table: "user",
                column: "username",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_access_relationship_external_entity_external_entity_id",
                schema: "identity_access",
                table: "access_relationship",
                column: "external_entity_id",
                principalSchema: "identity_access",
                principalTable: "external_entity",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_access_relationship_permission_profile_version_permission_p",
                schema: "identity_access",
                table: "access_relationship",
                column: "permission_profile_version_id",
                principalSchema: "identity_access",
                principalTable: "permission_profile_version",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_access_relationship_user_sponsor_user_id",
                schema: "identity_access",
                table: "access_relationship",
                column: "sponsor_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_access_relationship_user_user_id",
                schema: "identity_access",
                table: "access_relationship",
                column: "user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_external_entity_user_sponsor_user_id",
                schema: "identity_access",
                table: "external_entity",
                column: "sponsor_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_user_department_department_id",
                schema: "identity_access",
                table: "user");

            migrationBuilder.DropForeignKey(
                name: "fk_user_external_entity_external_entity_id",
                schema: "identity_access",
                table: "user");

            migrationBuilder.DropTable(
                name: "access_relationship",
                schema: "identity_access");

            migrationBuilder.DropTable(
                name: "permission_profile_grant",
                schema: "identity_access");

            migrationBuilder.DropTable(
                name: "permission",
                schema: "identity_access");

            migrationBuilder.DropTable(
                name: "permission_profile_version",
                schema: "identity_access");

            migrationBuilder.DropTable(
                name: "permission_profile",
                schema: "identity_access");

            migrationBuilder.DropTable(
                name: "role",
                schema: "identity_access");

            migrationBuilder.DropTable(
                name: "department",
                schema: "identity_access");

            migrationBuilder.DropTable(
                name: "external_entity",
                schema: "identity_access");

            migrationBuilder.DropTable(
                name: "user",
                schema: "identity_access");
        }
    }
}
