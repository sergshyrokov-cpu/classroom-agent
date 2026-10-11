using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassroomAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetExportAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_export_columns_absent",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_export_shape",
                table: "audit_event");

            migrationBuilder.AddColumn<long>(
                name: "export_meeting_id",
                table: "audit_event",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "export_meetings",
                table: "audit_event",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('admin_sign_in', 'workspace_connection_saved', 'access_check_run', 'dean_account_created', 'dean_account_disabled', 'dean_account_reenabled', 'dean_account_password_reset', 'dean_password_changed', 'dean_sign_in', 'retention_purge_run', 'synchronization_requested', 'report_template_created', 'report_template_changed', 'report_template_deleted', 'journal_exported', 'meet_code_auto_linked', 'meet_code_course_picked', 'meet_code_link_confirmed', 'meet_code_relinked', 'meet_code_marked_not_a_course', 'meet_code_mark_removed', 'meet_reports_exported', 'meet_meeting_exported')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_export_columns_absent",
                table: "audit_event",
                sql: "action IN ('journal_exported', 'meet_reports_exported', 'meet_meeting_exported') OR (export_period_from IS NULL AND export_period_to IS NULL AND export_template_id IS NULL AND export_template_built_in IS NULL AND export_rows IS NULL AND export_format IS NULL AND export_meeting_id IS NULL AND export_meetings IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_export_shape",
                table: "audit_event",
                sql: "action <> 'journal_exported' OR (actor_type = 'app_user' AND target_type = 'course' AND target_id IS NOT NULL AND outcome = 'succeeded' AND export_period_from <= export_period_to AND export_rows >= 0 AND export_format IN ('xlsx') AND export_template_built_in = (export_template_id IS NULL) AND export_meeting_id IS NULL AND export_meetings IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_meet_meeting_export_shape",
                table: "audit_event",
                sql: "action <> 'meet_meeting_exported' OR (actor_type = 'app_user' AND target_type = 'course' AND target_id IS NOT NULL AND outcome = 'succeeded' AND refusal_category IS NULL AND export_meeting_id IS NOT NULL AND export_meeting_id > 0 AND export_rows IS NOT NULL AND export_rows >= 0 AND export_format = 'xlsx' AND export_period_from IS NULL AND export_period_to IS NULL AND export_meetings IS NULL AND export_template_id IS NULL AND export_template_built_in IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_meet_reports_export_shape",
                table: "audit_event",
                sql: "action <> 'meet_reports_exported' OR (actor_type = 'app_user' AND target_type = 'course' AND target_id IS NOT NULL AND outcome = 'succeeded' AND refusal_category IS NULL AND export_period_from IS NOT NULL AND export_period_to IS NOT NULL AND export_period_from <= export_period_to AND export_rows IS NOT NULL AND export_rows >= 0 AND export_meetings IS NOT NULL AND export_meetings >= 0 AND export_format = 'xlsx' AND export_meeting_id IS NULL AND export_template_id IS NULL AND export_template_built_in IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_export_columns_absent",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_export_shape",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_meet_meeting_export_shape",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_meet_reports_export_shape",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "export_meeting_id",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "export_meetings",
                table: "audit_event");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('admin_sign_in', 'workspace_connection_saved', 'access_check_run', 'dean_account_created', 'dean_account_disabled', 'dean_account_reenabled', 'dean_account_password_reset', 'dean_password_changed', 'dean_sign_in', 'retention_purge_run', 'synchronization_requested', 'report_template_created', 'report_template_changed', 'report_template_deleted', 'journal_exported', 'meet_code_auto_linked', 'meet_code_course_picked', 'meet_code_link_confirmed', 'meet_code_relinked', 'meet_code_marked_not_a_course', 'meet_code_mark_removed')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_export_columns_absent",
                table: "audit_event",
                sql: "action = 'journal_exported' OR (export_period_from IS NULL AND export_period_to IS NULL AND export_template_id IS NULL AND export_template_built_in IS NULL AND export_rows IS NULL AND export_format IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_export_shape",
                table: "audit_event",
                sql: "action <> 'journal_exported' OR (actor_type = 'app_user' AND target_type = 'course' AND target_id IS NOT NULL AND outcome = 'succeeded' AND export_period_from <= export_period_to AND export_rows >= 0 AND export_format IN ('xlsx') AND export_template_built_in = (export_template_id IS NULL))");
        }
    }
}
