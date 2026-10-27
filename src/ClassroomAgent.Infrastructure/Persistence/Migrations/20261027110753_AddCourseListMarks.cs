using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassroomAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseListMarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "technical_account_changed_at",
                table: "workspace_connection",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "last_course_list_empty",
                table: "sync_state",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "absence_mark",
                table: "course",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "absent_since",
                table: "course",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_listed_at",
                table: "course",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_course_absence_mark",
                table: "course",
                sql: "(absence_mark IS NULL AND absent_since IS NULL) OR (absence_mark IS NOT NULL AND absence_mark IN ('not_in_classroom', 'not_visible_to_technical_account') AND absent_since IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_course_absence_mark",
                table: "course");

            migrationBuilder.DropColumn(
                name: "technical_account_changed_at",
                table: "workspace_connection");

            migrationBuilder.DropColumn(
                name: "last_course_list_empty",
                table: "sync_state");

            migrationBuilder.DropColumn(
                name: "absence_mark",
                table: "course");

            migrationBuilder.DropColumn(
                name: "absent_since",
                table: "course");

            migrationBuilder.DropColumn(
                name: "last_listed_at",
                table: "course");
        }
    }
}
