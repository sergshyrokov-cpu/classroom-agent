using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassroomAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseListSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:CollationDefinition:course_name_uk", "uk-UA,uk-UA,icu,True");

            migrationBuilder.CreateIndex(
                name: "ix_course_membership_current_teacher",
                table: "course_membership",
                columns: new[] { "participant_id", "course_id" },
                filter: "role = 'teacher' AND on_roster = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_course_membership_current_teacher",
                table: "course_membership");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:CollationDefinition:course_name_uk", "uk-UA,uk-UA,icu,True");
        }
    }
}
