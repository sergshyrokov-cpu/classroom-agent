using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Drives US-013 in a real host: an installation seeded into a legitimacy state and a connection state, started
/// with the synchronization background service the host itself registers (spec FR-018). Nothing reaches Google
/// (TC-4) — the Story's pipeline is empty (OD-001), and the Control Plane is answered by the fake.
/// </summary>
public static class SyncHostExtensions
{
    /// <summary>
    /// A host created, seeded and only then started — the background service reads what is in the database at
    /// start (spec FR-011). No one signs in; this Story has no endpoint.
    /// </summary>
    public static async Task<InstallationTestHost> StartAsync(
        PostgreSqlFixture database,
        CancellationToken cancellationToken,
        ReadOnlyModeHost.Cause cause = ReadOnlyModeHost.Cause.NotReadOnly,
        SeededConnection connection = SeededConnection.Usable,
        int? intervalMinutes = null,
        bool? replySuccess = null,
        Func<InstallationTestHost, CancellationToken, Task>? seed = null,
        Action<FakeClassroomReader>? classroom = null)
    {
        var host = await InstallationTestHost.CreateAsync(database, cancellationToken);
        await ReadOnlyModeHost.SeedAsync(host, cause, cancellationToken);
        await AccessCheckHostExtensions.SeedConnectionAsync(host, connection, cancellationToken);
        if (intervalMinutes is { } minutes)
        {
            host.Settings[SyncTestData.IntervalSetting] = minutes.ToString(CultureInfo.InvariantCulture);
        }

        // A read-only cause is never answered: the seeded legitimacy_state row is what the guard must read, and a
        // successful check would clear the very mode the test is about (the US-011 ReadOnlyModeHost rule). Either
        // way the check COMPLETES, which is what the first run waits for (spec FR-011, OD-006).
        if (replySuccess ?? cause == ReadOnlyModeHost.Cause.NotReadOnly)
        {
            host.ControlPlane.ReplySuccess().ReplySuccess().ReplySuccess();
        }
        else
        {
            // Answered with a failure, not left unanswered: an unsuccessful check leaves the seeded row and its
            // last successful check exactly as they are (US-005), so the mode survives, while the check still
            // COMPLETES — which is what the first run waits for (spec FR-011).
            host.ControlPlane
                .ReplyFailure(CheckFailureCategory.Unreachable)
                .ReplyFailure(CheckFailureCategory.Unreachable)
                .ReplyFailure(CheckFailureCategory.Unreachable);
        }

        // US-014: the Classroom port and the database are seeded BEFORE the host starts, because the first run
        // may begin as soon as it has (spec FR-011). Nothing reaches Google either way (TC-4).
        classroom?.Invoke(host.Classroom);
        if (seed is not null)
        {
            await seed(host, cancellationToken);
        }

        host.Start();
        return host;
    }

    /// <summary>The single <c>sync_state</c> row, or none when the installation has never synchronized (spec I-2).</summary>
    public static Task<IReadOnlyList<SyncStateRow>> SyncStatesAsync(
        this InstallationTestHost host,
        CancellationToken cancellationToken) =>
        host.QueryAsync(
            """
            SELECT status, run_id, started_at, finished_at, processed_count, last_error, last_successful_run_at
            FROM sync_state
            """,
            r => new SyncStateRow(
                r.GetString(0),
                r.GetGuid(1),
                r.GetFieldValue<DateTimeOffset>(2),
                r.IsDBNull(3) ? null : r.GetFieldValue<DateTimeOffset>(3),
                r.GetInt32(4),
                r.IsDBNull(5) ? null : r.GetString(5),
                r.IsDBNull(6) ? null : r.GetFieldValue<DateTimeOffset>(6)),
            cancellationToken);

    /// <summary>Waits until the row reaches a terminal status, so an assertion never races the run.</summary>
    public static async Task<SyncStateRow> WaitForFinishedRunAsync(
        this InstallationTestHost host,
        CancellationToken cancellationToken,
        int runOrdinal = 1)
    {
        var deadline = DateTime.UtcNow + ManualTimeProvider.RealTimeLimit;
        while (DateTime.UtcNow < deadline)
        {
            var rows = await host.SyncStatesAsync(cancellationToken);
            if (rows.Count == 1 && rows[0].Status != SyncTestData.Status.Running)
            {
                return rows[0];
            }

            await Task.Delay(25, cancellationToken);
        }

        Assert.Fail($"No finished synchronization run appeared within {ManualTimeProvider.RealTimeLimit} (run {runOrdinal}).");
        throw new InvalidOperationException("unreachable");
    }

    /// <summary>The readiness state the private port reports (DC-11, spec FR-014).</summary>
    public static async Task<string> ReadinessAsync(this InstallationTestHost host, CancellationToken cancellationToken) =>
        (await host.SendPrivateAsync("GET", "/health/ready", cancellationToken)).Body;

    /// <summary>The coordinator of the running host — the seam US-019 will call (spec FR-004, OD-007).</summary>
    public static ClassroomAgent.Web.BackgroundServices.SyncRunCoordinator CoordinatorOf(InstallationTestHost host) =>
        host.Services.GetRequiredService<ClassroomAgent.Web.BackgroundServices.SyncRunCoordinator>();

    /// <summary>The in-process marker readiness reads (spec FR-014).</summary>
    public static ClassroomAgent.Application.UseCases.SynchronizationServiceMemory MemoryOf(InstallationTestHost host) =>
        host.Services.GetRequiredService<ClassroomAgent.Application.UseCases.SynchronizationServiceMemory>();

    /// <summary>A row of <c>sync_state</c> as the database holds it.</summary>
    public sealed record SyncStateRow(
        string Status,
        Guid RunId,
        DateTimeOffset StartedAt,
        DateTimeOffset? FinishedAt,
        int ProcessedCount,
        string? LastError,
        DateTimeOffset? LastSuccessfulRunAt)
    {
        public SyncRunStatus Parsed => Status switch
        {
            SyncTestData.Status.Running => SyncRunStatus.Running,
            SyncTestData.Status.Completed => SyncRunStatus.Completed,
            SyncTestData.Status.Failed => SyncRunStatus.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(Status), Status, null),
        };
    }
}
