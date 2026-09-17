using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ClassroomAgent.ControlPlane.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAllowedAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "allowed_admin",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    identifier = table.Column<Guid>(type: "uuid", nullable: false),
                    installation_id = table.Column<long>(type: "bigint", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    added_by_owner_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_allowed_admin", x => x.id);
                    table.CheckConstraint("ck_allowed_admin_email_format", "char_length(email) BETWEEN 3 AND 254 AND email ~ '^[a-z0-9._''-]{1,64}@[a-z0-9.-]+$'");
                    table.CheckConstraint("ck_allowed_admin_email_lower", "email = lower(email)");
                    table.ForeignKey(
                        name: "fk_allowed_admin_added_by_owner",
                        column: x => x.added_by_owner_id,
                        principalTable: "owner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_allowed_admin_installation",
                        column: x => x.installation_id,
                        principalTable: "installation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_allowed_admin_added_by_owner_id",
                table: "allowed_admin",
                column: "added_by_owner_id");

            migrationBuilder.CreateIndex(
                name: "uq_allowed_admin_identifier",
                table: "allowed_admin",
                column: "identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_allowed_admin_installation_email",
                table: "allowed_admin",
                columns: new[] { "installation_id", "email" },
                unique: true);

            // An entry's email is always in exactly its installation's domain (db-design §3.2).
            migrationBuilder.Sql(
                """
                CREATE FUNCTION fn_allowed_admin_domain_match() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF split_part(NEW.email, '@', 2) IS DISTINCT FROM
                        (SELECT domain FROM installation WHERE id = NEW.installation_id) THEN
                        RAISE EXCEPTION 'allowed_admin email must be in the installation domain';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                """);
            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_allowed_admin_domain_match
                BEFORE INSERT ON allowed_admin
                FOR EACH ROW EXECUTE FUNCTION fn_allowed_admin_domain_match();
                """);

            // An entry is never edited; revocation deletes it (db-design §3.3).
            migrationBuilder.Sql(
                """
                CREATE FUNCTION fn_allowed_admin_no_update() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'allowed_admin rows are never updated';
                END;
                $$;
                """);
            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_allowed_admin_no_update
                BEFORE UPDATE ON allowed_admin
                FOR EACH ROW EXECUTE FUNCTION fn_allowed_admin_no_update();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER trg_allowed_admin_no_update ON allowed_admin;");
            migrationBuilder.Sql("DROP FUNCTION fn_allowed_admin_no_update();");
            migrationBuilder.Sql("DROP TRIGGER trg_allowed_admin_domain_match ON allowed_admin;");
            migrationBuilder.Sql("DROP FUNCTION fn_allowed_admin_domain_match();");

            migrationBuilder.DropTable(
                name: "allowed_admin");
        }
    }
}
