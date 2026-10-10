using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.MeetTestData;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-031 AC-012, FR-012, SC-10, I-6: what the Meet step of a run writes to the log. The Meet port is substituted and
/// seeded before the host starts (TC-4); the host, the background service and PostgreSQL are real, and every assertion
/// waits for the log line itself (the known stub-wait race).
/// </summary>
public sealed class MeetPullLoggingTests(PostgreSqlFixture database)
{
    private static readonly DateTimeOffset T0 = InstallationTestHost.DefaultStart - TimeSpan.FromDays(2);

    private static DateTimeOffset At(int minutes) => T0 + TimeSpan.FromMinutes(minutes);

    private static Task<InstallationTestHost> StartWithMeetAsync(
        PostgreSqlFixture database,
        CancellationToken cancellationToken,
        Action<FakeMeetReportsReader> meet) =>
        SyncHostExtensions.StartAsync(
            database,
            cancellationToken,
            seed: (host, _) =>
            {
                meet(host.Meet);
                return Task.CompletedTask;
            });

    /// <summary>Every value an event carries that must never reach a log line.</summary>
    private static IEnumerable<string> SensitiveValues(IEnumerable<MeetCallEndedEvent> events) =>
        events
            .SelectMany(e => new[] { e.ConferenceId, e.MeetingCode, e.OrganizerEmail, e.EndpointId, e.Identifier })
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal);

    private static readonly MeetCallEndedEvent[] SeededEvents =
    [
        Event(1, 1, At(60), 3600, Teacher(1), Teacher(1)),
        Event(1, 2, At(50), 2400, Teacher(1), Student(1)),
        Event(2, 3, At(80), 1200, $"someone@{OtherDomain}", $"guest@{OtherDomain}"),
    ];

    /// <summary>
    /// AC-012, FR-012: a completed Meet step writes one <b>Information</b> line with the run id, the window and the
    /// counts of the seeded page.
    /// </summary>
    [Fact]
    public async Task ACompletedMeetStep_IsLoggedOnce_WithTheWindowAndTheCounts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWithMeetAsync(database, ct, meet => meet.WithPage(SeededEvents));

        var events = await host.WaitForLogEventAsync(LogEvents.StepCompleted, ct);
        var row = await host.WaitForFinishedRunAsync(ct);

        var line = Assert.Single(events, e => e.EventName == LogEvents.StepCompleted);
        Assert.Equal("Information", line.Level);
        Assert.Equal(row.RunId.ToString(), line.Property(LogProperties.RunId));
        Assert.Equal("3", line.Property(LogProperties.EventsRead));
        Assert.Equal("1", line.Property(LogProperties.SessionsAdded));
        Assert.Equal("2", line.Property(LogProperties.ParticipationsAdded));
        Assert.Equal("1", line.Property(LogProperties.NotOfTheSchool));
        Assert.NotNull(line.Property(LogProperties.WindowFrom));
        Assert.NotNull(line.Property(LogProperties.WindowTo));
    }

    /// <summary>
    /// AC-012, FR-012, SC-10: no line of the whole log carries an email, meeting code, conference id or endpoint id of
    /// the seeded events.
    /// </summary>
    [Fact]
    public async Task NoLogLine_CarriesAnyValueOfTheEvents()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWithMeetAsync(database, ct, meet => meet.WithPage(SeededEvents));

        await host.WaitForLogEventAsync(LogEvents.StepCompleted, ct);
        await host.WaitForFinishedRunAsync(ct);
        var lines = (await HostLogs.ReadEventsAsync(host.LogDirectory, ct)).Select(e => e.Line).ToList();

        Assert.NotEmpty(lines);
        foreach (var value in SensitiveValues(SeededEvents))
        {
            Assert.All(lines, line => Assert.DoesNotContain(value, line, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// AC-012, FR-012, VR-001: an invalid event is skipped with one <b>Warning</b> carrying the reason and the count —
    /// and none of the invalid event's values.
    /// </summary>
    [Fact]
    public async Task AnInvalidEvent_IsLoggedAsOneWarning_WithTheReasonAndTheCountOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        var invalid = Event(9, 9, At(30), 600, Teacher(9), Student(9)) with { MeetingCode = null };
        var valid = Event(1, 1, At(60), 3600, Teacher(1), Teacher(1));
        await using var host = await StartWithMeetAsync(database, ct, meet => meet.WithPage(invalid, valid));

        var events = await host.WaitForLogEventAsync(LogEvents.EventsSkipped, ct);
        await host.WaitForFinishedRunAsync(ct);

        var line = Assert.Single(events, e => e.EventName == LogEvents.EventsSkipped);
        Assert.Equal("Warning", line.Level);
        Assert.Equal("MeetingCode", line.Property(LogProperties.Reason));
        Assert.Equal("1", line.Property(LogProperties.Count));
        foreach (var value in SensitiveValues([invalid]))
        {
            Assert.DoesNotContain(value, line.Line, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC-012, FR-010, FR-012: a configuration failure in the Meet step ends in the existing run-failed line at
    /// <b>Error</b>, carrying the step and the diagnosis code.
    /// </summary>
    [Fact]
    public async Task AConfigurationFailureOfTheMeetStep_IsLoggedAtError_WithTheStepAndTheCode()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWithMeetAsync(
            database,
            ct,
            meet => meet.FailOnFirstPage =
                new GoogleReadFailedException(GoogleReadFailureKind.Configuration, SyncDiagnosis.ScopeNotAuthorized));

        var events = await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunFailed, ct);
        var row = await host.WaitForFinishedRunAsync(ct);

        var line = Assert.Single(events, e => e.EventName == SyncTestData.LogEvents.RunFailed);
        Assert.Equal("Error", line.Level);
        Assert.Equal(row.RunId.ToString(), line.Property(LogProperties.RunId));
        Assert.Equal("Meet", line.Property(LogProperties.Step));
        Assert.Contains("ScopeNotAuthorized", line.Line, StringComparison.Ordinal);
    }
}
