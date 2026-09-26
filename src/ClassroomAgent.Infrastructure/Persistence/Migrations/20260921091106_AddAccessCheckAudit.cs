using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassroomAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessCheckAudit : Migration
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

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('admin_sign_in', 'workspace_connection_saved', 'access_check_run')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_refusal_category_value",
                table: "audit_event",
                sql: "refusal_category IS NULL OR refusal_category IN ('not_in_allowed_admin', 'control_plane_unavailable', 'unknown_installation', 'callback_failed', 'account_disabled', 'domain_mismatch', 'impersonation_domain_mismatch', 'domain_not_confirmed', 'read_only_mode', 'connection_not_usable')");
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

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('admin_sign_in', 'workspace_connection_saved')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_refusal_category_value",
                table: "audit_event",
                sql: "refusal_category IS NULL OR refusal_category IN ('not_in_allowed_admin', 'control_plane_unavailable', 'unknown_installation', 'callback_failed', 'account_disabled', 'domain_mismatch', 'impersonation_domain_mismatch', 'domain_not_confirmed', 'read_only_mode')");
        }
    }
}
