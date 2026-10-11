using System.Globalization;
using System.Net;
using System.Text.Json;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Mr = ClassroomAgent.Tests.TestInfrastructure.MeetReportsHostExtensions;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-033 FR-013, db-design §6, SC-10: a built page logs the course id and counts only, never an email, a name or a
/// meeting code; a refused query logs a Warning naming the parameter and never the rejected value.
/// </summary>
public sealed class MeetReportsLoggingTests(PostgreSqlFixture database)
{
    /// <summary>
    /// Course ids come from a sequence that starts at 1, like the account ids the sign-in logs; a few filler courses make
    /// the course under test carry an id no other logged value has.
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

    [Fact]
    public async Task ABuiltReportsPage_LogsTheCourse_AndNoPersonalDataOrCode()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await SeedDistinctIdsAsync(host, ct);
        var baseline = (await HostLogs.ReadEventsAsync(host.LogDirectory, ct)).Count;

        var page = await client.GetAsync(MeetReportsTestData.OctoberUrl(world.CourseId), ct);
        Assert.Equal(HttpStatusCode.OK, page.Status);
        var courseId = world.CourseId.ToString(CultureInfo.InvariantCulture);
        await WaitForAsync(
            host,
            baseline,
            e => e.Level == "Information" && HasPropertyValue(e, courseId),
            "an Information event naming the course",
            ct);

        AssertNothingSensitive(await HostLogs.ReadFilesAsync(host.LogDirectory, ct));
    }

    [Fact]
    public async Task ABuiltMeetingPage_LogsTheCourse_AndNoPersonalDataOrCode()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await SeedDistinctIdsAsync(host, ct);
        var baseline = (await HostLogs.ReadEventsAsync(host.LogDirectory, ct)).Count;

        var page = await client.GetAsync(
            MeetReportsTestData.MeetingUrl(
                world.MeetingA.ToString(CultureInfo.InvariantCulture), world.CourseId,
                MeetReportsTestData.Period.FromText, MeetReportsTestData.Period.ToText),
            ct);
        Assert.Equal(HttpStatusCode.OK, page.Status);
        var courseId = world.CourseId.ToString(CultureInfo.InvariantCulture);
        await WaitForAsync(
            host,
            baseline,
            e => e.Level == "Information" && HasPropertyValue(e, courseId),
            "an Information event naming the course",
            ct);

        AssertNothingSensitive(await HostLogs.ReadFilesAsync(host.LogDirectory, ct));
    }

    [Theory]
    [InlineData("courseId", "zz-secret-zz", "")]
    [InlineData("from", "zz-secret-zz", "&to=2026-10-31")]
    [InlineData("to", "zz-secret-zz", "&from=2026-10-01")]
    public async Task AMalformedParameter_LogsAWarningNamingIt_ButNotItsValue(string parameter, string value, string rest)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await SeedDistinctIdsAsync(host, ct);
        var baseline = (await HostLogs.ReadEventsAsync(host.LogDirectory, ct)).Count;
        var path = parameter == "courseId"
            ? $"/workspace/meet-reports?courseId={value}{rest}"
            : $"/workspace/meet-reports?courseId={world.CourseId}&{parameter}={value}{rest}";

        var page = await client.GetAsync(path, ct);
        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        await WaitForAsync(
            host,
            baseline,
            e => e.Level == "Warning" && e.Line.Contains(parameter, StringComparison.OrdinalIgnoreCase),
            $"a Warning event naming the '{parameter}' parameter",
            ct);

        var log = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));
        Assert.DoesNotContain(value, log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMalformedCourseIdOnTheMeetingPage_LogsAWarningNamingIt_ButNotItsValue()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await SeedDistinctIdsAsync(host, ct);
        var baseline = (await HostLogs.ReadEventsAsync(host.LogDirectory, ct)).Count;

        var page = await client.GetAsync($"/workspace/meet-reports/meetings/{world.MeetingA}?courseId=zz-secret-zz", ct);
        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        await WaitForAsync(
            host,
            baseline,
            e => e.Level == "Warning" && e.Line.Contains("courseId", StringComparison.OrdinalIgnoreCase),
            "a Warning event naming the 'courseId' parameter",
            ct);

        var log = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));
        Assert.DoesNotContain("zz-secret-zz", log, StringComparison.Ordinal);
    }

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
