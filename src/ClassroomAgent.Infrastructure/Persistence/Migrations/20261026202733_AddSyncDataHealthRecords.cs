using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ClassroomAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncDataHealthRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "not_in_classroom_since",
                table: "course_work",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_complete_read_at",
                table: "course",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sync_skipped_course",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    course_google_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    course_name = table.Column<string>(type: "character varying(750)", maxLength: 750, nullable: true),
                    reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    skipped_since = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sync_skipped_course", x => x.id);
                    table.CheckConstraint("ck_sync_skipped_course_google_id", "char_length(course_google_id) >= 1");
                    table.CheckConstraint("ck_sync_skipped_course_name", "course_name IS NULL OR char_length(course_name) >= 1");
                    table.CheckConstraint("ck_sync_skipped_course_reason", "reason IN ('value_out_of_range', 'unknown_course_state')");
                });

            migrationBuilder.CreateIndex(
                name: "uq_sync_skipped_course_course_google_id",
                table: "sync_skipped_course",
                column: "course_google_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sync_skipped_course");

            migrationBuilder.DropColumn(
                name: "not_in_classroom_since",
                table: "course_work");

            migrationBuilder.DropColumn(
                name: "last_complete_read_at",
                table: "course");
        }
    }
}
