using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassroomAgent.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// US-065 db-design §7: an exclusion may be entered by email before the person is loaded — the Google user id becomes
    /// optional, an exclusion still needs the id or the fingerprint, the immutability check gives way to a trigger that
    /// allows exactly the one fill of the id, and the audit log gains the entry action.
    /// </summary>
    public partial class AddErasureExclusionInAdvance : Migration
    {
        /// <summary>
        /// db-design §2.3: a row may be updated only by a missing Google user id gaining one, every other column unchanged
        /// but <c>updated_at</c>. The EF model snapshot cannot show a trigger.
        /// </summary>
        private const string CreateFillOnlyTrigger = """
            CREATE FUNCTION erased_participant_fill_only() RETURNS trigger
            LANGUAGE plpgsql AS $$
            BEGIN
                IF OLD.google_user_id IS NULL
                   AND NEW.google_user_id IS NOT NULL
                   AND NEW.id = OLD.id
                   AND NEW.email_fingerprint IS NOT DISTINCT FROM OLD.email_fingerprint
                   AND NEW.erased_at = OLD.erased_at
                   AND NEW.created_at = OLD.created_at THEN
                    RETURN NEW;
                END IF;
                RAISE EXCEPTION 'erased_participant allows only the one fill of google_user_id'
                    USING ERRCODE = 'check_violation';
            END;
            $$;

            CREATE TRIGGER tg_erased_participant_fill_only
                BEFORE UPDATE ON erased_participant
                FOR EACH ROW EXECUTE FUNCTION erased_participant_fill_only();
            """;

        private const string DropFillOnlyTrigger = """
            DROP TRIGGER tg_erased_participant_fill_only ON erased_participant;
            DROP FUNCTION erased_participant_fill_only();
            """;

        /// <summary>
        /// db-design §7 Down: an exclusion entered in advance and not yet matched cannot go back to a required id; the
        /// migration refuses rather than delete it, which would undo an erasure on request.
        /// </summary>
        private const string RefuseDownWithUnmatchedExclusions = """
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM erased_participant WHERE google_user_id IS NULL) THEN
                    RAISE EXCEPTION 'exclusions entered in advance without a Google user id exist; Down is refused';
                END IF;
            END;
            $$;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_erased_participant_immutable",
                table: "erased_participant");

            migrationBuilder.DropCheckConstraint(
                name: "ck_erased_participant_values",
                table: "erased_participant");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.AlterColumn<string>(
                name: "google_user_id",
                table: "erased_participant",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AddCheckConstraint(
                name: "ck_erased_participant_values",
                table: "erased_participant",
                sql: "(google_user_id IS NULL OR char_length(google_user_id) >= 1) AND (email_fingerprint IS NULL OR octet_length(email_fingerprint) = 32) AND (google_user_id IS NOT NULL OR email_fingerprint IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('admin_sign_in', 'workspace_connection_saved', 'access_check_run', 'dean_account_created', 'dean_account_disabled', 'dean_account_reenabled', 'dean_account_password_reset', 'dean_password_changed', 'dean_sign_in', 'retention_purge_run', 'synchronization_requested', 'report_template_created', 'report_template_changed', 'report_template_deleted', 'journal_exported', 'meet_code_auto_linked', 'meet_code_course_picked', 'meet_code_link_confirmed', 'meet_code_relinked', 'meet_code_marked_not_a_course', 'meet_code_mark_removed', 'meet_reports_exported', 'meet_meeting_exported', 'participant_erased', 'erasure_exclusion_lifted', 'courses_confirmed_deleted', 'erasure_exclusion_entered')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_exclusion_entry_shape",
                table: "audit_event",
                sql: "action <> 'erasure_exclusion_entered' OR (actor_type = 'app_user' AND actor_role = 'admin' AND (outcome = 'refused' OR (target_type = 'erased_participant' AND target_id IS NOT NULL)))");

            migrationBuilder.Sql(CreateFillOnlyTrigger);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RefuseDownWithUnmatchedExclusions);
            migrationBuilder.Sql(DropFillOnlyTrigger);

            migrationBuilder.DropCheckConstraint(
                name: "ck_erased_participant_values",
                table: "erased_participant");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_exclusion_entry_shape",
                table: "audit_event");

            migrationBuilder.AlterColumn<string>(
                name: "google_user_id",
                table: "erased_participant",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_erased_participant_immutable",
                table: "erased_participant",
                sql: "updated_at = created_at");

            migrationBuilder.AddCheckConstraint(
                name: "ck_erased_participant_values",
                table: "erased_participant",
                sql: "char_length(google_user_id) >= 1 AND (email_fingerprint IS NULL OR octet_length(email_fingerprint) = 32)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('admin_sign_in', 'workspace_connection_saved', 'access_check_run', 'dean_account_created', 'dean_account_disabled', 'dean_account_reenabled', 'dean_account_password_reset', 'dean_password_changed', 'dean_sign_in', 'retention_purge_run', 'synchronization_requested', 'report_template_created', 'report_template_changed', 'report_template_deleted', 'journal_exported', 'meet_code_auto_linked', 'meet_code_course_picked', 'meet_code_link_confirmed', 'meet_code_relinked', 'meet_code_marked_not_a_course', 'meet_code_mark_removed', 'meet_reports_exported', 'meet_meeting_exported', 'participant_erased', 'erasure_exclusion_lifted', 'courses_confirmed_deleted')");
        }
    }
}
