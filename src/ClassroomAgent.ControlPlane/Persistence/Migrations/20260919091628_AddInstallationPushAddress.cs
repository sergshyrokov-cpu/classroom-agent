using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassroomAgent.ControlPlane.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstallationPushAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "push_address",
                table: "installation",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_installation_push_address_format",
                table: "installation",
                sql: "push_address IS NULL OR push_address ~ '^http://(\\[[0-9a-f:.]+\\]|[a-z0-9.-]{1,253}):[1-9][0-9]{0,4}$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_installation_push_address_format",
                table: "installation");

            migrationBuilder.DropColumn(
                name: "push_address",
                table: "installation");
        }
    }
}
