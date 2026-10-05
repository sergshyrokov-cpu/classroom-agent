using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassroomAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ParticipantNamePartsAndTemplateNameSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "name_source",
                table: "report_template",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "profile");

            // US-042 db-design §4, D-5: every existing template gets "profile", then the default is dropped so the
            // schema matches the model — the domain always sets the value.
            migrationBuilder.AlterColumn<string>(
                name: "name_source",
                table: "report_template",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldMaxLength: 8,
                oldDefaultValue: "profile");

            migrationBuilder.AddColumn<string>(
                name: "given_name",
                table: "classroom_participant",
                type: "character varying(750)",
                maxLength: 750,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "surname",
                table: "classroom_participant",
                type: "character varying(750)",
                maxLength: 750,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_report_template_name_source",
                table: "report_template",
                sql: "name_source IN ('profile', 'email')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_report_template_name_source",
                table: "report_template");

            migrationBuilder.DropColumn(
                name: "name_source",
                table: "report_template");

            migrationBuilder.DropColumn(
                name: "given_name",
                table: "classroom_participant");

            migrationBuilder.DropColumn(
                name: "surname",
                table: "classroom_participant");
        }
    }
}
