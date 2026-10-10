using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ClassroomAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingCodeLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_sync_state_failed_step",
                table: "sync_state");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.AddColumn<string>(
                name: "meet_code",
                table: "audit_event",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "meet_previous_course_id",
                table: "audit_event",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "purged_meet_code_links",
                table: "audit_event",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "meeting_code_link",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    meeting_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    course_id = table.Column<long>(type: "bigint", nullable: true),
                    linked_automatically = table.Column<bool>(type: "boolean", nullable: true),
                    linked_by_app_user_id = table.Column<long>(type: "bigint", nullable: true),
                    linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_by_app_user_id = table.Column<long>(type: "bigint", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    marked_by_app_user_id = table.Column<long>(type: "bigint", nullable: true),
                    marked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_code_link", x => x.id);
                    table.UniqueConstraint("uq_meeting_code_link_meeting_code", x => x.meeting_code);
                    table.CheckConstraint("ck_meeting_code_link_confirmation", "(confirmed_by_app_user_id IS NULL) = (confirmed_at IS NULL) AND (confirmed_at IS NULL OR linked_automatically)");
                    table.CheckConstraint("ck_meeting_code_link_maker", "linked_automatically IS NULL OR linked_automatically = (linked_by_app_user_id IS NULL)");
                    table.CheckConstraint("ck_meeting_code_link_state", "(course_id IS NOT NULL AND linked_automatically IS NOT NULL AND linked_at IS NOT NULL AND marked_by_app_user_id IS NULL AND marked_at IS NULL) OR (course_id IS NULL AND linked_automatically IS NULL AND linked_at IS NULL AND linked_by_app_user_id IS NULL AND confirmed_by_app_user_id IS NULL AND confirmed_at IS NULL AND marked_by_app_user_id IS NOT NULL AND marked_at IS NOT NULL)");
                    table.CheckConstraint("ck_meeting_code_link_values", "char_length(meeting_code) >= 1 AND char_length(concurrency_stamp) >= 1");
                    table.ForeignKey(
                        name: "fk_meeting_code_link_course_id",
                        column: x => x.course_id,
                        principalTable: "course",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_sync_state_failed_step",
                table: "sync_state",
                sql: "failed_step IS NULL OR (status = 'failed' AND failed_step IN ('classroom', 'meet', 'linking'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('admin_sign_in', 'workspace_connection_saved', 'access_check_run', 'dean_account_created', 'dean_account_disabled', 'dean_account_reenabled', 'dean_account_password_reset', 'dean_password_changed', 'dean_sign_in', 'retention_purge_run', 'synchronization_requested', 'report_template_created', 'report_template_changed', 'report_template_deleted', 'journal_exported', 'meet_code_auto_linked', 'meet_code_course_picked', 'meet_code_link_confirmed', 'meet_code_relinked', 'meet_code_marked_not_a_course', 'meet_code_mark_removed')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_meet_code_absent",
                table: "audit_event",
                sql: "action IN ('meet_code_auto_linked', 'meet_code_course_picked', 'meet_code_link_confirmed', 'meet_code_relinked', 'meet_code_marked_not_a_course', 'meet_code_mark_removed') OR (meet_code IS NULL AND meet_previous_course_id IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_meet_code_shape",
                table: "audit_event",
                sql: "action NOT IN ('meet_code_auto_linked', 'meet_code_course_picked', 'meet_code_link_confirmed', 'meet_code_relinked', 'meet_code_marked_not_a_course', 'meet_code_mark_removed') OR ( (actor_type = 'system') = (action = 'meet_code_auto_linked') AND (action <> 'meet_code_auto_linked' OR (outcome = 'succeeded' AND request_id IS NULL)) AND (refusal_category IS NULL OR refusal_category = 'read_only_mode') AND (target_type IS NULL OR target_type = 'course') AND (target_type IS NULL) = (target_id IS NULL) AND (outcome = 'succeeded' OR (target_id IS NULL AND meet_previous_course_id IS NULL)) AND (outcome <> 'succeeded' OR (meet_code IS NOT NULL AND (target_id IS NULL) = (action = 'meet_code_marked_not_a_course'))) AND (meet_previous_course_id IS NULL OR action IN ('meet_code_relinked', 'meet_code_marked_not_a_course')) AND (action <> 'meet_code_relinked' OR outcome <> 'succeeded' OR meet_previous_course_id IS NOT NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_meet_code_values",
                table: "audit_event",
                sql: "(meet_code IS NULL OR char_length(meet_code) >= 1) AND (meet_previous_course_id IS NULL OR meet_previous_course_id > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_purge_meet_code_links",
                table: "audit_event",
                sql: "(purged_meet_code_links IS NULL OR (action = 'retention_purge_run' AND purged_meet_code_links >= 0))");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_code_link_course_id",
                table: "meeting_code_link",
                column: "course_id");

            // US-032 db-design §5.2: EF Core cannot express an expression index in the model, so it is created here
            // only (and dropped in Down); the case-insensitive email lookup of the scoring reads uses it.
            migrationBuilder.Sql(
                "CREATE INDEX ix_classroom_participant_email_lower ON classroom_participant (lower(email));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX ix_classroom_participant_email_lower;");

            migrationBuilder.DropTable(
                name: "meeting_code_link");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sync_state_failed_step",
                table: "sync_state");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_meet_code_absent",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_meet_code_shape",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_meet_code_values",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_purge_meet_code_links",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "meet_code",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "meet_previous_course_id",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "purged_meet_code_links",
                table: "audit_event");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sync_state_failed_step",
                table: "sync_state",
                sql: "failed_step IS NULL OR (status = 'failed' AND failed_step IN ('classroom', 'meet'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('admin_sign_in', 'workspace_connection_saved', 'access_check_run', 'dean_account_created', 'dean_account_disabled', 'dean_account_reenabled', 'dean_account_password_reset', 'dean_password_changed', 'dean_sign_in', 'retention_purge_run', 'synchronization_requested', 'report_template_created', 'report_template_changed', 'report_template_deleted', 'journal_exported')");
        }
    }
}
