using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ClassroomAgent.ControlPlane.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstallation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "installation",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    identifier = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    domain = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                    client_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_installation", x => x.id);
                    table.CheckConstraint("ck_installation_client_id_format", "client_id ~ '^[0-9]{10,32}$'");
                    table.CheckConstraint("ck_installation_domain_format", "domain ~ '^[a-z0-9.-]{3,253}$' AND position('.' in domain) > 0");
                    table.CheckConstraint("ck_installation_domain_lower", "domain = lower(domain)");
                    table.CheckConstraint("ck_installation_name_length", "char_length(name) BETWEEN 1 AND 200");
                    table.CheckConstraint("ck_installation_status", "status IN ('active', 'suspended')");
                });

            migrationBuilder.CreateIndex(
                name: "uq_installation_client_id",
                table: "installation",
                column: "client_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_installation_domain",
                table: "installation",
                column: "domain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_installation_identifier",
                table: "installation",
                column: "identifier",
                unique: true);

            // The identifier and the domain never change, whatever issues the statement (db-design §3.3).
            migrationBuilder.Sql(
                """
                CREATE FUNCTION fn_installation_immutable_columns() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW.identifier IS DISTINCT FROM OLD.identifier OR NEW.domain IS DISTINCT FROM OLD.domain THEN
                        RAISE EXCEPTION 'installation identifier and domain are immutable';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                """);
            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_installation_immutable_columns
                BEFORE UPDATE ON installation
                FOR EACH ROW EXECUTE FUNCTION fn_installation_immutable_columns();
                """);

            // No installation is ever deleted (db-design §3.3).
            migrationBuilder.Sql(
                """
                CREATE FUNCTION fn_installation_no_delete() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'installation rows are never deleted';
                END;
                $$;
                """);
            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_installation_no_delete
                BEFORE DELETE ON installation
                FOR EACH ROW EXECUTE FUNCTION fn_installation_no_delete();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER trg_installation_no_delete ON installation;");
            migrationBuilder.Sql("DROP FUNCTION fn_installation_no_delete();");
            migrationBuilder.Sql("DROP TRIGGER trg_installation_immutable_columns ON installation;");
            migrationBuilder.Sql("DROP FUNCTION fn_installation_immutable_columns();");

            migrationBuilder.DropTable(
                name: "installation");
        }
    }
}
