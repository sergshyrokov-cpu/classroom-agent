using System.Net;
using System.Text.Json;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Cc = ClassroomAgent.Tests.TestInfrastructure.CourseCardHostExtensions;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-021 spec FR-013, S-07, SC-10, AC-008: a shown card logs the counts and nothing that names a course or a person; a
/// refused id logs a Warning naming the parameter and the outcome and never the value; a value the route does not match
/// is never logged.
/// </summary>
public sealed class CourseCardLoggingTests(PostgreSqlFixture database)
{
    private const string EchoValue = "zz-echo-zz";

    public static TheoryData<string, string> RefusedIds => new()
    {
        { "9223372036854775808", "CourseMalformed" },
        { "8765432109", "CourseUnknown" },
    };

    [Fact]
    public async Task AShownCard_LogsTheCounts_AndNoCourseOrPersonData()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedCourseCardAsync(ct);
        var baseline = (await HostLogs.ReadEventsAsync(host.LogDirectory, ct)).Count;

        var page = await client.GetAsync(CourseCardTestData.Path(world.Physics), ct);
        Assert.Equal(HttpStatusCode.OK, page.Status);
        await WaitForAsync(
            host,
            baseline,
            e => e.Level == "Information" && HasPropertyValue(e, "7") && HasPropertyValue(e, "3"),
            "an Information event naming the student and leaver counts",
            ct);

        var files = await HostLogs.ReadFilesAsync(host.LogDirectory, ct);
        foreach (var file in files)
        {
            foreach (var value in Cc.SensitiveValues)
            {
                Assert.DoesNotContain(value, file, StringComparison.Ordinal);
            }
        }
    }

    [Theory]
    [MemberData(nameof(RefusedIds))]
    public async Task AMalformedOrUnknownId_LogsAWarningNamingTheParameter_ButNotItsValue(string value, string outcome)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedCourseCardAsync(ct);
        var baseline = (await HostLogs.ReadEventsAsync(host.LogDirectory, ct)).Count;

        var page = await client.GetAsync(CourseCardTestData.Path(value), ct);
        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        await WaitForAsync(
            host,
            baseline,
            e => e.Level == "Warning"
                 && (e.Line.Contains("courseId", StringComparison.OrdinalIgnoreCase) || HasPropertyValue(e, "courseId"))
                 && (e.Line.Contains(outcome, StringComparison.Ordinal) || HasPropertyValue(e, outcome)),
            $"a Warning event naming the 'courseId' parameter and the outcome {outcome}",
            ct);

        var log = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));
        Assert.DoesNotContain(value, log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AValueTheRouteDoesNotMatch_IsNeverLogged()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedCourseCardAsync(ct);
        var baseline = (await HostLogs.ReadEventsAsync(host.LogDirectory, ct)).Count;

        var unmatched = await client.GetAsync(CourseCardTestData.Path(EchoValue), ct);
        Assert.Equal(HttpStatusCode.NotFound, unmatched.Status);
        var page = await client.GetAsync(CourseCardTestData.Path(world.Physics), ct);
        Assert.Equal(HttpStatusCode.OK, page.Status);
        await WaitForAsync(
            host,
            baseline,
            e => e.Level == "Information" && HasPropertyValue(e, "7") && HasPropertyValue(e, "3"),
            "an Information event naming the student and leaver counts",
            ct);

        var log = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));
        Assert.DoesNotContain(EchoValue, log, StringComparison.Ordinal);
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
