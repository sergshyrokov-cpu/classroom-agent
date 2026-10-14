using System.Net;
using System.Text.Json;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-043 FR-011 (SC-10, DC-10): a successful export logs the layout, the per-copy values and the fill method in the
/// <c>JournalExported</c> line, a refused one a <c>JournalExportRefused</c> Warning naming the rule, and no line carries
/// a name, a title or a rejected value.
/// </summary>
public sealed class GroupLessonsBlankLoggingTests(PostgreSqlFixture database)
{
    private const string Exported = "JournalExported";

    private const string Refused = "JournalExportRefused";

    /// <summary>FR-011: the success line names the layout, the per-copy values and the fill method; no personal data.</summary>
    [Fact]
    public async Task ASuccessfulBlankExport_LogsTheLayoutAndValues_AndNoPersonalData()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(GroupLessonsBlank.Body(seeded.CourseId, 7, 5), ct);
        Assert.Equal(HttpStatusCode.OK, response.Status);
        await WaitForAsync(host, e => e.EventName == Exported, ct);

        var events = await ReadEventsAsync(host, ct);
        var line = Assert.Single(events, e => e.EventName == Exported);
        Assert.Equal("Information", line.Level);
        Assert.True(HasPropertyValue(line, "group-lessons-blank"), "The line must name the layout.");
        Assert.True(HasPropertyValue(line, "7"), "The line must record the students per copy.");
        Assert.True(HasPropertyValue(line, "5"), "The line must record the lessons per copy.");
        Assert.Contains("Basic", line.Line, StringComparison.Ordinal);
        var log = string.Join('\n', events.Select(e => e.Line));
        Assert.DoesNotContain(SeededJournal.StudentName, log, StringComparison.Ordinal);
        Assert.DoesNotContain("student.one", log, StringComparison.Ordinal);
        Assert.DoesNotContain(SeededJournal.GradedTitle, log, StringComparison.Ordinal);
        Assert.DoesNotContain(SeededJournal.CourseName, log, StringComparison.Ordinal);
        Assert.DoesNotContain(SeededJournal.TeacherName, log, StringComparison.Ordinal);
    }

    /// <summary>FR-011: the program layout is named in the success line.</summary>
    [Fact]
    public async Task AProgramExport_LogsTheProgramLayout()
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
        Assert.True(HasPropertyValue(line, "program"), "The line must name the layout.");
    }

    /// <summary>FR-011, SC-10: a refused per-copy value is a Warning naming the rule, never the value.</summary>
    [Fact]
    public async Task ARefusedPerCopyValue_IsAWarningNamingTheRule_AndTheValueIsNeverLogged()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(
            GroupLessonsBlank.RawBody(seeded.CourseId, ("layout", "\"group-lessons-blank\""), ("studentsPerCopy", "7777")), ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        await WaitForAsync(host, e => e.EventName == Refused && e.Level == "Warning", ct);

        var events = await ReadEventsAsync(host, ct);
        var refused = Assert.Single(events, e => e.EventName == Refused);
        Assert.Contains("StudentsPerCopyInvalid", refused.Line, StringComparison.Ordinal);
        Assert.DoesNotContain(events, e => e.Line.Contains("7777", StringComparison.Ordinal));
        Assert.DoesNotContain(events, e => e.EventName == Exported);
    }

    /// <summary>FR-011, SC-10: an unknown layout is a Warning naming the rule, never the value.</summary>
    [Fact]
    public async Task ARefusedLayout_IsAWarningNamingTheRule_AndTheValueIsNeverLogged()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(
            GroupLessonsBlank.RawBody(seeded.CourseId, ("layout", "\"zz-layout-zz\"")), ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        await WaitForAsync(host, e => e.EventName == Refused && e.Level == "Warning", ct);

        var events = await ReadEventsAsync(host, ct);
        var refused = Assert.Single(events, e => e.EventName == Refused);
        Assert.Contains("LayoutInvalid", refused.Line, StringComparison.Ordinal);
        Assert.DoesNotContain(events, e => e.Line.Contains("zz-layout-zz", StringComparison.Ordinal));
        Assert.DoesNotContain(events, e => e.EventName == Exported);
    }

    /// <summary>FR-011, OD-011 a: a file that would exceed the sheet limit is refused with a Warning naming the rule.</summary>
    [Fact]
    public async Task TooManySheets_IsAWarningNamingTheRule()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var courseId = await host.SeedBlankCourseAsync(10, 10, ct);

        var response = await client.ExportAsync(GroupLessonsBlank.Body(courseId, 1, 1), ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        await WaitForAsync(host, e => e.EventName == Refused && e.Level == "Warning", ct);

        var events = await ReadEventsAsync(host, ct);
        var refused = Assert.Single(events, e => e.EventName == Refused);
        Assert.Contains("TooManySheets", refused.Line, StringComparison.Ordinal);
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
