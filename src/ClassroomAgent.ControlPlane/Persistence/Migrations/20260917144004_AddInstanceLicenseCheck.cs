using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ClassroomAgent.ControlPlane.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstanceLicenseCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "instance_license_check",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    installation_id = table.Column<long>(type: "bigint", nullable: false),
                    answered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    application_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    contract_version = table.Column<int>(type: "integer", nullable: false),
                    answered_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    answered_compatibility = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instance_license_check", x => x.id);
                    table.CheckConstraint("ck_instance_license_check_answered_compatibility", "answered_compatibility IN ('supported', 'upgrade_recommended', 'upgrade_required')");
                    table.CheckConstraint("ck_instance_license_check_answered_status", "answered_status IN ('active', 'suspended')");
                    table.CheckConstraint("ck_instance_license_check_application_version", "application_version ~ '^(0|[1-9][0-9]{0,5})\\.(0|[1-9][0-9]{0,5})\\.(0|[1-9][0-9]{0,5})$'");
                    table.CheckConstraint("ck_instance_license_check_contract_version", "contract_version BETWEEN 1 AND 999999");
                    table.ForeignKey(
                        name: "fk_instance_license_check_installation",
                        column: x => x.installation_id,
                        principalTable: "installation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_instance_license_check_installation_id",
                table: "instance_license_check",
                column: "installation_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "instance_license_check");
        }
    }
}
