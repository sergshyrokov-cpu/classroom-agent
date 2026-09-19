using System.Net;
using System.Text.Json;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.ControlPlane.Push;

/// <summary>
/// US-006 AC-005 … AC-008: a status change that really changed the status hands one push to the sender,
/// which posts the installation id to the stored address, retries after 5 s, 30 s and 2 min, never retries a
/// `404`, replaces an unfinished push and keeps nothing across a restart (spec FR-005 … FR-008;
/// api-design §6). The network is <see cref="PushClientStub"/>; the retry schedule runs on the manual clock.
/// </summary>
public sealed class StatusPushDeliveryTests(PostgreSqlFixture database)
{
    private static readonly TimeSpan FirstPause = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SecondPause = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ThirdPause = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Suspending_PostsThePushToTheStoredAddress()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, stub, clock) = await StartAsync(database, ct);
        await using var _ = host;
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        await owner.SuspendInstallationAsync(installation, ct);

        await stub.WaitForAttemptsAsync(1, ct);
        var attempt = Assert.Single(stub.Attempts);
        Assert.Equal(HttpMethod.Post, attempt.Method);
        Assert.Equal(new Uri(PushTestData.Address + PushTestData.StatusPushPath), attempt.Uri);
        Assert.StartsWith("application/json", attempt.ContentType ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        using var body = JsonDocument.Parse(attempt.Body);
        Assert.Equal(new[] { "installationId" }, body.RootElement.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(installation, Guid.Parse(body.RootElement.GetProperty("installationId").GetString()!));
        Assert.False(attempt.Headers.ContainsKey("Cookie"));
        Assert.False(attempt.Headers.ContainsKey("Authorization"));
        Assert.Equal(clock.GetUtcNow(), attempt.At);
    }

    [Fact]
    public async Task Resuming_PostsThePush()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, stub, _) = await StartAsync(database, ct);
        await using var _host = host;
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);
        await host.SetInstallationStatusAsync(installation, "suspended", ct);

        await owner.ResumeInstallationAsync(installation, ct);

        await stub.WaitForAttemptsAsync(1, ct);
        Assert.Single(stub.Attempts);
    }

    [Fact]
    public async Task ActionThatChangesNothing_SendsNoPush()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, stub, _) = await StartAsync(database, ct);
        await using var _host = host;
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);
        await host.SetInstallationStatusAsync(installation, "suspended", ct);

        await owner.SuspendInstallationAsync(installation, ct);

        await stub.AssertNoAttemptAsync(ct);
    }

    [Fact]
    public async Task WithoutAPushAddress_NothingIsSent()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, stub, _) = await StartAsync(database, ct);
        await using var _host = host;
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct, pushAddress: null);

        var response = await owner.SuspendInstallationAsync(installation, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("suspended", (await host.InstallationAsync(installation, ct))!.Status);
        await stub.AssertNoAttemptAsync(ct);
    }

    [Fact]
    public async Task ChangesOtherThanTheStatus_SendNoPush()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, stub, _) = await StartAsync(database, ct);
        await using var _host = host;
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        await owner.PostFromPageAsync(
            $"/installations/{installation:D}/name",
            $"/installations/{installation:D}/name",
            InstallationTestData.NameFields(InstallationTestData.OtherName),
            ct);
        await owner.PostFromPageAsync(
            $"/installations/{installation:D}/client-id",
            $"/installations/{installation:D}/client-id",
            InstallationTestData.ClientIdFields(InstallationTestData.OtherClientId),
            ct);
        await owner.ChangePushAddressAsync(installation, PushTestData.OtherAddress, ct);

        await stub.AssertNoAttemptAsync(ct);
    }

    [Fact]
    public async Task AddingAndRevokingAnAllowedAdmin_SendNoPush()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, stub, _) = await StartAsync(database, ct);
        await using var _host = host;
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        var admin = await host.AddAllowedAdminAsync(owner, installation, AllowedAdminTestData.Email, ct);
        await owner.RevokeAllowedAdminAsync(installation, admin.Identifier, ct);

        await stub.AssertNoAttemptAsync(ct);
    }

    [Fact]
    public async Task TheOwnerIsNotKeptWaitingForThePush()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, stub, _) = await StartAsync(database, ct);
        await using var _host = host;
        stub.NeverAnswer();
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        var response = await owner.SuspendInstallationAsync(installation, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(PushTestData.DetailPath(installation), response.LocationPath);
    }

    [Fact]
    public async Task Accepted_IsDelivered_WithoutARetry()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, stub, clock) = await StartAsync(database, ct);
        await using var _host = host;
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        await owner.SuspendInstallationAsync(installation, ct);
        await stub.WaitForAttemptsAsync(1, ct);
        clock.Advance(ThirdPause + ThirdPause);

        await stub.AssertNoAttemptAsync(ct, expected: 1);
    }

    [Fact]
    public async Task NotFound_IsNotRetried()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, stub, clock) = await StartAsync(database, ct);
        await using var _host = host;
        stub.AlwaysAnswer(HttpStatusCode.NotFound);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        await owner.SuspendInstallationAsync(installation, ct);
        await stub.WaitForAttemptsAsync(1, ct);
        clock.Advance(ThirdPause + ThirdPause);

        await stub.AssertNoAttemptAsync(ct, expected: 1);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Found)]
    public async Task UnexpectedStatus_IsRetriedThreeTimes_ThenAbandoned(HttpStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, stub, clock) = await StartAsync(database, ct);
        await using var _host = host;
        stub.AlwaysAnswer(status);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);
        var start = clock.GetUtcNow();

        await owner.SuspendInstallationAsync(installation, ct);
        await stub.WaitForAttemptsAsync(1, ct);
        clock.Advance(FirstPause);
        await stub.WaitForAttemptsAsync(2, ct);
        clock.Advance(SecondPause);
        await stub.WaitForAttemptsAsync(3, ct);
        clock.Advance(ThirdPause);
        await stub.WaitForAttemptsAsync(4, ct);
        clock.Advance(ThirdPause + ThirdPause);

        await stub.AssertNoAttemptAsync(ct, expected: 4);
        Assert.Equal(
            new[] { start, start + FirstPause, start + FirstPause + SecondPause, start + FirstPause + SecondPause + ThirdPause },
            stub.Attempts.Select(a => a.At).ToArray());
    }

    [Fact]
    public async Task ConnectionFailure_IsRetried()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, stub, clock) = await StartAsync(database, ct);
        await using var _host = host;
        stub.FailToConnect().Answer(HttpStatusCode.Accepted);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        await owner.SuspendInstallationAsync(installation, ct);
        await stub.WaitForAttemptsAsync(1, ct);
        clock.Advance(FirstPause);
        await stub.WaitForAttemptsAsync(2, ct);
        clock.Advance(ThirdPause);

        await stub.AssertNoAttemptAsync(ct, expected: 2);
    }

    [Fact]
    public async Task NoAnswerWithinTenSeconds_IsATimeout_AndIsRetried()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, stub, clock) = await StartAsync(database, ct);
        await using var _host = host;
        stub.NeverAnswer().Answer(HttpStatusCode.Accepted);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        await owner.SuspendInstallationAsync(installation, ct);
        await stub.WaitForAttemptsAsync(1, ct);
        clock.Advance(AttemptTimeout);
        clock.Advance(FirstPause);

        await stub.WaitForAttemptsAsync(2, ct);
    }

    [Fact]
    public async Task NewerPush_ReplacesTheUnfinishedOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, stub, clock) = await StartAsync(database, ct);
        await using var _host = host;
        stub.AlwaysAnswer(HttpStatusCode.InternalServerError);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        await owner.SuspendInstallationAsync(installation, ct);
        await stub.WaitForAttemptsAsync(1, ct);
        await owner.ResumeInstallationAsync(installation, ct);
        await stub.WaitForAttemptsAsync(2, ct);
        clock.Advance(FirstPause);
        await stub.WaitForAttemptsAsync(3, ct);
        clock.Advance(SecondPause - FirstPause);

        // The replacement restarted the schedule: after 5 s there is a third attempt, and the
        // cancelled push contributes none of its own.
        await stub.AssertNoAttemptAsync(ct, expected: 3);
    }

    [Fact]
    public async Task PushesToDifferentSchools_AreIndependent()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, stub, _) = await StartAsync(database, ct);
        await using var _host = host;
        using var owner = await host.CreateOwnerAsync(ct);
        var first = await host.RegisterInstallationWithPushAddressAsync(owner, ct);
        var second = await host.RegisterInstallationWithPushAddressAsync(
            owner,
            ct,
            pushAddress: PushTestData.OtherAddress,
            name: InstallationTestData.OtherName,
            domain: InstallationTestData.OtherDomain,
            clientId: InstallationTestData.OtherClientId);

        await owner.SuspendInstallationAsync(first, ct);
        await owner.SuspendInstallationAsync(second, ct);

        await stub.WaitForAttemptsAsync(2, ct);
        Assert.Equal(
            new[] { PushTestData.Address + PushTestData.StatusPushPath, PushTestData.OtherAddress + PushTestData.StatusPushPath },
            stub.Attempts.Select(a => a.Uri.ToString()).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task PendingRetries_DoNotOutliveTheHost()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, stub, _) = await StartAsync(database, ct);
        await using var _host = host;
        stub.AlwaysAnswer(HttpStatusCode.InternalServerError);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);
        await owner.SuspendInstallationAsync(installation, ct);
        await stub.WaitForAttemptsAsync(1, ct);

        await host.StopAsync();

        await stub.AssertNoAttemptAsync(ct, expected: 1);
    }

    [Fact]
    public async Task DeliveryIsNotAudited_AndChangesNothingStored()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, stub, _) = await StartAsync(database, ct);
        await using var _host = host;
        stub.AlwaysAnswer(HttpStatusCode.InternalServerError);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        await owner.SuspendInstallationAsync(installation, ct);
        await stub.WaitForAttemptsAsync(1, ct);

        var rows = await host.AuditRowsAsync(ct);
        Assert.Single(rows, row => row.Action == "installation_suspended");
        Assert.DoesNotContain(rows, row => row.Action.Contains("push", StringComparison.Ordinal));
        Assert.Equal(PushTestData.Address, await host.PushAddressAsync(installation, ct));
    }

    [Fact]
    public async Task TheAddressIsNeverLogged()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, stub, _) = await StartAsync(database, ct);
        await using var _host = host;
        stub.AlwaysAnswer(HttpStatusCode.InternalServerError);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);
        await owner.SuspendInstallationAsync(installation, ct);
        await stub.WaitForAttemptsAsync(1, ct);

        var logs = await host.ReadLogFilesAsync(ct);

        Assert.DoesNotContain(logs, log => log.Contains("10.0.0.5", StringComparison.Ordinal));
        Assert.DoesNotContain(logs, log => log.Contains(InstallationTestData.Domain, StringComparison.Ordinal));
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
