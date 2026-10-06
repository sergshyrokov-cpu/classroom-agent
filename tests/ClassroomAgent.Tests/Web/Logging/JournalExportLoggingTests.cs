using System.Net;
using System.Text.Json;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-028 FR-012, AC-009, S-05 (api-design §3; SC-10, DC-10): a successful export is one <c>JournalExported</c>
/// Information line with ids and counts, a refused one a <c>JournalExportRefused</c> Warning naming the field and rule,
/// and no line carries a name, an email, a title or a rejected value.
/// </summary>
public sealed class JournalExportLoggingTests(PostgreSqlFixture database)
{
    private const string Exported = "JournalExported";

    private const string Refused = "JournalExportRefused";

    /// <summary>FR-012: the success line — Information, the course id, the counts and the size; no personal data.</summary>
    [Fact]
    public async Task ASuccessfulExport_LogsOneInformationLine_WithIdsAndCounts_AndNoPersonalData()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(JournalExportHostExtensions.September(seeded.CourseId), ct);
        Assert.Equal(HttpStatusCode.OK, response.Status);
        await WaitForAsync(host, e => e.EventName == Exported, ct);

        var events = await ReadEventsAsync(host, ct);
        var line = Assert.Single(events, e => e.EventName == Exported);
        Assert.Equal("Information", line.Level);
        Assert.True(HasPropertyValue(line, seeded.CourseId.ToString(System.Globalization.CultureInfo.InvariantCulture)), "The line must name the course id.");
        Assert.True(HasPropertyValue(line, response.Bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)), "The line must record the file size.");
        var log = string.Join('\n', events.Select(e => e.Line));
        Assert.DoesNotContain(SeededJournal.StudentName, log, StringComparison.Ordinal);
        Assert.DoesNotContain("student.one", log, StringComparison.Ordinal);
        Assert.DoesNotContain(SeededJournal.GradedTitle, log, StringComparison.Ordinal);
        Assert.DoesNotContain(SeededJournal.CourseName, log, StringComparison.Ordinal);
    }

    /// <summary>FR-012, S-05: a refused value is a Warning naming the field and the rule, never the value.</summary>
    [Fact]
    public async Task ARefusedValue_IsAWarningNamingTheRule_AndTheValueIsNeverLogged()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(
            JournalExportHostExtensions.September(seeded.CourseId, orientation: "zz-bad-orientation-zz"), ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        await WaitForAsync(host, e => e.EventName == Refused && e.Level == "Warning", ct);

        var events = await ReadEventsAsync(host, ct);
        var refused = Assert.Single(events, e => e.EventName == Refused);
        Assert.Contains("OrientationInvalid", refused.Line, StringComparison.Ordinal);
        Assert.DoesNotContain(events, e => e.Line.Contains("zz-bad-orientation-zz", StringComparison.Ordinal));
        Assert.DoesNotContain(events, e => e.EventName == Exported);
    }

    private static bool HasPropertyValue(LogEvent e, string value) =>
        e.Json.EnumerateObject().Any(p =>
            !p.Name.StartsWith('@')
            && string.Equals(
                p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText(),
                value,
                StringComparison.Ordinal));

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

    /// <summary>Polls the running host's log (real time, bounded) until an event matches; a torn line is retried.</summary>
    private static async Task WaitForAsync(InstallationTestHost host, Func<LogEvent, bool> match, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + ManualTimeProvider.RealTimeLimit;
        while (true)
        {
            try
            {
                if (LogEvent.Parse(await host.ReadLogFilesWhileRunningAsync(cancellationToken)).Any(match))
                {
                    return;
                }
            }
            catch (JsonException)
            {
            }

            if (DateTime.UtcNow >= deadline)
            {
                Assert.Fail("Timed out waiting for the export log event.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
    }
}
