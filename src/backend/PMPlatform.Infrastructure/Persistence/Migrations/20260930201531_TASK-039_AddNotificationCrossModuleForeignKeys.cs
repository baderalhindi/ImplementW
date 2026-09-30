using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-039, 2 of 3: the foreign keys from <c>notifications</c> to the tables of other modules that ERD D-14 makes
    /// referenceable — <c>identity_access.user</c> (recipient, preference holder, template reviewer and publisher) and
    /// <c>project.project</c> (an intent's scope) — with their indexes.
    /// </summary>
    public partial class TASK039_AddNotificationCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_notification_template_published_by_user_id",
                schema: "notifications",
                table: "notification_template",
                column: "published_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_template_validated_by_user_id",
                schema: "notifications",
                table: "notification_template",
                column: "validated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_intent_scope_project_id",
                schema: "notifications",
                table: "notification_intent",
                column: "scope_project_id");

            migrationBuilder.AddForeignKey(
                name: "fk_notification_delivery_user_recipient_user_id",
                schema: "notifications",
                table: "notification_delivery",
                column: "recipient_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_notification_intent_project_scope_project_id",
                schema: "notifications",
                table: "notification_intent",
                column: "scope_project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_notification_preference_user_user_id",
                schema: "notifications",
                table: "notification_preference",
                column: "user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_notification_template_user_published_by_user_id",
                schema: "notifications",
                table: "notification_template",
                column: "published_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_notification_template_user_validated_by_user_id",
                schema: "notifications",
                table: "notification_template",
                column: "validated_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_notification_delivery_user_recipient_user_id",
                schema: "notifications",
                table: "notification_delivery");

            migrationBuilder.DropForeignKey(
                name: "fk_notification_intent_project_scope_project_id",
                schema: "notifications",
                table: "notification_intent");

            migrationBuilder.DropForeignKey(
                name: "fk_notification_preference_user_user_id",
                schema: "notifications",
                table: "notification_preference");

            migrationBuilder.DropForeignKey(
                name: "fk_notification_template_user_published_by_user_id",
                schema: "notifications",
                table: "notification_template");

            migrationBuilder.DropForeignKey(
                name: "fk_notification_template_user_validated_by_user_id",
                schema: "notifications",
                table: "notification_template");

            migrationBuilder.DropIndex(
                name: "ix_notification_template_published_by_user_id",
                schema: "notifications",
                table: "notification_template");

            migrationBuilder.DropIndex(
                name: "ix_notification_template_validated_by_user_id",
                schema: "notifications",
                table: "notification_template");

            migrationBuilder.DropIndex(
                name: "ix_notification_intent_scope_project_id",
                schema: "notifications",
                table: "notification_intent");
        }
    }
}
