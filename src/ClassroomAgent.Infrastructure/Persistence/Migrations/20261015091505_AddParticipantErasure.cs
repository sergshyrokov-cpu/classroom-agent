using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ClassroomAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddParticipantErasure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_meet_session_values",
                table: "meet_session");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_target_type_value",
                table: "audit_event");

            migrationBuilder.AlterColumn<string>(
                name: "organizer_email",
                table: "meet_session",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254);

            migrationBuilder.AddColumn<int>(
                name: "cleared_course_owners",
                table: "audit_event",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cleared_meet_organizers",
                table: "audit_event",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "erased_meet_participations",
                table: "audit_event",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "erased_memberships",
                table: "audit_event",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "erased_submissions",
                table: "audit_event",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "erased_participant",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    google_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    email_fingerprint = table.Column<byte[]>(type: "bytea", nullable: true),
                    erased_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_erased_participant", x => x.id);
                    table.CheckConstraint("ck_erased_participant_immutable", "updated_at = created_at");
                    table.CheckConstraint("ck_erased_participant_values", "char_length(google_user_id) >= 1 AND (email_fingerprint IS NULL OR octet_length(email_fingerprint) = 32)");
                });

            migrationBuilder.CreateTable(
                name: "erasure_salt",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    salt = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    singleton = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_erasure_salt", x => x.id);
                    table.CheckConstraint("ck_erasure_salt_immutable", "updated_at = created_at");
                    table.CheckConstraint("ck_erasure_salt_singleton", "singleton");
                    table.CheckConstraint("ck_erasure_salt_values", "octet_length(salt) = 32");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_meet_session_values",
                table: "meet_session",
                sql: "char_length(conference_id) >= 1 AND char_length(meeting_code) >= 1 AND (organizer_email IS NULL OR char_length(organizer_email) >= 3)");

            migrationBuilder.CreateIndex(
                name: "ix_course_owner_google_id",
                table: "course",
                column: "owner_google_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('admin_sign_in', 'workspace_connection_saved', 'access_check_run', 'dean_account_created', 'dean_account_disabled', 'dean_account_reenabled', 'dean_account_password_reset', 'dean_password_changed', 'dean_sign_in', 'retention_purge_run', 'synchronization_requested', 'report_template_created', 'report_template_changed', 'report_template_deleted', 'journal_exported', 'meet_code_auto_linked', 'meet_code_course_picked', 'meet_code_link_confirmed', 'meet_code_relinked', 'meet_code_marked_not_a_course', 'meet_code_mark_removed', 'meet_reports_exported', 'meet_meeting_exported', 'participant_erased', 'erasure_exclusion_lifted')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_erasure_counts",
                table: "audit_event",
                sql: "NOT (action = 'participant_erased' AND outcome = 'succeeded') OR (erased_memberships IS NOT NULL AND erased_submissions IS NOT NULL AND erased_meet_participations IS NOT NULL AND cleared_meet_organizers IS NOT NULL AND cleared_course_owners IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_erasure_counts_absent",
                table: "audit_event",
                sql: "(action = 'participant_erased' AND outcome = 'succeeded') OR (erased_memberships IS NULL AND erased_submissions IS NULL AND erased_meet_participations IS NULL AND cleared_meet_organizers IS NULL AND cleared_course_owners IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_erasure_counts_non_negative",
                table: "audit_event",
                sql: "(erased_memberships IS NULL OR erased_memberships >= 0) AND (erased_submissions IS NULL OR erased_submissions >= 0) AND (erased_meet_participations IS NULL OR erased_meet_participations >= 0) AND (cleared_meet_organizers IS NULL OR cleared_meet_organizers >= 0) AND (cleared_course_owners IS NULL OR cleared_course_owners >= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_erasure_shape",
                table: "audit_event",
                sql: "action <> 'participant_erased' OR (actor_type = 'app_user' AND actor_role = 'admin' AND (outcome = 'refused' OR (target_type = 'classroom_participant' AND target_id IS NOT NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_exclusion_lift_shape",
                table: "audit_event",
                sql: "action <> 'erasure_exclusion_lifted' OR (actor_type = 'app_user' AND actor_role = 'admin' AND (outcome = 'refused' OR (target_type = 'erased_participant' AND target_id IS NOT NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_target_type_value",
                table: "audit_event",
                sql: "target_type IS NULL OR target_type IN ('workspace_connection', 'app_user', 'report_template', 'course', 'classroom_participant', 'erased_participant')");

            migrationBuilder.CreateIndex(
                name: "ix_erased_participant_email_fingerprint",
                table: "erased_participant",
                column: "email_fingerprint");

            migrationBuilder.CreateIndex(
                name: "uq_erased_participant_google_user_id",
                table: "erased_participant",
                column: "google_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_erasure_salt_singleton",
                table: "erasure_salt",
                column: "singleton",
                unique: true);

            // US-038 db-design §4, §5: EF Core cannot express an expression index in the model, so the two lower-cased
            // email lookups of the erasure's counts, deletes and clearings are created here only (and dropped in Down).
            migrationBuilder.Sql(
                "CREATE INDEX ix_meet_session_organizer_email_lower ON meet_session (lower(organizer_email));");
            migrationBuilder.Sql(
                "CREATE INDEX ix_meet_participation_email_lower ON meet_participation (lower(email));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX ix_meet_participation_email_lower;");
            migrationBuilder.Sql("DROP INDEX ix_meet_session_organizer_email_lower;");

            migrationBuilder.DropTable(
                name: "erased_participant");

            migrationBuilder.DropTable(
                name: "erasure_salt");

            migrationBuilder.DropCheckConstraint(
                name: "ck_meet_session_values",
                table: "meet_session");

            migrationBuilder.DropIndex(
                name: "ix_course_owner_google_id",
                table: "course");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_erasure_counts",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_erasure_counts_absent",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_erasure_counts_non_negative",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_erasure_shape",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_exclusion_lift_shape",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_target_type_value",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "cleared_course_owners",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "cleared_meet_organizers",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "erased_meet_participations",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "erased_memberships",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "erased_submissions",
                table: "audit_event");

            // US-038 db-design §11: restoring NOT NULL fails once an erasure has cleared an organizer — an accepted
            // one-way step after the first erasure.
            migrationBuilder.AlterColumn<string>(
                name: "organizer_email",
                table: "meet_session",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_meet_session_values",
                table: "meet_session",
                sql: "char_length(conference_id) >= 1 AND char_length(meeting_code) >= 1 AND char_length(organizer_email) >= 3");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('admin_sign_in', 'workspace_connection_saved', 'access_check_run', 'dean_account_created', 'dean_account_disabled', 'dean_account_reenabled', 'dean_account_password_reset', 'dean_password_changed', 'dean_sign_in', 'retention_purge_run', 'synchronization_requested', 'report_template_created', 'report_template_changed', 'report_template_deleted', 'journal_exported', 'meet_code_auto_linked', 'meet_code_course_picked', 'meet_code_link_confirmed', 'meet_code_relinked', 'meet_code_marked_not_a_course', 'meet_code_mark_removed', 'meet_reports_exported', 'meet_meeting_exported')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_target_type_value",
                table: "audit_event",
                sql: "target_type IS NULL OR target_type IN ('workspace_connection', 'app_user', 'report_template', 'course')");
        }
    }
}
