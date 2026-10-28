using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassroomAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDataHealth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "course_list_empty_since",
                table: "sync_state",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long[]>(
                name: "confirmed_course_ids",
                table: "audit_event",
                type: "bigint[]",
                nullable: true);

            // US-062 FR-008.2, OD-002 a, db-design §2: a row flagged before this Story is dated by its last run's start —
            // the latest certain instant — before the CHECK makes flag and date agree.
            migrationBuilder.Sql(
                "UPDATE sync_state SET course_list_empty_since = started_at WHERE last_course_list_empty;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sync_state_course_list_empty_since",
                table: "sync_state",
                sql: "last_course_list_empty = (course_list_empty_since IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_submission_graded",
                table: "submission",
                column: "course_work_id",
                filter: "assigned_grade IS NOT NULL OR draft_grade IS NOT NULL")
                .Annotation("Npgsql:IndexInclude", new[] { "assigned_grade", "draft_grade" });

            migrationBuilder.CreateIndex(
                name: "ix_course_work_not_in_classroom",
                table: "course_work",
                column: "course_id",
                filter: "not_in_classroom_since IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('admin_sign_in', 'workspace_connection_saved', 'access_check_run', 'dean_account_created', 'dean_account_disabled', 'dean_account_reenabled', 'dean_account_password_reset', 'dean_password_changed', 'dean_sign_in', 'retention_purge_run', 'synchronization_requested', 'report_template_created', 'report_template_changed', 'report_template_deleted', 'journal_exported', 'meet_code_auto_linked', 'meet_code_course_picked', 'meet_code_link_confirmed', 'meet_code_relinked', 'meet_code_marked_not_a_course', 'meet_code_mark_removed', 'meet_reports_exported', 'meet_meeting_exported', 'participant_erased', 'erasure_exclusion_lifted', 'courses_confirmed_deleted')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_confirmed_course_ids_absent",
                table: "audit_event",
                sql: "action = 'courses_confirmed_deleted' OR confirmed_course_ids IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_course_deletion_shape",
                table: "audit_event",
                sql: "action <> 'courses_confirmed_deleted' OR (actor_type = 'app_user' AND actor_role = 'admin' AND target_type IS NULL AND target_id IS NULL AND ((outcome = 'succeeded' AND refusal_category IS NULL AND confirmed_course_ids IS NOT NULL AND cardinality(confirmed_course_ids) BETWEEN 1 AND 1000) OR (outcome = 'refused' AND refusal_category = 'read_only_mode' AND confirmed_course_ids IS NULL)))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_sync_state_course_list_empty_since",
                table: "sync_state");

            migrationBuilder.DropIndex(
                name: "ix_submission_graded",
                table: "submission");

            migrationBuilder.DropIndex(
                name: "ix_course_work_not_in_classroom",
                table: "course_work");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_confirmed_course_ids_absent",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_course_deletion_shape",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "course_list_empty_since",
                table: "sync_state");

            migrationBuilder.DropColumn(
                name: "confirmed_course_ids",
                table: "audit_event");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('admin_sign_in', 'workspace_connection_saved', 'access_check_run', 'dean_account_created', 'dean_account_disabled', 'dean_account_reenabled', 'dean_account_password_reset', 'dean_password_changed', 'dean_sign_in', 'retention_purge_run', 'synchronization_requested', 'report_template_created', 'report_template_changed', 'report_template_deleted', 'journal_exported', 'meet_code_auto_linked', 'meet_code_course_picked', 'meet_code_link_confirmed', 'meet_code_relinked', 'meet_code_marked_not_a_course', 'meet_code_mark_removed', 'meet_reports_exported', 'meet_meeting_exported', 'participant_erased', 'erasure_exclusion_lifted')");
        }
    }
}
