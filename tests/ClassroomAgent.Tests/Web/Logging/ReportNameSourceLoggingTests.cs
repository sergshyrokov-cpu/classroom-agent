using System.Net;
using System.Text.Json;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-042 FR-011, AC-010, S-06, S-07: the built-report line records the effective name source and its origin, a refused
/// source is a Warning naming the rule, and no line carries a name, an email part or a rejected value (SC-10).
/// </summary>
public sealed class ReportNameSourceLoggingTests(PostgreSqlFixture database)
{
    private const string ReportBuilt = "ReportBuilt";

    private const string ReportQueryRefused = "ReportQueryRefused";

    [Fact]
    public async Task TheBuiltReport_RecordsThePageSourceAndItsOrigin()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(
            ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId, names: "email"), ct);
        Assert.Equal(HttpStatusCode.OK, page.Status);
        await WaitForAsync(host, e => e.EventName == ReportBuilt, "the ReportBuilt event", ct);

        var built = Assert.Single(await ReadEventsAsync(host, ct), e => e.EventName == ReportBuilt);
        Assert.True(HasPropertyValue(built, "email"), "The line must record the effective source.");
        Assert.True(HasPropertyValue(built, "Page"), "The line must record that the page chose it.");
    }

    [Fact]
    public async Task TheBuiltReport_RecordsTheTemplateSourceAndItsOrigin_WhenThePageHasNone()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId), ct);
        Assert.Equal(HttpStatusCode.OK, page.Status);
        await WaitForAsync(host, e => e.EventName == ReportBuilt, "the ReportBuilt event", ct);

        var built = Assert.Single(await ReadEventsAsync(host, ct), e => e.EventName == ReportBuilt);
        Assert.True(HasPropertyValue(built, "profile"), "The line must record the effective source.");
        Assert.True(HasPropertyValue(built, "Template"), "The line must record that the template chose it.");
    }

    [Fact]
    public async Task AMalformedSource_IsAWarningNamingTheRule_AndTheValueIsNeverLogged()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(
            ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId, names: "zz-bad-zz"), ct);
        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        await WaitForAsync(
            host, e => e.EventName == ReportQueryRefused && e.Level == "Warning", "the ReportQueryRefused warning", ct);

        var events = await ReadEventsAsync(host, ct);
        var refused = Assert.Single(events, e => e.EventName == ReportQueryRefused);
        Assert.Contains("NameSourceMalformed", refused.Line, StringComparison.Ordinal);
        Assert.DoesNotContain(events, e => e.Line.Contains("zz-bad-zz", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NoLogLine_CarriesAProfileNameOrAnEmailPart()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);
        await host.ExecuteAsync(
            "UPDATE classroom_participant SET surname = @surname, given_name = @given WHERE id = @id",
            ct,
            ("surname", "Тестова"),
            ("given", "Олена"),
            ("id", seeded.StudentId));

        var page = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId), ct);
        Assert.Equal(HttpStatusCode.OK, page.Status);
        await WaitForAsync(host, e => e.EventName == ReportBuilt, "the ReportBuilt event", ct);

        var log = string.Join('\n', (await ReadEventsAsync(host, ct)).Select(e => e.Line));
        Assert.DoesNotContain("Тестова", log, StringComparison.Ordinal);
        Assert.DoesNotContain("Олена", log, StringComparison.Ordinal);
        Assert.DoesNotContain("student.one", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARejectedFormValue_IsNeverLogged()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await client.GetAsync(ReportTemplateTestData.NewPath, ct);
        var form = ReportTemplateFormBuilder.Valid("Test Template Bad Source")
            .Set(ReportTemplateTestData.NamesField, "bad-value-zz");

        var page = await client.PostFormAsync(ReportTemplateTestData.ListPath, form.Http(), ct);
        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        await WaitForAsync(host, e => e.Level == "Warning", "a Warning event for the rejected form", ct);

        var log = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));
        Assert.DoesNotContain("bad-value-zz", log, StringComparison.Ordinal);
    }

    /// <summary>True when a property (not a Serilog <c>@</c> field, so not the message template) has that value.</summary>
    private static bool HasPropertyValue(LogEvent e, string value) =>
        e.Json.EnumerateObject().Any(p =>
            !p.Name.StartsWith('@')
            && string.Equals(
                p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText(),
                value,
                StringComparison.OrdinalIgnoreCase));

    private static async Task<IReadOnlyList<LogEvent>> ReadEventsAsync(
        InstallationTestHost host, CancellationToken cancellationToken)
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
