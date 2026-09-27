using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassroomAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table <c>sync_state</c> exactly as US-013 db-design §3 defines it. The shadow <c>singleton</c> column, unique
/// and always true, keeps the table at zero or one row; <c>ck_sync_state_terminal_fields</c> keeps the status and
/// the three nullable columns from ever disagreeing.
/// </summary>
public sealed class SyncStateConfiguration : IEntityTypeConfiguration<SyncState>
{
    /// <summary>The shadow property of the <c>singleton</c> column; every row sets it to true.</summary>
    public const string Singleton = "Singleton";

    public void Configure(EntityTypeBuilder<SyncState> builder)
    {
        builder.ToTable("sync_state", table =>
        {
            table.HasCheckConstraint("ck_sync_state_singleton", "singleton");
            table.HasCheckConstraint("ck_sync_state_status", "status IN ('running', 'completed', 'failed')");
            table.HasCheckConstraint("ck_sync_state_counter", "processed_count >= 0");
            table.HasCheckConstraint(
                "ck_sync_state_finished_after_started",
                "finished_at IS NULL OR finished_at >= started_at");

            // The status and the three nullable columns can never disagree (db-design §3.2). last_successful_run_at
            // is deliberately outside it: a failed row with a non-null last success is the normal case.
            table.HasCheckConstraint(
                "ck_sync_state_terminal_fields",
                """
                (status = 'running' AND finished_at IS NULL AND last_error IS NULL)
                OR (status = 'completed' AND finished_at IS NOT NULL AND last_error IS NULL)
                OR (status = 'failed' AND finished_at IS NOT NULL AND last_error IS NOT NULL)
                """);
            table.HasCheckConstraint(
                "ck_sync_state_error_length",
                "last_error IS NULL OR char_length(last_error) BETWEEN 1 AND 512");
        });

        builder.HasKey(s => s.Id).HasName("pk_sync_state");
        builder.Property(s => s.Id).UseIdentityByDefaultColumn();
        builder.Property<bool>(Singleton).IsRequired().HasDefaultValue(true);
        builder.Property(s => s.Status)
            .HasMaxLength(16)
            .IsRequired()
            .HasConversion(v => StatusCode(v), code => StatusFromCode(code));
        builder.Property(s => s.RunId).IsRequired();
        builder.Property(s => s.StartedAt).IsRequired();
        builder.Property(s => s.FinishedAt);
        builder.Property(s => s.ProcessedCount).IsRequired();
        builder.Property(s => s.LastError).HasMaxLength(SyncState.MaxErrorLength);
        builder.Property(s => s.LastSuccessfulRunAt);
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();

        builder.HasIndex(Singleton).IsUnique().HasDatabaseName("uq_sync_state_singleton");
    }

    private static string StatusCode(SyncRunStatus value) => value switch
    {
        SyncRunStatus.Running => "running",
        SyncRunStatus.Completed => "completed",
        SyncRunStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static SyncRunStatus StatusFromCode(string code) => code switch
    {
        "running" => SyncRunStatus.Running,
        "completed" => SyncRunStatus.Completed,
        "failed" => SyncRunStatus.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };
}
