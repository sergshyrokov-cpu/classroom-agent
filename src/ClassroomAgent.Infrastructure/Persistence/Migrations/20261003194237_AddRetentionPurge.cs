using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassroomAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRetentionPurge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.AddColumn<int>(
                name: "purged_accounts",
                table: "audit_event",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "purged_audit_rows",
                table: "audit_event",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "purged_courses",
                table: "audit_event",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "purged_leaver_memberships",
                table: "audit_event",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "purged_participants",
                table: "audit_event",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_course_membership_off_roster_last_seen",
                table: "course_membership",
                column: "last_seen_at",
                filter: "on_roster = false");

            migrationBuilder.CreateIndex(
                name: "ix_audit_event_occurred_at",
                table: "audit_event",
                column: "occurred_at");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('admin_sign_in', 'workspace_connection_saved', 'access_check_run', 'dean_account_created', 'dean_account_disabled', 'dean_account_reenabled', 'dean_account_password_reset', 'dean_password_changed', 'dean_sign_in', 'retention_purge_run')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_purge_actor",
                table: "audit_event",
                sql: "action <> 'retention_purge_run' OR (actor_type = 'system' AND target_type IS NULL AND target_id IS NULL AND outcome = 'succeeded' AND request_id IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_purge_counts",
                table: "audit_event",
                sql: "(action = 'retention_purge_run') = (purged_courses IS NOT NULL AND purged_leaver_memberships IS NOT NULL AND purged_participants IS NOT NULL AND purged_accounts IS NOT NULL AND purged_audit_rows IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_purge_counts_absent",
                table: "audit_event",
                sql: "action = 'retention_purge_run' OR (purged_courses IS NULL AND purged_leaver_memberships IS NULL AND purged_participants IS NULL AND purged_accounts IS NULL AND purged_audit_rows IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_purge_counts_non_negative",
                table: "audit_event",
                sql: "(purged_courses IS NULL OR purged_courses >= 0) AND (purged_leaver_memberships IS NULL OR purged_leaver_memberships >= 0) AND (purged_participants IS NULL OR purged_participants >= 0) AND (purged_accounts IS NULL OR purged_accounts >= 0) AND (purged_audit_rows IS NULL OR purged_audit_rows >= 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_course_membership_off_roster_last_seen",
                table: "course_membership");

            migrationBuilder.DropIndex(
                name: "ix_audit_event_occurred_at",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_purge_actor",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_purge_counts",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_purge_counts_absent",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_purge_counts_non_negative",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "purged_accounts",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "purged_audit_rows",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "purged_courses",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "purged_leaver_memberships",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "purged_participants",
                table: "audit_event");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('admin_sign_in', 'workspace_connection_saved', 'access_check_run', 'dean_account_created', 'dean_account_disabled', 'dean_account_reenabled', 'dean_account_password_reset', 'dean_password_changed', 'dean_sign_in')");
        }
    }
}
