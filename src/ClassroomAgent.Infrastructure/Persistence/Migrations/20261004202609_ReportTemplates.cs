using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ClassroomAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReportTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_target_type_value",
                table: "audit_event");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "scheduled_time",
                table: "course_work",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "report_template",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    author_id = table.Column<long>(type: "bigint", nullable: false),
                    view = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    hide_materials = table.Column<bool>(type: "boolean", nullable: false),
                    hours_per_lesson = table.Column<int>(type: "integer", nullable: false),
                    scale_mode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    late_mark_kind = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    late_mark_text = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_template", x => x.id);
                    table.CheckConstraint("ck_report_template_hours_per_lesson", "hours_per_lesson BETWEEN 1 AND 10");
                    table.CheckConstraint("ck_report_template_late_mark_kind", "late_mark_kind IN ('program', 'own', 'hidden')");
                    table.CheckConstraint("ck_report_template_late_mark_text", "(late_mark_kind = 'own') = (late_mark_text IS NOT NULL)");
                    table.CheckConstraint("ck_report_template_scale_mode", "scale_mode IN ('none', 'ranges')");
                    table.CheckConstraint("ck_report_template_view", "view IN ('full', 'short')");
                });

            migrationBuilder.CreateTable(
                name: "report_template_mark",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    report_template_id = table.Column<long>(type: "bigint", nullable: false),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    kind = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    text = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_template_mark", x => x.id);
                    table.CheckConstraint("ck_report_template_mark_kind", "kind IN ('program', 'own', 'empty')");
                    table.CheckConstraint("ck_report_template_mark_state", "state IN ('turned_in_not_graded', 'returned_without_grade', 'turned_in', 'returned', 'not_turned_in', 'not_due_yet', 'not_turned_in_no_due_date', 'not_assigned', 'unrecognised')");
                    table.CheckConstraint("ck_report_template_mark_text", "(kind = 'own') = (text IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_report_template_mark_report_template",
                        column: x => x.report_template_id,
                        principalTable: "report_template",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "report_template_scale_row",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    report_template_id = table.Column<long>(type: "bigint", nullable: false),
                    from_percent = table.Column<short>(type: "smallint", nullable: false),
                    to_percent = table.Column<short>(type: "smallint", nullable: false),
                    label = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_template_scale_row", x => x.id);
                    table.CheckConstraint("ck_report_template_scale_row_bounds", "from_percent BETWEEN 0 AND 100 AND to_percent BETWEEN 0 AND 100 AND from_percent <= to_percent");
                    table.ForeignKey(
                        name: "fk_report_template_scale_row_report_template",
                        column: x => x.report_template_id,
                        principalTable: "report_template",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('admin_sign_in', 'workspace_connection_saved', 'access_check_run', 'dean_account_created', 'dean_account_disabled', 'dean_account_reenabled', 'dean_account_password_reset', 'dean_password_changed', 'dean_sign_in', 'retention_purge_run', 'synchronization_requested', 'report_template_created', 'report_template_changed', 'report_template_deleted')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_report_template_shape",
                table: "audit_event",
                sql: "action NOT IN ('report_template_created', 'report_template_changed', 'report_template_deleted') OR (actor_type = 'app_user' AND target_type = 'report_template' AND (refusal_category IS NULL OR refusal_category = 'read_only_mode') AND (outcome <> 'succeeded' OR target_id IS NOT NULL) AND (action <> 'report_template_created' OR outcome = 'succeeded' OR target_id IS NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_target_type_value",
                table: "audit_event",
                sql: "target_type IS NULL OR target_type IN ('workspace_connection', 'app_user', 'report_template')");

            migrationBuilder.CreateIndex(
                name: "uq_report_template_normalized_name",
                table: "report_template",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_report_template_mark_template_state",
                table: "report_template_mark",
                columns: new[] { "report_template_id", "state" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_report_template_scale_row_template_from",
                table: "report_template_scale_row",
                columns: new[] { "report_template_id", "from_percent" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "report_template_mark");

            migrationBuilder.DropTable(
                name: "report_template_scale_row");

            migrationBuilder.DropTable(
                name: "report_template");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_report_template_shape",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_target_type_value",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "scheduled_time",
                table: "course_work");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('admin_sign_in', 'workspace_connection_saved', 'access_check_run', 'dean_account_created', 'dean_account_disabled', 'dean_account_reenabled', 'dean_account_password_reset', 'dean_password_changed', 'dean_sign_in', 'retention_purge_run', 'synchronization_requested')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_target_type_value",
                table: "audit_event",
                sql: "target_type IS NULL OR target_type IN ('workspace_connection', 'app_user')");
        }
    }
}
