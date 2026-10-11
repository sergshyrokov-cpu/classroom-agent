using System.Globalization;
using System.Net;
using System.Text.Json;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Mr = ClassroomAgent.Tests.TestInfrastructure.MeetReportsHostExtensions;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-044 FR-014, AC-012, S-05 (SC-10, DC-10): a successful export is one Information line with ids, counts and the
/// orientation, a refused one a Warning naming the field, and no line carries a name, an email, a meeting code or a
/// rejected value. The events are found by their content, not by an event name or a category.
/// </summary>
public sealed class MeetExportLoggingTests(PostgreSqlFixture database)
{
    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Ids come from sequences that start at 1, like the account ids the sign-in logs; filler courses make the course
    /// under test carry an id no other logged value has.
    /// </summary>
    private static async Task<Mr.SeededWorld> SeedDistinctIdsAsync(InstallationTestHost host, CancellationToken ct)
    {
        await host.WaitForPurgeRunsAsync(1, ct);
        for (var i = 0; i < 5; i++)
        {
            await CourseRows.InsertCourseAsync(
                host, ct, googleId: CourseTestData.CourseId(50 + i), name: $"Test Course Filler {i}");
        }

        return await host.SeedWorldAsync(ct);
    }

    /// <summary>FR-014: the course export line — Information, ids, period, counts and orientation; no personal data.</summary>
    [Fact]
    public async Task ASuccessfulCourseExport_LogsOneInformationLine_WithIdsAndCounts_AndNoPersonalData()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await SeedDistinctIdsAsync(host, ct);
        var baseline = (await HostLogs.ReadEventsAsync(host.LogDirectory, ct)).Count;

        var response = await client.ExportReportsAsync(MeetExportHostExtensions.October(world.CourseId, "landscape"), ct);
        Assert.Equal(HttpStatusCode.OK, response.Status);
        await WaitForAsync(host, baseline, e => IsCourseExportLine(e, world), "the course export Information line", ct);

        var events = (await ReadEventsAsync(host, ct)).Skip(baseline).ToList();
        var line = Assert.Single(events, e => IsCourseExportLine(e, world));
        Assert.Equal("Information", line.Level);
        Assert.Contains("2026-10-01", line.Line, StringComparison.Ordinal);
        Assert.Contains("2026-10-31", line.Line, StringComparison.Ordinal);
        Assert.True(HasPropertyValue(line, "3"), "The line must carry the student row count.");
        Assert.True(HasPropertyValue(line, "2"), "The line must carry the meeting row count.");
        AssertNothingSensitive(await HostLogs.ReadFilesAsync(host.LogDirectory, ct));
    }

    /// <summary>FR-014: the meeting export line — Information, the meeting id; no personal data and no code.</summary>
    [Fact]
    public async Task ASuccessfulMeetingExport_LogsOneInformationLine_WithTheMeetingId_AndNoPersonalDataOrCode()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await SeedDistinctIdsAsync(host, ct);
        var baseline = (await HostLogs.ReadEventsAsync(host.LogDirectory, ct)).Count;

        var response = await client.ExportMeetingAsync(MeetExportHostExtensions.Meeting(world.CourseId, world.MeetingA, "landscape"), ct);
        Assert.Equal(HttpStatusCode.OK, response.Status);
        await WaitForAsync(host, baseline, e => IsMeetingExportLine(e, world), "the meeting export Information line", ct);

        var events = (await ReadEventsAsync(host, ct)).Skip(baseline).ToList();
        var line = Assert.Single(events, e => IsMeetingExportLine(e, world));
        Assert.Equal("Information", line.Level);
        AssertNothingSensitive(await HostLogs.ReadFilesAsync(host.LogDirectory, ct));
    }

    /// <summary>AC-012: a refused period is a Warning naming the field, never the value.</summary>
    [Fact]
    public async Task ARefusedCourseExport_IsAWarningNamingTheField_AndTheValueIsNeverLogged()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await SeedDistinctIdsAsync(host, ct);
        var baseline = (await HostLogs.ReadEventsAsync(host.LogDirectory, ct)).Count;
        var body = MeetExportHostExtensions.October(world.CourseId);
        body["from"] = "2026-13-01";

        var response = await client.ExportReportsAsync(body, ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        await WaitForAsync(
            host, baseline, e => e.Level == "Warning" && ContainsIgnoringCase(e.Line, "from"), "a Warning naming 'from'", ct);

        var log = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));
        Assert.DoesNotContain("2026-13-01", log, StringComparison.Ordinal);
    }

    /// <summary>AC-012: a malformed meeting id is a Warning naming the field, never the value.</summary>
    [Fact]
    public async Task ARefusedMeetingExport_IsAWarningNamingTheField_AndTheValueIsNeverLogged()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await SeedDistinctIdsAsync(host, ct);
        var baseline = (await HostLogs.ReadEventsAsync(host.LogDirectory, ct)).Count;
        var body = MeetExportHostExtensions.Meeting(world.CourseId, world.MeetingA);
        body["meetingId"] = "abc12";

        var response = await client.ExportMeetingAsync(body, ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        await WaitForAsync(
            host, baseline, e => e.Level == "Warning" && ContainsIgnoringCase(e.Line, "meetingId"), "a Warning naming 'meetingId'", ct);

        var log = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));
        Assert.DoesNotContain("abc12", log, StringComparison.Ordinal);
    }

    private static bool IsCourseExportLine(LogEvent e, Mr.SeededWorld world) =>
        e.Level == "Information"
        && HasPropertyValue(e, Id(world.CourseId))
        && ContainsIgnoringCase(e.Line, "landscape")
        && e.Line.Contains("2026-10-31", StringComparison.Ordinal);

    private static bool IsMeetingExportLine(LogEvent e, Mr.SeededWorld world) =>
        e.Level == "Information"
        && HasPropertyValue(e, Id(world.MeetingA))
        && HasPropertyValue(e, Id(world.CourseId))
        && ContainsIgnoringCase(e.Line, "landscape");

    private static bool ContainsIgnoringCase(string text, string value) =>
        text.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static void AssertNothingSensitive(IReadOnlyList<string> files)
    {
        var log = string.Join('\n', files);
        foreach (var value in Mr.SensitiveValues)
        {
            Assert.DoesNotContain(value, log, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool HasPropertyValue(LogEvent e, string value) =>
        e.Json.EnumerateObject().Any(p =>
            !p.Name.StartsWith('@')
            && (p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText()) == value);

    private static async Task<IReadOnlyList<LogEvent>> ReadEventsAsync(InstallationTestHost host, CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                return LogEvent.Parse(await host.ReadLogFilesWhileRunningAsync(cancellationToken));
            }
            catch (JsonException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
            }
        }
    }

    /// <summary>Polls the running host's log (real time, bounded) until an event after the baseline matches; a torn line is retried.</summary>
    private static async Task WaitForAsync(
        InstallationTestHost host,
        int baseline,
        Func<LogEvent, bool> match,
        string description,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + ManualTimeProvider.RealTimeLimit;
        while (true)
        {
            try
            {
                var events = LogEvent.Parse(await host.ReadLogFilesWhileRunningAsync(cancellationToken));
                if (events.Skip(baseline).Any(match))
                {
                    return;
                }
            }
            catch (JsonException)
            {
            }

            if (DateTime.UtcNow >= deadline)
            {
                Assert.Fail($"Timed out waiting for {description}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
        }
    }
}
