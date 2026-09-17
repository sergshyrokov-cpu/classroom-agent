using System.Net;
using System.Text;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>
/// US-005 AC-003, AC-006, AC-013: the service-channel endpoint answers a known installation with exactly
/// status, compatibility, domain and client ID; refuses an unknown or malformed call without data; is a
/// token-less, session-less POST (spec FR-002, FR-004; api-design §3 … §5).
/// </summary>
public sealed class LegitimacyCheckEndpointTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task KnownActiveInstallation_Returns200_WithExactlyStatusCompatibilityDomainClientId()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);

        var response = await host.PostCheckAsync(identifier, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.StartsWith("application/json", ContentType(response), StringComparison.OrdinalIgnoreCase);
        var body = LegitimacyCheckHostExtensions.JsonProperties(response);
        Assert.Equal(new[] { "clientId", "compatibility", "domain", "status" }, body.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("active", body["status"]);
        Assert.Equal("supported", body["compatibility"]);
        Assert.Equal(InstallationTestData.Domain, body["domain"]);
        Assert.Equal(InstallationTestData.ClientId, body["clientId"]);
    }

    [Fact]
    public async Task SuspendedInstallation_IsAnsweredTheSameWay_WithStatusSuspended()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct, status: "suspended");

        var response = await host.PostCheckAsync(identifier, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        var body = LegitimacyCheckHostExtensions.JsonProperties(response);
        Assert.Equal("suspended", body["status"]);
        Assert.Equal("supported", body["compatibility"]);
        Assert.Equal(InstallationTestData.Domain, body["domain"]);
        Assert.Equal(InstallationTestData.ClientId, body["clientId"]);
    }

    [Fact]
    public async Task AnswersTheCallersOwnInstallation_NotAnother()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await host.InsertInstallationAsync(ct);
        var other = await host.InsertInstallationAsync(
            ct,
            name: InstallationTestData.OtherName,
            domain: InstallationTestData.OtherDomain,
            clientId: InstallationTestData.OtherClientId,
            status: "suspended");

        var response = await host.PostCheckAsync(other, ct);

        var body = LegitimacyCheckHostExtensions.JsonProperties(response);
        Assert.Equal(InstallationTestData.OtherDomain, body["domain"]);
        Assert.Equal(InstallationTestData.OtherClientId, body["clientId"]);
        Assert.Equal("suspended", body["status"]);
    }

    [Fact]
    public async Task UnknownInstallationId_Returns404_OutcomeOnly_RecordsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await host.InsertInstallationAsync(ct);

        var response = await host.PostCheckAsync(Guid.Parse(InstallationTestData.UnknownIdentifier), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.StartsWith("application/json", ContentType(response), StringComparison.OrdinalIgnoreCase);
        var body = LegitimacyCheckHostExtensions.JsonProperties(response);
        Assert.Equal(new Dictionary<string, string> { ["outcome"] = "unknown_installation" }, body);
        Assert.DoesNotContain(InstallationTestData.Domain, response.Body, StringComparison.Ordinal);
        Assert.Empty(await host.InstanceLicenseChecksAsync(ct));
    }

    public static TheoryData<string, string?, string> InvalidRequests => new()
    {
        { "missing body", null, "application/json" },
        { "empty body", string.Empty, "application/json" },
        { "not JSON content type", """{"installationId":"{id}","applicationVersion":"1.0.0","contractVersion":1}""", "text/plain" },
        { "form content type", "installationId={id}&applicationVersion=1.0.0&contractVersion=1", "application/x-www-form-urlencoded" },
        { "malformed JSON", """{"installationId":"{id}","applicationVersion":"1.0.0","contractVersion":1""", "application/json" },
        { "JSON array", """[{"installationId":"{id}","applicationVersion":"1.0.0","contractVersion":1}]""", "application/json" },
        { "missing installationId", """{"applicationVersion":"1.0.0","contractVersion":1}""", "application/json" },
        { "installationId not a UUID", """{"installationId":"abc","applicationVersion":"1.0.0","contractVersion":1}""", "application/json" },
        { "installationId a number", """{"installationId":12345,"applicationVersion":"1.0.0","contractVersion":1}""", "application/json" },
        { "missing applicationVersion", """{"installationId":"{id}","contractVersion":1}""", "application/json" },
        { "applicationVersion two parts", """{"installationId":"{id}","applicationVersion":"1.0","contractVersion":1}""", "application/json" },
        { "applicationVersion leading zero", """{"installationId":"{id}","applicationVersion":"01.0.0","contractVersion":1}""", "application/json" },
        { "applicationVersion pre-release", """{"installationId":"{id}","applicationVersion":"1.0.0-rc1","contractVersion":1}""", "application/json" },
        { "applicationVersion build metadata", """{"installationId":"{id}","applicationVersion":"1.0.0+abc","contractVersion":1}""", "application/json" },
        { "applicationVersion seven digits", """{"installationId":"{id}","applicationVersion":"1000000.0.0","contractVersion":1}""", "application/json" },
        { "applicationVersion empty", """{"installationId":"{id}","applicationVersion":"","contractVersion":1}""", "application/json" },
        { "applicationVersion a number", """{"installationId":"{id}","applicationVersion":100,"contractVersion":1}""", "application/json" },
        { "missing contractVersion", """{"installationId":"{id}","applicationVersion":"1.0.0"}""", "application/json" },
        { "contractVersion zero", """{"installationId":"{id}","applicationVersion":"1.0.0","contractVersion":0}""", "application/json" },
        { "contractVersion negative", """{"installationId":"{id}","applicationVersion":"1.0.0","contractVersion":-1}""", "application/json" },
        { "contractVersion too large", """{"installationId":"{id}","applicationVersion":"1.0.0","contractVersion":1000000}""", "application/json" },
        { "contractVersion a string", """{"installationId":"{id}","applicationVersion":"1.0.0","contractVersion":"1"}""", "application/json" },
        { "contractVersion fractional", """{"installationId":"{id}","applicationVersion":"1.0.0","contractVersion":1.5}""", "application/json" },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidRequest_Returns400_InvalidRequestOnly_RecordsNothing(string scenario, string? template, string contentType)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);

        var response = await host.PostCheckAsync(template?.Replace("{id}", identifier.ToString("D"), StringComparison.Ordinal), ct, contentType);

        Assert.True(response.Status == HttpStatusCode.BadRequest, $"{scenario}: expected 400, got {(int)response.Status}.");
        var body = LegitimacyCheckHostExtensions.JsonProperties(response);
        Assert.Equal(new Dictionary<string, string> { ["outcome"] = "invalid_request" }, body);
        Assert.DoesNotContain(InstallationTestData.Domain, response.Body, StringComparison.Ordinal);
        Assert.Empty(await host.InstanceLicenseChecksAsync(ct));
    }

    [Theory]
    [InlineData("0.0.0")]
    [InlineData("999999.999999.999999")]
    [InlineData("10.20.30")]
    public async Task ApplicationVersion_AtTheBounds_IsAccepted(string version)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);

        var response = await host.PostCheckAsync(identifier, ct, applicationVersion: version);

        Assert.Equal(HttpStatusCode.OK, response.Status);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(999999)]
    public async Task ContractVersion_AtTheBounds_IsAccepted(int contractVersion)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);

        var response = await host.PostCheckAsync(identifier, ct, contractVersion: contractVersion);

        Assert.Equal(HttpStatusCode.OK, response.Status);
    }

    [Fact]
    public async Task UnknownExtraProperties_AreIgnored()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);
        var body = $$"""{"installationId":"{{identifier:D}}","applicationVersion":"1.0.0","contractVersion":1,"futureField":{"x":1},"status":"suspended"}""";

        var response = await host.PostCheckAsync(body, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("active", LegitimacyCheckHostExtensions.JsonProperties(response)["status"]);
        Assert.Equal("active", (await host.InstallationAsync(identifier, ct))!.Status);
    }

    [Fact]
    public async Task InstallationIdInUpperCase_IsTheSameInstallation()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);
        var body = $$"""{"installationId":"{{identifier.ToString("D").ToUpperInvariant()}}","applicationVersion":"1.0.0","contractVersion":1}""";

        var response = await host.PostCheckAsync(body, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
    }

    [Fact]
    public async Task WithoutSessionAndWithoutAntiforgeryToken_IsProcessed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);
        using var anonymous = host.CreateClient();

        var response = await anonymous.SendAsync(
            HttpMethod.Post,
            LegitimacyCheckTestData.Path,
            new StringContent(LegitimacyCheckTestData.RequestJson(identifier), Encoding.UTF8, "application/json"),
            ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Null(response.Location);
        Assert.Empty(response.SetCookies);
        Assert.Single(await host.InstanceLicenseChecksAsync(ct));
    }

    [Fact]
    public async Task WithOwnerSessionCookie_IsProcessedTheSameWay_NoChallenge()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);

        var response = await owner.SendAsync(
            HttpMethod.Post,
            LegitimacyCheckTestData.Path,
            new StringContent(LegitimacyCheckTestData.RequestJson(identifier), Encoding.UTF8, "application/json"),
            ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(InstallationTestData.Domain, LegitimacyCheckHostExtensions.JsonProperties(response)["domain"]);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task OtherMethods_AreNotHandled_RecordNothing(string method)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);
        using var anonymous = host.CreateClient();

        var response = await anonymous.SendAsync(
            new HttpMethod(method),
            LegitimacyCheckTestData.Path,
            method == "GET" ? null : new StringContent(LegitimacyCheckTestData.RequestJson(identifier), Encoding.UTF8, "application/json"),
            ct);

        Assert.True(
            response.Status is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"{method}: expected 404 or 405, got {(int)response.Status}.");
        Assert.DoesNotContain(InstallationTestData.Domain, response.Body, StringComparison.Ordinal);
        Assert.Empty(await host.InstanceLicenseChecksAsync(ct));
    }

    [Fact]
    public async Task BeforeOwnerSetup_RedirectsToSetup_RecordsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        var identifier = await host.InsertInstallationAsync(ct);

        var response = await host.PostCheckAsync(identifier, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/setup", response.LocationPath);
        Assert.Empty(await host.InstanceLicenseChecksAsync(ct));
    }

    [Fact]
    public async Task Checks_WriteNoAuditRow_EvenForUnknownAndInvalidCalls()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);
        var auditBefore = await host.AuditRowsAsync(ct);

        await host.PostCheckAsync(identifier, ct);
        await host.PostCheckAsync(identifier, ct, applicationVersion: "0.0.1");
        await host.PostCheckAsync(Guid.Parse(InstallationTestData.UnknownIdentifier), ct);
        await host.PostCheckAsync("{", ct);

        Assert.Equal(auditBefore, await host.AuditRowsAsync(ct));
    }

    [Fact]
    public async Task Checks_DoNotChangeTheInstallation()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);
        var before = await host.InstallationAsync(identifier, ct);
        host.Time.Advance(TimeSpan.FromMinutes(5));

        await host.PostCheckAsync(identifier, ct);

        Assert.Equal(before, await host.InstallationAsync(identifier, ct));
    }

    private static string ContentType(PageResponse response) =>
        response.Headers.FirstOrDefault(h => string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;
}
