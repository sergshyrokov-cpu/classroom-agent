using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ClassroomAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAppUserAndAuditEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_user",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    sign_in_method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    security_stamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    concurrency_stamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ui_language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    is_disabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    last_successful_sign_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_user", x => x.id);
                    table.CheckConstraint("ck_app_user_access_failed_count", "access_failed_count >= 0");
                    table.CheckConstraint("ck_app_user_email_lowercase", "email = lower(email) AND normalized_email = lower(normalized_email)");
                    table.CheckConstraint("ck_app_user_password_hash", "(sign_in_method = 'password') = (password_hash IS NOT NULL)");
                    table.CheckConstraint("ck_app_user_role", "role IN ('admin', 'dean')");
                    table.CheckConstraint("ck_app_user_role_sign_in_method", "(role = 'admin') = (sign_in_method = 'google')");
                    table.CheckConstraint("ck_app_user_sign_in_method", "sign_in_method IN ('google', 'password')");
                    table.CheckConstraint("ck_app_user_ui_language", "ui_language IN ('uk', 'en')");
                });

            migrationBuilder.CreateTable(
                name: "audit_event",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    actor_id = table.Column<long>(type: "bigint", nullable: true),
                    actor_role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
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
                    table.CheckConstraint("ck_audit_event_action", "action IN ('admin_sign_in')");
                    table.CheckConstraint("ck_audit_event_actor_id", "(actor_type = 'app_user') = (actor_id IS NOT NULL)");
                    table.CheckConstraint("ck_audit_event_actor_role", "(actor_type = 'app_user') = (actor_role IS NOT NULL)");
                    table.CheckConstraint("ck_audit_event_actor_role_value", "actor_role IS NULL OR actor_role IN ('admin', 'dean')");
                    table.CheckConstraint("ck_audit_event_actor_type", "actor_type IN ('app_user', 'anonymous', 'system')");
                    table.CheckConstraint("ck_audit_event_immutable", "updated_at = created_at");
                    table.CheckConstraint("ck_audit_event_outcome", "outcome IN ('succeeded', 'refused')");
                    table.CheckConstraint("ck_audit_event_refusal_category", "(outcome = 'refused') = (refusal_category IS NOT NULL)");
                    table.CheckConstraint("ck_audit_event_refusal_category_value", "refusal_category IS NULL OR refusal_category IN ('not_in_allowed_admin', 'control_plane_unavailable', 'unknown_installation', 'callback_failed', 'account_disabled')");
                    table.CheckConstraint("ck_audit_event_target", "(target_type IS NULL) = (target_id IS NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "uq_app_user_normalized_email",
                table: "app_user",
                column: "normalized_email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_user");

            migrationBuilder.DropTable(
                name: "audit_event");
        }
    }
}
