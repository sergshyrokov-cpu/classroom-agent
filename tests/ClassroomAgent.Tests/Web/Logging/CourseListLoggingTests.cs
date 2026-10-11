using System.Net;
using System.Text.Json;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Cl = ClassroomAgent.Tests.TestInfrastructure.CourseListHostExtensions;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-020 spec FR-018, I-4, SC-10, AC-013: a listed page logs the total and nothing that names a course or a person; a
/// refused query logs a Warning naming the parameter and never the rejected value; the search text is never logged.
/// </summary>
public sealed class CourseListLoggingTests(PostgreSqlFixture database)
{
    private const string Secret = "zz-secret-zz";

    public static TheoryData<string, string> MalformedQueries => new()
    {
        { "status", CourseListTestData.Url(status: Secret) },
        { "teacherId", CourseListTestData.Url(teacherId: Secret) },
        { "q", CourseListTestData.Url(q: Secret + new string('я', 100)) },
        { "page", CourseListTestData.Url(page: Secret) },
    };

    [Fact]
    public async Task AListedPage_LogsTheTotal_AndNoCourseOrPersonData()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedCourseListAsync(ct);
        var baseline = (await HostLogs.ReadEventsAsync(host.LogDirectory, ct)).Count;

        var page = await client.GetAsync(CourseListTestData.Path, ct);
        Assert.Equal(HttpStatusCode.OK, page.Status);
        await WaitForAsync(
            host,
            baseline,
            e => e.Level == "Information" && HasPropertyValue(e, "2"),
            "an Information event naming the total",
            ct);

        var files = await HostLogs.ReadFilesAsync(host.LogDirectory, ct);
        foreach (var file in files)
        {
            foreach (var value in Cl.SensitiveValues)
            {
                Assert.DoesNotContain(value, file, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task TheSearchText_IsNeverLogged()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedCourseListAsync(ct);
        var baseline = (await HostLogs.ReadEventsAsync(host.LogDirectory, ct)).Count;

        var page = await client.GetAsync(CourseListTestData.Url(q: "zzsecretsearch"), ct);
        Assert.Equal(HttpStatusCode.OK, page.Status);
        await WaitForAsync(host, baseline, e => e.Level == "Information", "an Information event", ct);

        var log = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));
        Assert.DoesNotContain("zzsecretsearch", log, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(MalformedQueries))]
    public async Task AMalformedParameter_LogsAWarningNamingIt_ButNotItsValue(string parameter, string url)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedCourseListAsync(ct);
        var baseline = (await HostLogs.ReadEventsAsync(host.LogDirectory, ct)).Count;

        var page = await client.GetAsync(url, ct);
        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        await WaitForAsync(
            host,
            baseline,
            e => e.Level == "Warning"
                 && (e.Line.Contains(parameter, StringComparison.OrdinalIgnoreCase) || HasPropertyValue(e, parameter)),
            $"a Warning event naming the '{parameter}' parameter",
            ct);

        var log = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));
        Assert.DoesNotContain(Secret, log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownTeacher_LogsAWarningNamingTheParameter_ButNotItsValue()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedCourseListAsync(ct);
        var baseline = (await HostLogs.ReadEventsAsync(host.LogDirectory, ct)).Count;

        var page = await client.GetAsync(CourseListTestData.Url(teacherId: "987654321"), ct);
        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        await WaitForAsync(
            host,
            baseline,
            e => e.Level == "Warning"
                 && (e.Line.Contains("teacherId", StringComparison.OrdinalIgnoreCase) || HasPropertyValue(e, "teacherId")),
            "a Warning event naming the 'teacherId' parameter",
            ct);

        var log = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));
        Assert.DoesNotContain("987654321", log, StringComparison.Ordinal);
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
