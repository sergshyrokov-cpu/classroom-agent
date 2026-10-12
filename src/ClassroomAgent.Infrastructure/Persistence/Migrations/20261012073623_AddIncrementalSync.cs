using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassroomAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIncrementalSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_full_run_at",
                table: "sync_state",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "complete_read_update_time",
                table: "course",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_sync_state_last_full_run_at",
                table: "sync_state",
                sql: "last_full_run_at IS NULL OR (last_successful_run_at IS NOT NULL AND last_full_run_at <= last_successful_run_at)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_sync_state_last_full_run_at",
                table: "sync_state");

            migrationBuilder.DropColumn(
                name: "last_full_run_at",
                table: "sync_state");

            migrationBuilder.DropColumn(
                name: "complete_read_update_time",
                table: "course");
        }
    }
}
