using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassroomAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalExportAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_target_type_value",
                table: "audit_event");

            migrationBuilder.AddColumn<string>(
                name: "export_format",
                table: "audit_event",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "export_period_from",
                table: "audit_event",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "export_period_to",
                table: "audit_event",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "export_rows",
                table: "audit_event",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "export_template_built_in",
                table: "audit_event",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "export_template_id",
                table: "audit_event",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('admin_sign_in', 'workspace_connection_saved', 'access_check_run', 'dean_account_created', 'dean_account_disabled', 'dean_account_reenabled', 'dean_account_password_reset', 'dean_password_changed', 'dean_sign_in', 'retention_purge_run', 'synchronization_requested', 'report_template_created', 'report_template_changed', 'report_template_deleted', 'journal_exported')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_export_columns",
                table: "audit_event",
                sql: "(action = 'journal_exported') = (export_period_from IS NOT NULL AND export_period_to IS NOT NULL AND export_template_built_in IS NOT NULL AND export_rows IS NOT NULL AND export_format IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_export_columns_absent",
                table: "audit_event",
                sql: "action = 'journal_exported' OR (export_period_from IS NULL AND export_period_to IS NULL AND export_template_id IS NULL AND export_template_built_in IS NULL AND export_rows IS NULL AND export_format IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_export_shape",
                table: "audit_event",
                sql: "action <> 'journal_exported' OR (actor_type = 'app_user' AND target_type = 'course' AND target_id IS NOT NULL AND outcome = 'succeeded' AND export_period_from <= export_period_to AND export_rows >= 0 AND export_format IN ('xlsx') AND export_template_built_in = (export_template_id IS NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_export_template_id",
                table: "audit_event",
                sql: "export_template_id IS NULL OR export_template_id > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_target_type_value",
                table: "audit_event",
                sql: "target_type IS NULL OR target_type IN ('workspace_connection', 'app_user', 'report_template', 'course')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_export_columns",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_export_columns_absent",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_export_shape",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_export_template_id",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_target_type_value",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "export_format",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "export_period_from",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "export_period_to",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "export_rows",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "export_template_built_in",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "export_template_id",
                table: "audit_event");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('admin_sign_in', 'workspace_connection_saved', 'access_check_run', 'dean_account_created', 'dean_account_disabled', 'dean_account_reenabled', 'dean_account_password_reset', 'dean_password_changed', 'dean_sign_in', 'retention_purge_run', 'synchronization_requested', 'report_template_created', 'report_template_changed', 'report_template_deleted')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_target_type_value",
                table: "audit_event",
                sql: "target_type IS NULL OR target_type IN ('workspace_connection', 'app_user', 'report_template')");
        }
    }
}
