using System.Globalization;
using System.Net;
using System.Text.Json;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Data = ClassroomAgent.Tests.TestInfrastructure.DatabaseStatisticsTestData;
using Sr = ClassroomAgent.Tests.TestInfrastructure.SynchronizationRequestHostExtensions;
using SrActor = ClassroomAgent.Tests.TestInfrastructure.SynchronizationRequestHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-024 spec FR-013, SC-10, S-10: opening the page logs one Information event with the actor id and nothing that names
/// a course, a person or a meeting code; the ignored query string is never logged; an accepted press logs the US-019
/// "SynchronizationRequested" event.
/// </summary>
public sealed class DatabaseStatisticsLoggingTests(PostgreSqlFixture database)
{
    private const string Marker = "zz-marker-zz";

    /// <summary>FR-013: opening the page logs an Information event naming the actor id and no personal data.</summary>
    [Fact]
    public async Task OpeningThePage_LogsTheActor_AndNoSchoolData()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedStatisticsWorldAsync(ct);
        var adminId = await host.ScalarAsync<long>(
            "SELECT id FROM app_user WHERE email = @e", ct, ("e", SignInTestData.AdminEmail));
        var baseline = (await HostLogs.ReadEventsAsync(host.LogDirectory, ct)).Count;

        var page = await client.GetAsync(Data.Path, ct);
        Assert.Equal(HttpStatusCode.OK, page.Status);
        await WaitForAsync(
            host,
            baseline,
            e => e.Level == "Information" && HasPropertyValue(e, adminId.ToString(CultureInfo.InvariantCulture)),
            "an Information event naming the actor",
            ct);

        foreach (var file in await HostLogs.ReadFilesAsync(host.LogDirectory, ct))
        {
            foreach (var value in DatabaseStatisticsHostExtensions.SensitiveValues)
            {
                Assert.DoesNotContain(value, file, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>FR-013, SC-10: a parameter the page ignores is never logged.</summary>
    [Fact]
    public async Task AnIgnoredParameter_IsNeverLogged()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedStatisticsWorldAsync(ct);
        var baseline = (await HostLogs.ReadEventsAsync(host.LogDirectory, ct)).Count;

        var page = await client.GetAsync(Data.Path + "?x=" + Marker, ct);
        Assert.Equal(HttpStatusCode.OK, page.Status);
        await WaitForAsync(host, baseline, e => e.Level == "Information", "an Information event", ct);

        var log = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));
        Assert.DoesNotContain(Marker, log, StringComparison.Ordinal);
    }

    /// <summary>FR-013: an accepted press logs "SynchronizationRequested" and no school data.</summary>
    [Fact]
    public async Task AnAcceptedPress_LogsTheRequestedEvent_AndNoSchoolData()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await Sr.StartAsync(database, SrActor.Admin, ct);
        await using var _host = host;
        await host.SeedStatisticsWorldAsync(ct);
        var page = await client.GetAsync(Data.Path, ct);
        if (!Html.HasInput(page.Body, Html.AntiforgeryFieldName))
        {
            await client.GetAsync(SignInTestData.LandingPath, ct);
        }

        var response = await client.PostFormAsync(Data.PressPath, [], ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        await HostLogs.WaitForEventAsync(host.LogDirectory, SynchronizationRequestTestData.LogEvents.Requested, 1, ct);
        foreach (var file in await HostLogs.ReadFilesAsync(host.LogDirectory, ct))
        {
            foreach (var value in DatabaseStatisticsHostExtensions.SensitiveValues)
            {
                Assert.DoesNotContain(value, file, StringComparison.Ordinal);
            }
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
