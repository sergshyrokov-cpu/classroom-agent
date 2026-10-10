using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.BackgroundServices;

/// <summary>
/// US-031 AC-001, AC-003, AC-006, AC-007, FR-015: the Meet step through the real host, its composition root and
/// PostgreSQL, with the Meet port substituted (TC-4). The fake is seeded before the host starts, because the first run
/// may begin as soon as it has.
/// </summary>
public sealed class MeetPullHostTests(PostgreSqlFixture database)
{
    private static readonly DateTimeOffset LeftAt = InstallationTestHost.DefaultStart - TimeSpan.FromDays(1);

    /// <summary>Starts a usable host whose Meet port answers one page of two domain events of one conference.</summary>
    private Task<InstallationTestHost> StartWithOnePageAsync(CancellationToken ct, int? intervalMinutes = null) =>
        SyncHostExtensions.StartAsync(
            database,
            ct,
            intervalMinutes: intervalMinutes,
            seed: (host, _) =>
            {
                host.Meet.WithPage(
                    MeetTestData.Event(1, 1, LeftAt, 600, MeetTestData.Teacher(1), MeetTestData.Teacher(1)),
                    MeetTestData.Event(1, 2, LeftAt, 300, MeetTestData.Teacher(1), MeetTestData.Student(1)));
                return Task.CompletedTask;
            });

    /// <summary>Npgsql returns a <c>timestamptz</c> scalar as a UTC <see cref="DateTime"/>.</summary>
    private static async Task<DateTimeOffset?> WatermarkAsync(InstallationTestHost host, CancellationToken ct) =>
        await host.ScalarAsync<DateTime?>("SELECT meet_loaded_up_to FROM sync_state", ct) is { } utc
            ? new DateTimeOffset(utc, TimeSpan.Zero)
            : null;

    /// <summary>AC-001, FR-007: one meeting and two connections are stored, and the watermark is the run's instant.</summary>
    [Fact]
    public async Task ARun_StoresTheMeeting_AndMovesTheWatermarkToTheRunsInstant()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWithOnePageAsync(ct);

        var row = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, row.Status);
        Assert.Equal(1L, await host.CountAsync("meet_session", ct));
        Assert.Equal(2L, await host.CountAsync("meet_participation", ct));
        Assert.Equal(host.Time.GetUtcNow(), await WatermarkAsync(host, ct));
    }

    /// <summary>AC-003: a second run that reads the same events duplicates nothing.</summary>
    [Fact]
    public async Task ASecondRunReadingTheSameEvents_LeavesOneSessionAndTwoParticipations()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWithOnePageAsync(ct, intervalMinutes: 1);
        var first = await host.WaitForFinishedRunAsync(ct);

        await host.Time.AdvanceWhenDueAsync(TimeSpan.FromMinutes(1), ct);
        await host.Time.WaitUntilAsync(
            () => host.SyncStatesAsync(ct).GetAwaiter().GetResult() is [{ } row] && row.RunId != first.RunId && row.Status != SyncTestData.Status.Running,
            "a second finished synchronization run",
            ct);

        Assert.Equal(2, host.Meet.Calls.Count);
        Assert.Equal(1L, await host.CountAsync("meet_session", ct));
        Assert.Equal(2L, await host.CountAsync("meet_participation", ct));
    }

    /// <summary>AC-006, FR-002: the port is asked as the technical account, for the first window.</summary>
    [Fact]
    public async Task ThePort_IsCalledAsTheTechnicalAccount_WithTheFirstWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWithOnePageAsync(ct);

        await host.WaitForFinishedRunAsync(ct);

        var call = Assert.Single(host.Meet.Calls);
        Assert.Equal(AccessCheckTestData.TechnicalAccount, call.ImpersonationUser);
        Assert.Equal(host.Time.GetUtcNow() - MeetTestData.Horizon, call.From);
        Assert.Equal(host.Time.GetUtcNow(), call.To);
    }

    /// <summary>AC-007, FR-010: a configuration failure of the Meet step fails the run at that step, with no watermark.</summary>
    [Fact]
    public async Task AConfigurationFailureOfTheMeetStep_FailsTheRunAtTheMeetStep()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            seed: (h, _) =>
            {
                h.Meet.FailOnFirstPage = new GoogleReadFailedException(GoogleReadFailureKind.Configuration, SyncDiagnosis.ScopeNotAuthorized);
                return Task.CompletedTask;
            });

        var row = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Failed, row.Status);
        Assert.Equal("ScopeNotAuthorized", row.LastError);
        Assert.Equal("meet", await host.ScalarAsync<string>("SELECT failed_step FROM sync_state", ct));
        Assert.Null(await WatermarkAsync(host, ct));
    }
}
