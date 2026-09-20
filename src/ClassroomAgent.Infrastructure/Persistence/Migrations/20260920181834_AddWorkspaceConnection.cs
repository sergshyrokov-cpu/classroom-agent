using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ClassroomAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceConnection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_refusal_category_value",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_target",
                table: "audit_event");

            migrationBuilder.CreateTable(
                name: "workspace_connection",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    domain = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                    impersonation_user_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    singleton = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspace_connection", x => x.id);
                    table.CheckConstraint("ck_workspace_connection_domain_format", "domain ~ '^[a-z0-9]([a-z0-9-]*[a-z0-9])?(\\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)+$'");
                    table.CheckConstraint("ck_workspace_connection_domain_length", "char_length(domain) BETWEEN 3 AND 253");
                    table.CheckConstraint("ck_workspace_connection_domain_lowercase", "domain = lower(domain)");
                    table.CheckConstraint("ck_workspace_connection_email_domain", "split_part(impersonation_user_email, '@', 2) = domain");
                    table.CheckConstraint("ck_workspace_connection_email_format", "impersonation_user_email ~ '^[^@[:space:]]+@[^@[:space:]]+$'");
                    table.CheckConstraint("ck_workspace_connection_email_length", "char_length(impersonation_user_email) BETWEEN 3 AND 254");
                    table.CheckConstraint("ck_workspace_connection_email_lowercase", "impersonation_user_email = lower(impersonation_user_email)");
                    table.CheckConstraint("ck_workspace_connection_singleton", "singleton");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('admin_sign_in', 'workspace_connection_saved')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_refusal_category_value",
                table: "audit_event",
                sql: "refusal_category IS NULL OR refusal_category IN ('not_in_allowed_admin', 'control_plane_unavailable', 'unknown_installation', 'callback_failed', 'account_disabled', 'domain_mismatch', 'impersonation_domain_mismatch', 'domain_not_confirmed', 'read_only_mode')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_target",
                table: "audit_event",
                sql: "target_id IS NULL OR target_type IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_target_type_value",
                table: "audit_event",
                sql: "target_type IS NULL OR target_type IN ('workspace_connection')");

            migrationBuilder.CreateIndex(
                name: "uq_workspace_connection_singleton",
                table: "workspace_connection",
                column: "singleton",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workspace_connection");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_refusal_category_value",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_target",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_target_type_value",
                table: "audit_event");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('admin_sign_in')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_refusal_category_value",
                table: "audit_event",
                sql: "refusal_category IS NULL OR refusal_category IN ('not_in_allowed_admin', 'control_plane_unavailable', 'unknown_installation', 'callback_failed', 'account_disabled')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_target",
                table: "audit_event",
                sql: "(target_type IS NULL) = (target_id IS NULL)");
        }
    }
}
