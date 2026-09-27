using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassroomAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeanAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_refusal_category_value",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_target_type_value",
                table: "audit_event");

            migrationBuilder.AddColumn<bool>(
                name: "password_is_temporary",
                table: "app_user",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('admin_sign_in', 'workspace_connection_saved', 'access_check_run', 'dean_account_created', 'dean_account_disabled', 'dean_account_reenabled', 'dean_account_password_reset', 'dean_password_changed', 'dean_sign_in')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_refusal_category_value",
                table: "audit_event",
                sql: "refusal_category IS NULL OR refusal_category IN ('not_in_allowed_admin', 'control_plane_unavailable', 'unknown_installation', 'callback_failed', 'account_disabled', 'domain_mismatch', 'impersonation_domain_mismatch', 'domain_not_confirmed', 'read_only_mode', 'connection_not_usable', 'unknown_login', 'wrong_password', 'locked_out')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_target_type_value",
                table: "audit_event",
                sql: "target_type IS NULL OR target_type IN ('workspace_connection', 'app_user')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_app_user_password_temporary",
                table: "app_user",
                sql: "password_is_temporary = false OR sign_in_method = 'password'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_refusal_category_value",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_target_type_value",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_app_user_password_temporary",
                table: "app_user");

            migrationBuilder.DropColumn(
                name: "password_is_temporary",
                table: "app_user");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('admin_sign_in', 'workspace_connection_saved', 'access_check_run')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_refusal_category_value",
                table: "audit_event",
                sql: "refusal_category IS NULL OR refusal_category IN ('not_in_allowed_admin', 'control_plane_unavailable', 'unknown_installation', 'callback_failed', 'account_disabled', 'domain_mismatch', 'impersonation_domain_mismatch', 'domain_not_confirmed', 'read_only_mode', 'connection_not_usable')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_target_type_value",
                table: "audit_event",
                sql: "target_type IS NULL OR target_type IN ('workspace_connection')");
        }
    }
}
