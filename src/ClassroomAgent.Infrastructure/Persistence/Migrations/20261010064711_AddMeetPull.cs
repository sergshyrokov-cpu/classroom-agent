using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ClassroomAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetPull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "failed_step",
                table: "sync_state",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "meet_loaded_up_to",
                table: "sync_state",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "purged_meet_participations",
                table: "audit_event",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "purged_meet_sessions",
                table: "audit_event",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "meet_session",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    conference_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    meeting_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    organizer_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meet_session", x => x.id);
                    table.CheckConstraint("ck_meet_session_ended_after_started", "ended_at >= started_at");
                    table.CheckConstraint("ck_meet_session_values", "char_length(conference_id) >= 1 AND char_length(meeting_code) >= 1 AND char_length(organizer_email) >= 3");
                });

            migrationBuilder.CreateTable(
                name: "meet_participation",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    meet_session_id = table.Column<long>(type: "bigint", nullable: false),
                    endpoint_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duration_seconds = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meet_participation", x => x.id);
                    table.CheckConstraint("ck_meet_participation_duration", "duration_seconds BETWEEN 0 AND 86400");
                    table.CheckConstraint("ck_meet_participation_values", "char_length(endpoint_id) >= 1 AND (email IS NULL OR char_length(email) >= 3)");
                    table.ForeignKey(
                        name: "fk_meet_participation_meet_session_id",
                        column: x => x.meet_session_id,
                        principalTable: "meet_session",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_sync_state_failed_step",
                table: "sync_state",
                sql: "failed_step IS NULL OR (status = 'failed' AND failed_step IN ('classroom', 'meet'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sync_state_meet_loaded_up_to",
                table: "sync_state",
                sql: "meet_loaded_up_to IS NULL OR last_successful_run_at IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_purge_meet_counts",
                table: "audit_event",
                sql: "(purged_meet_sessions IS NULL) = (purged_meet_participations IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_purge_meet_counts_absent",
                table: "audit_event",
                sql: "action = 'retention_purge_run' OR (purged_meet_sessions IS NULL AND purged_meet_participations IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_purge_meet_counts_non_negative",
                table: "audit_event",
                sql: "(purged_meet_sessions IS NULL OR purged_meet_sessions >= 0) AND (purged_meet_participations IS NULL OR purged_meet_participations >= 0)");

            migrationBuilder.CreateIndex(
                name: "ix_meet_participation_email_joined_at",
                table: "meet_participation",
                columns: new[] { "email", "joined_at" });

            migrationBuilder.CreateIndex(
                name: "uq_meet_participation_meet_session_id_endpoint_id",
                table: "meet_participation",
                columns: new[] { "meet_session_id", "endpoint_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meet_session_meeting_code_started_at",
                table: "meet_session",
                columns: new[] { "meeting_code", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_meet_session_started_at",
                table: "meet_session",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "uq_meet_session_conference_id",
                table: "meet_session",
                column: "conference_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meet_participation");

            migrationBuilder.DropTable(
                name: "meet_session");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sync_state_failed_step",
                table: "sync_state");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sync_state_meet_loaded_up_to",
                table: "sync_state");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_purge_meet_counts",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_purge_meet_counts_absent",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_purge_meet_counts_non_negative",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "failed_step",
                table: "sync_state");

            migrationBuilder.DropColumn(
                name: "meet_loaded_up_to",
                table: "sync_state");

            migrationBuilder.DropColumn(
                name: "purged_meet_participations",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "purged_meet_sessions",
                table: "audit_event");
        }
    }
}
