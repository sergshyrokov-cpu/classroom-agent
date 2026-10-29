using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassroomAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeanLoginChangedAuditAction : Migration
    {
        /// <summary>
        /// US-067 db-design §4 Down: audit rows are never deleted (SC-11) and the earlier list cannot be re-added over a
        /// <c>dean_login_changed</c> row, so the migration refuses rather than lose or break them.
        /// </summary>
        private const string RefuseDownWithLoginChangeRows = """
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM audit_event WHERE action = 'dean_login_changed') THEN
                    RAISE EXCEPTION 'dean_login_changed audit rows exist; Down is refused';
                END IF;
            END;
            $$;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('admin_sign_in', 'workspace_connection_saved', 'access_check_run', 'dean_account_created', 'dean_account_disabled', 'dean_account_reenabled', 'dean_account_password_reset', 'dean_password_changed', 'dean_sign_in', 'retention_purge_run', 'synchronization_requested', 'report_template_created', 'report_template_changed', 'report_template_deleted', 'journal_exported', 'meet_code_auto_linked', 'meet_code_course_picked', 'meet_code_link_confirmed', 'meet_code_relinked', 'meet_code_marked_not_a_course', 'meet_code_mark_removed', 'meet_reports_exported', 'meet_meeting_exported', 'participant_erased', 'erasure_exclusion_lifted', 'courses_confirmed_deleted', 'erasure_exclusion_entered', 'dean_login_changed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RefuseDownWithLoginChangeRows);

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('admin_sign_in', 'workspace_connection_saved', 'access_check_run', 'dean_account_created', 'dean_account_disabled', 'dean_account_reenabled', 'dean_account_password_reset', 'dean_password_changed', 'dean_sign_in', 'retention_purge_run', 'synchronization_requested', 'report_template_created', 'report_template_changed', 'report_template_deleted', 'journal_exported', 'meet_code_auto_linked', 'meet_code_course_picked', 'meet_code_link_confirmed', 'meet_code_relinked', 'meet_code_marked_not_a_course', 'meet_code_mark_removed', 'meet_reports_exported', 'meet_meeting_exported', 'participant_erased', 'erasure_exclusion_lifted', 'courses_confirmed_deleted', 'erasure_exclusion_entered')");
        }
    }
}
