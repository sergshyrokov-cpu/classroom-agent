using System.Net;
using System.Text.Json;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Tl = ClassroomAgent.Tests.TestInfrastructure.TeacherListHostExtensions;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-022 spec FR-015, SC-10, AC-013: a listed page logs the total and nothing that names a course or a person; a
/// refused query logs a Warning naming the parameter and never the rejected value; ignored parameters are never logged.
/// </summary>
public sealed class TeacherListLoggingTests(PostgreSqlFixture database)
{
    private const string Secret = "zz-secret-zz";

    public static TheoryData<string, string> MalformedQueries => new()
    {
        { "status", TeacherListTestData.Url(status: Secret) },
        { "page", TeacherListTestData.Url(page: Secret) },
    };

    /// <summary>FR-015: a listed page logs the total as an Information event and no name, email or course.</summary>
    [Fact]
    public async Task AListedPage_LogsTheTotal_AndNoTeacherData()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedTeacherListAsync(ct);
        var baseline = (await HostLogs.ReadEventsAsync(host.LogDirectory, ct)).Count;

        var page = await client.GetAsync(TeacherListTestData.Path, ct);
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
            foreach (var value in Tl.SensitiveValues)
            {
                Assert.DoesNotContain(value, file, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>AC-013, FR-015: a malformed parameter logs a Warning naming it, never its value.</summary>
    [Theory]
    [MemberData(nameof(MalformedQueries))]
    public async Task AMalformedParameter_LogsAWarningNamingIt_ButNotItsValue(string parameter, string url)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedTeacherListAsync(ct);
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

    /// <summary>FR-015, SC-10: parameters the page ignores are never logged.</summary>
    [Fact]
    public async Task IgnoredParameters_AreNeverLogged()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedTeacherListAsync(ct);
        var baseline = (await HostLogs.ReadEventsAsync(host.LogDirectory, ct)).Count;

        var page = await client.GetAsync(TeacherListTestData.Path + "?teacherId=zz-tid-zz&q=zz-q-zz", ct);
        Assert.Equal(HttpStatusCode.OK, page.Status);
        await WaitForAsync(host, baseline, e => e.Level == "Information", "an Information event", ct);

        var log = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));
        Assert.DoesNotContain("zz-tid-zz", log, StringComparison.Ordinal);
        Assert.DoesNotContain("zz-q-zz", log, StringComparison.Ordinal);
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
