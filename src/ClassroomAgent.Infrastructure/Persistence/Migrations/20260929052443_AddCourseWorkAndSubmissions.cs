using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ClassroomAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseWorkAndSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "course_work",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    course_id = table.Column<long>(type: "bigint", nullable: false),
                    google_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    resource = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    title = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false),
                    item_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    max_points = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    creation_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    update_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_work", x => x.id);
                    table.CheckConstraint("ck_course_work_material_has_no_grading", "resource <> 'course_work_material' OR (max_points IS NULL AND due_at IS NULL)");
                    table.CheckConstraint("ck_course_work_max_points_non_negative", "max_points IS NULL OR max_points >= 0");
                    table.CheckConstraint("ck_course_work_resource", "resource IN ('course_work', 'course_work_material')");
                    table.ForeignKey(
                        name: "fk_course_work_course",
                        column: x => x.course_id,
                        principalTable: "course",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "submission",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    course_work_id = table.Column<long>(type: "bigint", nullable: false),
                    participant_id = table.Column<long>(type: "bigint", nullable: false),
                    google_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    raw_state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    assigned_grade = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    draft_grade = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    turned_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    late = table.Column<bool>(type: "boolean", nullable: false),
                    update_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submission", x => x.id);
                    table.CheckConstraint("ck_submission_grades_non_negative", "(assigned_grade IS NULL OR assigned_grade >= 0) AND (draft_grade IS NULL OR draft_grade >= 0)");
                    table.CheckConstraint("ck_submission_raw_state", "(state = 'unrecognised') = (raw_state IS NOT NULL)");
                    table.CheckConstraint("ck_submission_state", "state IN ('new', 'created', 'turned_in', 'returned', 'reclaimed_by_student', 'student_edited_after_turn_in', 'unrecognised')");
                    table.ForeignKey(
                        name: "fk_submission_classroom_participant",
                        column: x => x.participant_id,
                        principalTable: "classroom_participant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_submission_course_work",
                        column: x => x.course_work_id,
                        principalTable: "course_work",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_course_work_course_item_date",
                table: "course_work",
                columns: new[] { "course_id", "item_date" });

            migrationBuilder.CreateIndex(
                name: "uq_course_work_course_resource_google_id",
                table: "course_work",
                columns: new[] { "course_id", "resource", "google_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_submission_course_work_participant",
                table: "submission",
                columns: new[] { "course_work_id", "participant_id" });

            migrationBuilder.CreateIndex(
                name: "ix_submission_participant_id",
                table: "submission",
                column: "participant_id");

            migrationBuilder.CreateIndex(
                name: "uq_submission_course_work_google_id",
                table: "submission",
                columns: new[] { "course_work_id", "google_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "submission");

            migrationBuilder.DropTable(
                name: "course_work");
        }
    }
}
