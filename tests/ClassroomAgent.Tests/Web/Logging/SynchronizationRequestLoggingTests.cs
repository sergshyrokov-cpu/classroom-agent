using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.SynchronizationRequestHostExtensions;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>US-019 FR-010: an accepted press logs one Information line, a refused press one Warning line, neither with personal data (SC-10).</summary>
public sealed class SynchronizationRequestLoggingTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task AnAcceptedPress_LogsOneInformationLine()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        await client.PressSynchronizeAsync(SynchronizationRequestTestData.AdminReturnPage, ct);
        var events = await host.WaitForLogEventAsync(SynchronizationRequestTestData.LogEvents.Requested, ct);

        var line = Assert.Single(events, e => e.EventName == SynchronizationRequestTestData.LogEvents.Requested);
        Assert.Equal("Information", line.Level);
        Assert.DoesNotContain(SignInTestData.AdminEmail, line.Line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ARefusedPress_LogsOneWarningLine()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartAsync(database, Actor.Admin, ct, ReadOnlyModeHost.Cause.Suspended);
        await using var _host = host;

        await client.PressSynchronizeAsync(SynchronizationRequestTestData.AdminReturnPage, ct);
        var events = await host.WaitForLogEventAsync(SynchronizationRequestTestData.LogEvents.Refused, ct);

        var line = Assert.Single(events, e => e.EventName == SynchronizationRequestTestData.LogEvents.Refused);
        Assert.Equal("Warning", line.Level);
    }
}
