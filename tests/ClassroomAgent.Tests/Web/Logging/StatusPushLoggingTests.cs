using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-006 AC-009 … AC-012: the installation logs an accepted push at <c>Information</c>, a push for
/// another installation at <c>Warning</c>, a rejected body at <c>Error</c> with its category only, logs
/// nothing for a deferred push and logs the pending check when it starts (spec FR-011; api-design §9;
/// SC-10, DC-10). Event names are fixed by the test strategy §3.
/// </summary>
public sealed class StatusPushLoggingTests(PostgreSqlFixture database)
{
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    [Fact]
    public async Task AcceptedPush_IsLoggedAtInformation()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(database, ct, replies: 2);
        host.Time.Advance(Minute);

        await PushAsync(host, ct, host.InstallationId);
        await host.ControlPlane.WaitForCallsAsync(2, ct);

        var events = await host.ReadLogEventsAsync(ct);
        var accepted = Assert.Single(events, e => e.EventName == "StatusPushAccepted");
        Assert.Equal("Information", accepted.Level);
    }

    [Fact]
    public async Task PushForAnotherInstallation_IsLoggedAtWarning_WithoutTheReceivedId()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(database, ct, replies: 1);
        host.Time.Advance(Minute);
        var foreign = Guid.NewGuid();

        await PushAsync(host, ct, foreign);

        var events = await host.ReadLogEventsAsync(ct);
        var refused = Assert.Single(events, e => e.EventName == "StatusPushForeignInstallation");
        Assert.Equal("Warning", refused.Level);
        Assert.DoesNotContain(events, e => e.Line.Contains(foreign.ToString("D"), StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(events, e => e.EventName == "StatusPushAccepted");
    }

    [Theory]
    [InlineData("not json", "invalid")]
    [InlineData("""{"installationId":"not-a-uuid"}""", "invalid")]
    public async Task MalformedPush_IsLoggedAtError_WithItsCategoryOnly(string body, string category)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(database, ct, replies: 1);
        host.Time.Advance(Minute);

        await host.SendPrivateAsync("POST", PushTestData.StatusPushPath, ct, body: body);

        var events = await host.ReadLogEventsAsync(ct);
        var rejected = Assert.Single(events, e => e.EventName == "StatusPushRejected");
        Assert.Equal("Error", rejected.Level);
        Assert.Equal(category, rejected.Property("Category"));
        Assert.DoesNotContain(events, e => e.Line.Contains("not-a-uuid", StringComparison.Ordinal));
        Assert.DoesNotContain(events, e => e.Line.Contains("not json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OversizedPush_IsLoggedAtError_WithTheTooLargeCategory()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(database, ct, replies: 1);
        host.Time.Advance(Minute);
        var padding = new string('x', 5000);
        var body = $$$"""{"installationId":"{{{host.InstallationId:D}}}","padding":"{{{padding}}}"}""";

        await host.SendPrivateAsync("POST", PushTestData.StatusPushPath, ct, body: body);

        var events = await host.ReadLogEventsAsync(ct);
        var rejected = Assert.Single(events, e => e.EventName == "StatusPushRejected");
        Assert.Equal("Error", rejected.Level);
        Assert.Equal("too_large", rejected.Property("Category"));
        Assert.DoesNotContain(events, e => e.Line.Contains(padding, StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeferredPush_IsNotLogged_AndThePendingCheckIsLoggedWhenItStarts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(database, ct, replies: 3);

        // The first push starts a check and the minute with it (`trebovaniya.md` v77 §9; spec I-11).
        host.Time.Advance(Minute);
        await PushAsync(host, ct, host.InstallationId);
        await host.ControlPlane.WaitForCallsAsync(2, ct);

        // Inside that minute: both pushes are accepted, neither starts a check and neither is logged.
        host.Time.Advance(TimeSpan.FromSeconds(10));
        await PushAsync(host, ct, host.InstallationId);
        await PushAsync(host, ct, host.InstallationId);
        host.Time.Advance(TimeSpan.FromSeconds(50));
        await host.ControlPlane.WaitForCallsAsync(3, ct);

        var events = await host.ReadLogEventsAsync(ct);
        var accepted = Assert.Single(events, e => e.EventName == "StatusPushAccepted");
        Assert.Equal("Information", accepted.Level);
        var pending = Assert.Single(events, e => e.EventName == "PendingPushCheckStarted");
        Assert.Equal("Information", pending.Level);
    }

    private static async Task<InstallationTestHost> StartedAsync(
        PostgreSqlFixture database,
        CancellationToken cancellationToken,
        int replies)
    {
        var host = await InstallationTestHost.CreateAsync(database, cancellationToken);
        for (var i = 0; i < replies; i++)
        {
            host.ControlPlane.ReplySuccess();
        }

        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, cancellationToken);
        return host;
    }

    private static Task<InstallationTestHost.RawResponse> PushAsync(
        InstallationTestHost host,
        CancellationToken cancellationToken,
        Guid installationId) =>
        host.SendPrivateAsync("POST", PushTestData.StatusPushPath, cancellationToken, body: PushTestData.Body(installationId));
}
