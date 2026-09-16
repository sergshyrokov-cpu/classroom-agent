using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ClassroomAgent.ControlPlane.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialOwnerAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_event",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    actor_id = table.Column<long>(type: "bigint", nullable: true),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    target_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    target_id = table.Column<long>(type: "bigint", nullable: true),
                    outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    refusal_category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    request_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_event", x => x.id);
                    table.CheckConstraint("ck_audit_event_actor", "(actor_type = 'anonymous') = (actor_id IS NULL)");
                    table.CheckConstraint("ck_audit_event_actor_type", "actor_type IN ('owner', 'anonymous')");
                    table.CheckConstraint("ck_audit_event_outcome", "outcome IN ('succeeded', 'refused')");
                    table.CheckConstraint("ck_audit_event_refusal_category", "(outcome = 'refused') = (refusal_category IS NOT NULL)");
                    table.CheckConstraint("ck_audit_event_target", "(target_type IS NULL) = (target_id IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "owner",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    normalized_user_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    security_stamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    concurrency_stamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ui_language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    singleton = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_owner", x => x.id);
                    table.CheckConstraint("ck_owner_access_failed_count", "access_failed_count >= 0");
                    table.CheckConstraint("ck_owner_singleton", "singleton = true");
                    table.CheckConstraint("ck_owner_ui_language", "ui_language IN ('uk', 'en')");
                });

            migrationBuilder.CreateIndex(
                name: "uq_owner_normalized_user_name",
                table: "owner",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_owner_singleton",
                table: "owner",
                column: "singleton",
                unique: true);

            // Audit rows are never updated or deleted, whatever issues the statement (db-design §4.2).
            migrationBuilder.Sql(
                """
                CREATE FUNCTION fn_audit_event_immutable() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'audit_event rows are immutable';
                END;
                $$;
                """);
            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_audit_event_immutable
                BEFORE UPDATE OR DELETE ON audit_event
                FOR EACH ROW EXECUTE FUNCTION fn_audit_event_immutable();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER trg_audit_event_immutable ON audit_event;");
            migrationBuilder.Sql("DROP FUNCTION fn_audit_event_immutable();");

            migrationBuilder.DropTable(
                name: "audit_event");

            migrationBuilder.DropTable(
                name: "owner");
        }
    }
}
