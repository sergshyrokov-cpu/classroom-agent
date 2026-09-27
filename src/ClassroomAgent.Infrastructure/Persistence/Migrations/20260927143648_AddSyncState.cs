using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ClassroomAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sync_state",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    processed_count = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    last_successful_run_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    singleton = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sync_state", x => x.id);
                    table.CheckConstraint("ck_sync_state_counter", "processed_count >= 0");
                    table.CheckConstraint("ck_sync_state_error_length", "last_error IS NULL OR char_length(last_error) BETWEEN 1 AND 512");
                    table.CheckConstraint("ck_sync_state_finished_after_started", "finished_at IS NULL OR finished_at >= started_at");
                    table.CheckConstraint("ck_sync_state_singleton", "singleton");
                    table.CheckConstraint("ck_sync_state_status", "status IN ('running', 'completed', 'failed')");
                    table.CheckConstraint("ck_sync_state_terminal_fields", "(status = 'running' AND finished_at IS NULL AND last_error IS NULL)\nOR (status = 'completed' AND finished_at IS NOT NULL AND last_error IS NULL)\nOR (status = 'failed' AND finished_at IS NOT NULL AND last_error IS NOT NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "uq_sync_state_singleton",
                table: "sync_state",
                column: "singleton",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sync_state");
        }
    }
}
