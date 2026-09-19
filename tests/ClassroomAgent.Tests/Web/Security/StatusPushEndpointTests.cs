using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-006 AC-009, AC-010, AC-012, AC-013, AC-014: the push receiver answers only on the private port,
/// accepts a push for this installation with `202` and starts a legitimacy check, refuses another
/// installation's id with `404`, rejects a malformed or oversized body, and behaves the same in read-only
/// mode (spec FR-009, FR-012; api-design §4; SC-4, DC-6, TC-5).
/// </summary>
public sealed class StatusPushEndpointTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task PushForThisInstallation_IsAccepted_AndStartsACheck()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.ControlPlane.ReplySuccess().ReplySuccess();
        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, ct);
        host.Time.Advance(TimeSpan.FromMinutes(2));

        var response = await host.SendPrivateAsync("POST", PushTestData.StatusPushPath, ct, body: PushTestData.Body(host.InstallationId));

        Assert.Equal(HttpStatusCode.Accepted, response.Status);
        await host.ControlPlane.WaitForCallsAsync(2, ct);
    }

    [Fact]
    public async Task PushForAnotherInstallation_IsNotFound_AndStartsNoCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.ControlPlane.ReplySuccess();
        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, ct);
        host.Time.Advance(TimeSpan.FromMinutes(2));

        var response = await host.SendPrivateAsync("POST", PushTestData.StatusPushPath, ct, body: PushTestData.Body(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
        Assert.Single(host.ControlPlane.Calls);
    }

    public static TheoryData<string?, string?> InvalidBodies => new()
    {
        { null, null },
        { string.Empty, "application/json" },
        { "not json", "application/json" },
        { """{"installationId":"not-a-uuid"}""", "application/json" },
        { """{"installationId":null}""", "application/json" },
        { "{}", "application/json" },
        { """{"installationId":"3f1c2a8e-5b7d-4c9e-a1f0-2d6b8e4c7a15"}""", "text/plain" },
    };

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task MalformedPush_IsRefused_AndStartsNoCheck(string? body, string? contentType)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.ControlPlane.ReplySuccess();
        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, ct);
        host.Time.Advance(TimeSpan.FromMinutes(2));

        var response = await host.SendPrivateAsync("POST", PushTestData.StatusPushPath, ct, body: body, contentType: contentType);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
        Assert.Single(host.ControlPlane.Calls);
    }

    [Fact]
    public async Task OversizedPush_IsRefused_AndStartsNoCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.ControlPlane.ReplySuccess();
        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, ct);
        host.Time.Advance(TimeSpan.FromMinutes(2));
        var padding = new string('x', 5000);
        var body = $$"""{"installationId":"{{host.InstallationId:D}}","padding":"{{padding}}"}""";

        var response = await host.SendPrivateAsync("POST", PushTestData.StatusPushPath, ct, body: body);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.Status);
        await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
        Assert.Single(host.ControlPlane.Calls);
    }

    [Fact]
    public async Task UnknownPropertiesInThePush_AreIgnored()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.ControlPlane.ReplySuccess().ReplySuccess();
        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, ct);
        host.Time.Advance(TimeSpan.FromMinutes(2));
        var body = $$$"""{"installationId":"{{{host.InstallationId:D}}}","status":"suspended","future":{"x":1}}""";

        var response = await host.SendPrivateAsync("POST", PushTestData.StatusPushPath, ct, body: body);

        Assert.Equal(HttpStatusCode.Accepted, response.Status);
        await host.ControlPlane.WaitForCallsAsync(2, ct);
    }

    [Fact]
    public async Task OnThePublicPort_ThePathIsNotFound_AndStartsNoCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.ControlPlane.ReplySuccess();
        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, ct);
        host.Time.Advance(TimeSpan.FromMinutes(2));

        var response = await host.SendPublicAsync("POST", PushTestData.StatusPushPath, ct, body: PushTestData.Body(host.InstallationId));

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
        Assert.Single(host.ControlPlane.Calls);
    }

    [Fact]
    public async Task OnThePublicPort_AForgedHostDoesNotReachTheReceiver()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.ControlPlane.ReplySuccess();
        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, ct);
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Host"] = $"localhost:{InstallationConfigurationKeys.PrivatePortValue}",
            ["X-Forwarded-Host"] = $"localhost:{InstallationConfigurationKeys.PrivatePortValue}",
        };

        var response = await host.SendPublicAsync(
            "POST",
            PushTestData.StatusPushPath,
            ct,
            headers,
            PushTestData.Body(host.InstallationId));

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task OtherMethods_DoNotReachTheReceiver(string method)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.ControlPlane.ReplySuccess();
        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, ct);
        host.Time.Advance(TimeSpan.FromMinutes(2));

        var response = await host.SendPrivateAsync(method, PushTestData.StatusPushPath, ct, body: PushTestData.Body(host.InstallationId));

        Assert.NotEqual(HttpStatusCode.Accepted, response.Status);
        await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
        Assert.Single(host.ControlPlane.Calls);
    }

    [Fact]
    public async Task InReadOnlyMode_ThePushIsAccepted_AndTheCheckWritesTheState()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await host.InsertLegitimacyStateAsync(ct, lastSuccessfulCheckAt: InstallationTestHost.DefaultStart.AddDays(-9), status: "suspended");
        host.ControlPlane.ReplyFailure(ClassroomAgent.Application.Models.CheckFailureCategory.Unreachable);
        host.ControlPlane.Reply(FakeControlPlaneClient.Answer());
        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, ct);
        host.Time.Advance(TimeSpan.FromMinutes(2));

        var response = await host.SendPrivateAsync("POST", PushTestData.StatusPushPath, ct, body: PushTestData.Body(host.InstallationId));

        Assert.Equal(HttpStatusCode.Accepted, response.Status);
        await host.ControlPlane.WaitForCallsAsync(2, ct);
        await host.Time.WaitUntilAsync(
            () => host.LegitimacyStatesAsync(ct).GetAwaiter().GetResult().Single().Status == "active",
            "the pushed check to record the active status",
            ct);
    }

    [Fact]
    public async Task ThePushItselfWritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.ControlPlane.ReplySuccess();
        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, ct);
        await host.Time.WaitUntilAsync(
            () => host.LegitimacyStatesAsync(ct).GetAwaiter().GetResult().Count == 1,
            "the first check to store the state",
            ct);
        var before = (await host.LegitimacyStatesAsync(ct)).Single();
        host.Time.Advance(TimeSpan.FromMinutes(2));

        // No scripted reply is left, so the pushed check never completes: nothing may change meanwhile.
        await host.SendPrivateAsync("POST", PushTestData.StatusPushPath, ct, body: PushTestData.Body(host.InstallationId));
        await host.ControlPlane.WaitForCallsAsync(2, ct);

        Assert.Equal(before, (await host.LegitimacyStatesAsync(ct)).Single());
    }

    [Fact]
    public async Task TheReceiverAnswersWithoutWaitingForTheCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.ControlPlane.ReplySuccess();
        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, ct);
        host.Time.Advance(TimeSpan.FromMinutes(2));

        // The pushed check has no scripted reply and therefore never finishes.
        var response = await host.SendPrivateAsync("POST", PushTestData.StatusPushPath, ct, body: PushTestData.Body(host.InstallationId));

        Assert.Equal(HttpStatusCode.Accepted, response.Status);
        Assert.Equal(string.Empty, response.Body);
    }
}
