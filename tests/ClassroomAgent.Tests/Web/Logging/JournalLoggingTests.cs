using System.Text.Json;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-025 FR-016: a built journal logs one Information event naming the course, a malformed query one Warning event
/// naming the parameter; neither line carries personal data, a title, a grade or the rejected value (SC-10).
/// </summary>
public sealed class JournalLoggingTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task ABuiltJournal_LogsTheCourse_AndNoPersonalDataOrGrades()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(JournalTestData.SeptemberUrl(seeded.CourseId), ct);
        Assert.Equal(System.Net.HttpStatusCode.OK, page.Status);
        var courseId = seeded.CourseId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await WaitForAsync(
            host,
            e => e.Level == "Information" && HasPropertyValue(e, courseId),
            "an Information event naming the course",
            ct);

        var log = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));
        Assert.DoesNotContain(SeededJournal.StudentName, log, StringComparison.Ordinal);
        Assert.DoesNotContain(SeededJournal.SilentStudentName, log, StringComparison.Ordinal);
        Assert.DoesNotContain(SeededJournal.GradedTitle, log, StringComparison.Ordinal);
        Assert.DoesNotContain(CourseTestData.Email("student.one"), log, StringComparison.OrdinalIgnoreCase);

        // The grade is looked for outside the timestamp, whose fractional seconds can contain "8.5" by chance.
        var withoutTimestamps = string.Join(
            '\n',
            LogEvent.Parse(await HostLogs.ReadFilesAsync(host.LogDirectory, ct))
                .Select(e => string.Join(' ', e.Json.EnumerateObject().Where(p => p.Name != "@t").Select(p => p.Value.GetRawText()))));
        Assert.DoesNotContain("8.5", withoutTimestamps, StringComparison.Ordinal);
        Assert.DoesNotContain("8,5", withoutTimestamps, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMalformedParameter_LogsAWarningNamingIt_ButNotItsValue()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(JournalTestData.Url(seeded.CourseId, from: "zz-secret-zz", to: JournalTestData.Period.ToText), ct);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, page.Status);
        await WaitForAsync(
            host,
            e => e.Level == "Warning" && e.Line.Contains("from", StringComparison.OrdinalIgnoreCase),
            "a Warning event naming the 'from' parameter",
            ct);

        var log = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));
        Assert.DoesNotContain("zz-secret-zz", log, StringComparison.Ordinal);
    }

    private static bool HasPropertyValue(LogEvent e, string value) =>
        e.Json.EnumerateObject().Any(p =>
            !p.Name.StartsWith('@')
            && (p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText()) == value);

    /// <summary>Polls the running host's log (real time, bounded) until an event matches; a torn line is retried.</summary>
    private static async Task WaitForAsync(
        InstallationTestHost host,
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
                if (events.Any(match))
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
