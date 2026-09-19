using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.ControlPlane.Push;

/// <summary>
/// US-006 AC-005, AC-006: the Control Plane logs a delivered push at <c>Information</c>, each failed
/// attempt with its number and category at <c>Warning</c>, the exhausted retries, a refusal and a status
/// change without an address — and never the address, the domain or a response body (spec FR-011;
/// api-design §9; SC-10, DC-10). Event names are fixed by the test strategy §3.
/// </summary>
public sealed class StatusPushLoggingTests(PostgreSqlFixture database)
{
    private static readonly TimeSpan FirstPause = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SecondPause = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ThirdPause = TimeSpan.FromMinutes(2);

    [Fact]
    public async Task DeliveredPush_IsLoggedAtInformation_WithTheInstallationId()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, stub, _) = await StartAsync(database, ct);
        await using var _host = host;
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);
        var internalId = (await host.InstallationAsync(installation, ct))!.Id;

        await owner.SuspendInstallationAsync(installation, ct);
        await stub.WaitForAttemptsAsync(1, ct);

        var events = await host.ReadLogEventsAsync(ct);
        var delivered = Assert.Single(events, e => e.EventName == "StatusPushDelivered");
        Assert.Equal("Information", delivered.Level);
        Assert.Equal(internalId.ToString(System.Globalization.CultureInfo.InvariantCulture), delivered.Property("InstallationId"));
    }

    [Fact]
    public async Task RefusedPush_IsLoggedAtWarning_AndNotRetried()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, stub, clock) = await StartAsync(database, ct);
        await using var _host = host;
        stub.AlwaysAnswer(HttpStatusCode.NotFound);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        await owner.SuspendInstallationAsync(installation, ct);
        await stub.WaitForAttemptsAsync(1, ct);
        clock.Advance(ThirdPause);

        var events = await host.ReadLogEventsAsync(ct);
        var refused = Assert.Single(events, e => e.EventName == "StatusPushRefused");
        Assert.Equal("Warning", refused.Level);
        Assert.DoesNotContain(events, e => e.EventName == "StatusPushAttemptFailed");
        Assert.DoesNotContain(events, e => e.EventName == "StatusPushAbandoned");
    }

    [Fact]
    public async Task EveryFailedAttempt_IsLoggedWithItsNumberAndCategory_ThenTheRetriesAreAbandoned()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, stub, clock) = await StartAsync(database, ct);
        await using var _host = host;
        stub.AlwaysAnswer(HttpStatusCode.InternalServerError);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        await owner.SuspendInstallationAsync(installation, ct);
        await stub.WaitForAttemptsAsync(1, ct);
        clock.Advance(FirstPause);
        await stub.WaitForAttemptsAsync(2, ct);
        clock.Advance(SecondPause);
        await stub.WaitForAttemptsAsync(3, ct);
        clock.Advance(ThirdPause);
        await stub.WaitForAttemptsAsync(4, ct);

        var events = await host.ReadLogEventsAsync(ct);
        var failed = events.Where(e => e.EventName == "StatusPushAttemptFailed").ToList();
        Assert.Equal(4, failed.Count);
        Assert.All(failed, e =>
        {
            Assert.Equal("Warning", e.Level);
            Assert.Equal("UnexpectedStatus", e.Property("Category"));
            Assert.Equal("500", e.Property("StatusCode"));
        });
        Assert.Equal(["1", "2", "3", "4"], failed.Select(e => e.Property("Attempt")).ToArray());
        var abandoned = Assert.Single(events, e => e.EventName == "StatusPushAbandoned");
        Assert.Equal("Warning", abandoned.Level);
    }

    [Fact]
    public async Task ConnectionFailure_IsLoggedWithItsCategory_AndNoStatusCode()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, stub, _) = await StartAsync(database, ct);
        await using var _host = host;
        stub.FailToConnect().Answer(HttpStatusCode.Accepted);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        await owner.SuspendInstallationAsync(installation, ct);
        await stub.WaitForAttemptsAsync(1, ct);

        var events = await host.ReadLogEventsAsync(ct);
        var failed = Assert.Single(events, e => e.EventName == "StatusPushAttemptFailed");
        Assert.Equal("ConnectionFailed", failed.Property("Category"));
        Assert.True(failed.Property("StatusCode") is null or "null");
    }

    [Fact]
    public async Task StatusChangeWithoutAnAddress_IsLoggedAtWarning()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, stub, _) = await StartAsync(database, ct);
        await using var _host = host;
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct, pushAddress: null);
        var internalId = (await host.InstallationAsync(installation, ct))!.Id;

        await owner.SuspendInstallationAsync(installation, ct);
        await stub.AssertNoAttemptAsync(ct);

        var events = await host.ReadLogEventsAsync(ct);
        var noAddress = Assert.Single(events, e => e.EventName == "StatusPushNoAddress");
        Assert.Equal("Warning", noAddress.Level);
        Assert.Equal(internalId.ToString(System.Globalization.CultureInfo.InvariantCulture), noAddress.Property("InstallationId"));
    }

    [Fact]
    public async Task NoLogLineCarriesTheAddress_TheDomainOrTheResponseBody()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, stub, clock) = await StartAsync(database, ct);
        await using var _host = host;
        stub.AlwaysAnswer(HttpStatusCode.InternalServerError);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        await owner.SuspendInstallationAsync(installation, ct);
        await stub.WaitForAttemptsAsync(1, ct);
        clock.Advance(FirstPause);
        await stub.WaitForAttemptsAsync(2, ct);

        var logs = await host.ReadLogFilesAsync(ct);
        Assert.DoesNotContain(logs, log => log.Contains(PushTestData.Address, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logs, log => log.Contains("10.0.0.5", StringComparison.Ordinal));
        Assert.DoesNotContain(logs, log => log.Contains(InstallationTestData.Domain, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logs, log => log.Contains(InstallationTestData.ClientId, StringComparison.Ordinal));
    }

    private static async Task<(ControlPlaneTestHost Host, PushClientStub Stub, ManualTimeProvider Clock)> StartAsync(
        PostgreSqlFixture database,
        CancellationToken cancellationToken)
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 17, 9, 0, 0, TimeSpan.Zero));
        var stub = new PushClientStub(clock);
        var host = await ControlPlaneTestHost.StartAsync(
            database,
            cancellationToken,
            manualTime: clock,
            configureServices: services => services
                .AddHttpClient(PushTestData.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => stub));
        return (host, stub, clock);
    }
}
