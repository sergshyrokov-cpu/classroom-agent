using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ClassroomAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCoursesAndRosters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "classroom_participant",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    google_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    full_name = table.Column<string>(type: "character varying(750)", maxLength: 750, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_classroom_participant", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "course",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    google_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(750)", maxLength: 750, nullable: false),
                    section = table.Column<string>(type: "character varying(2800)", maxLength: 2800, nullable: true),
                    description_heading = table.Column<string>(type: "character varying(3600)", maxLength: 3600, nullable: true),
                    description = table.Column<string>(type: "character varying(30000)", maxLength: 30000, nullable: true),
                    room = table.Column<string>(type: "character varying(650)", maxLength: 650, nullable: true),
                    owner_google_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    creation_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    update_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    course_state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    alternate_link = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    teacher_folder_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    teacher_folder_title = table.Column<string>(type: "character varying(750)", maxLength: 750, nullable: true),
                    calendar_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course", x => x.id);
                    table.CheckConstraint("ck_course_course_state", "course_state IN ('active', 'archived', 'provisioned', 'declined', 'suspended')");
                });

            migrationBuilder.CreateTable(
                name: "course_membership",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    course_id = table.Column<long>(type: "bigint", nullable: false),
                    participant_id = table.Column<long>(type: "bigint", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    on_roster = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_membership", x => x.id);
                    table.CheckConstraint("ck_course_membership_role", "role IN ('teacher', 'student')");
                    table.CheckConstraint("ck_course_membership_seen_order", "last_seen_at >= first_seen_at");
                    table.ForeignKey(
                        name: "fk_course_membership_classroom_participant",
                        column: x => x.participant_id,
                        principalTable: "classroom_participant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_membership_course",
                        column: x => x.course_id,
                        principalTable: "course",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_classroom_participant_email",
                table: "classroom_participant",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "uq_classroom_participant_google_user_id",
                table: "classroom_participant",
                column: "google_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_course_google_id",
                table: "course",
                column: "google_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_course_membership_course_seen",
                table: "course_membership",
                columns: new[] { "course_id", "first_seen_at", "last_seen_at" });

            migrationBuilder.CreateIndex(
                name: "ix_course_membership_participant_id",
                table: "course_membership",
                column: "participant_id");

            migrationBuilder.CreateIndex(
                name: "uq_course_membership_course_participant",
                table: "course_membership",
                columns: new[] { "course_id", "participant_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "course_membership");

            migrationBuilder.DropTable(
                name: "classroom_participant");

            migrationBuilder.DropTable(
                name: "course");
        }
    }
}
