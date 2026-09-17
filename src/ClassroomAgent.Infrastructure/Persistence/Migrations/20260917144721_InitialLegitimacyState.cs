using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ClassroomAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialLegitimacyState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "legitimacy_state",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    last_successful_check_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    compatibility = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    domain = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                    client_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    singleton = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legitimacy_state", x => x.id);
                    table.CheckConstraint("ck_legitimacy_state_client_id_format", "client_id ~ '^[0-9]{10,32}$'");
                    table.CheckConstraint("ck_legitimacy_state_compatibility", "compatibility IN ('supported', 'upgrade_recommended', 'upgrade_required')");
                    table.CheckConstraint("ck_legitimacy_state_domain_length", "char_length(domain) BETWEEN 3 AND 253");
                    table.CheckConstraint("ck_legitimacy_state_singleton", "singleton");
                    table.CheckConstraint("ck_legitimacy_state_status", "status IN ('active', 'suspended')");
                });

            migrationBuilder.CreateIndex(
                name: "uq_legitimacy_state_singleton",
                table: "legitimacy_state",
                column: "singleton",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "legitimacy_state");
        }
    }
}
