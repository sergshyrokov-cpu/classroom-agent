using System.Globalization;
using System.Net;
using System.Text.Json;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-027 FR-018: a rejected form logs one Warning naming the field and the rule, a create logs the template id and a
/// built report logs the course; no line carries the entered value, the template name or Google data (SC-10).
/// </summary>
public sealed class ReportTemplateLoggingTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task AValidationFailure_IsLoggedWithoutTheValue()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var marker = "qq-marker-" + new string('x', 101);
        await client.GetAsync(ReportTemplateTestData.NewPath, ct);

        var page = await client.PostFormAsync(
            ReportTemplateTestData.ListPath, ReportTemplateFormBuilder.Valid(marker).Http(), ct);
        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        await WaitForAsync(host, e => e.Level == "Warning", "a Warning event for the rejected form", ct);

        var log = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));
        Assert.DoesNotContain("qq-marker-", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACreate_IsLoggedWithTheTemplateId_NotTheName()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        const string marker = "Test Marker Template Zq";
        await client.GetAsync(ReportTemplateTestData.NewPath, ct);

        var saved = await client.PostFormAsync(
            ReportTemplateTestData.ListPath, ReportTemplateFormBuilder.Valid(marker).Http(), ct);
        Assert.Equal(HttpStatusCode.Redirect, saved.Status);
        var id = Assert.Single(await host.TemplateRowsAsync(ct)).Id.ToString(CultureInfo.InvariantCulture);
        await WaitForAsync(
            host,
            e => e.Level == "Information" && HasPropertyValue(e, id),
            "an Information event naming the template id",
            ct);

        var log = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));
        Assert.DoesNotContain(marker, log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheReport_IsLoggedWithoutGoogleData()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);
        var courseId = seeded.CourseId.ToString(CultureInfo.InvariantCulture);

        var page = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId), ct);
        Assert.Equal(HttpStatusCode.OK, page.Status);
        await WaitForAsync(
            host,
            e => e.Level == "Information" && HasPropertyValue(e, courseId),
            "an Information event naming the course",
            ct);

        var log = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));
        Assert.DoesNotContain(SeededJournal.StudentName, log, StringComparison.Ordinal);
        Assert.DoesNotContain(SeededJournal.CourseName, log, StringComparison.Ordinal);
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
