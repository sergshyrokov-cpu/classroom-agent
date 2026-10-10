using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.MeetLinkingTestData;
using static ClassroomAgent.Tests.TestInfrastructure.MeetTestData;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-032 FR-006, §9, SC-10: what the linking step writes to the log — counts only. The Meet port is substituted and
/// seeded before the host starts (TC-4); every assertion waits for the log line itself (the known stub-wait race).
/// </summary>
public sealed class MeetLinkingLoggingTests(PostgreSqlFixture database)
{
    private Task<InstallationTestHost> StartAsync(CancellationToken ct) =>
        SyncHostExtensions.StartAsync(
            database,
            ct,
            seed: async (host, _) =>
            {
                await SeedClearRostersAsync(host, ct);
                host.Meet.WithPage(FirstMeetingEvents());
            });

    /// <summary>FR-006: a completed linking step writes one Information line with the run id and the two counts.</summary>
    [Fact]
    public async Task ACompletedLinkingStep_IsLoggedOnce_WithTheCounts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);

        var events = await host.WaitForLogEventAsync(LinkingStepCompleted, ct);
        var row = await host.WaitForFinishedRunAsync(ct);

        var line = Assert.Single(events, e => e.EventName == LinkingStepCompleted);
        Assert.Equal("Information", line.Level);
        Assert.Equal(row.RunId.ToString(), line.Property(MeetTestData.LogProperties.RunId));
        Assert.Equal("1", line.Property(CodesScored));
        Assert.Equal("1", line.Property(LinksCreated));
    }

    /// <summary>SC-10: no line of the whole log carries the code, an organizer or participant email, or a course name.</summary>
    [Fact]
    public async Task NoLogLine_CarriesTheCodeAnEmailOrACourseName()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);

        await host.WaitForLogEventAsync(LinkingStepCompleted, ct);
        await host.WaitForFinishedRunAsync(ct);
        var lines = (await HostLogs.ReadEventsAsync(host.LogDirectory, ct)).Select(e => e.Line).ToList();

        Assert.NotEmpty(lines);
        var sensitive = new List<string> { Code, Organizer, CourseTestData.CourseName(1), CourseTestData.CourseName(2) };
        sensitive.AddRange(Students(1, 20));
        foreach (var value in sensitive)
        {
            Assert.All(lines, line => Assert.DoesNotContain(value, line, StringComparison.Ordinal));
        }
    }
}
