using System.Net;
using System.Text.Json;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-028 AC-008 and the host-wide <c>/api/v1</c> error rules (spec FR-008, S-01 … S-03; openapi
/// <c>x-host-wide-rules.api-v1-error-body</c>; API-5 … API-7; TC-3, TC-5): the export is protected and POST-only, and
/// every refusal under <c>/api/v1</c> is the API-6 JSON body — never a redirect, never the HTML error page.
/// </summary>
public sealed class JournalExportAuthorizationTests(PostgreSqlFixture database)
{
    private static readonly string Pattern = JournalExportHostExtensions.ExportPath.Trim('/');

    /// <summary>API-6: content type, status, error, message, path — and no internals.</summary>
    private static JsonElement AssertApiError(BinaryResponse response, HttpStatusCode status, string message)
    {
        Assert.Equal(status, response.Status);
        Assert.Null(response.Location);
        Assert.StartsWith("application/json", response.Header("Content-Type"), StringComparison.OrdinalIgnoreCase);
        var json = response.Json();
        Assert.Equal((int)status, json.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(json.GetProperty("error").GetString()));
        Assert.True(json.TryGetProperty("timestamp", out _));
        Assert.Equal(message, json.GetProperty("message").GetString());
        Assert.DoesNotContain("Exception", response.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("ClassroomAgent.", response.Text, StringComparison.Ordinal);
        return json;
    }

    /// <summary>S-01, TC-5: the export is routed, POST only, and not anonymous.</summary>
    [Fact]
    public async Task TheExport_IsAProtectedPostOnlyEndpoint()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var endpoints = HostEndpoint.All(host.Services).Where(e => e.Pattern == Pattern).ToList();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, e => Assert.False(e.AllowsAnonymous, e.ToString()));
        Assert.All(endpoints, e => Assert.False(e.IsExemptFromAntiforgery, e.ToString()));
        Assert.Equal(["POST"], endpoints.SelectMany(e => e.Methods ?? ["*"]).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>AC-008, rule (1): no session → 401 with the API-6 body, not a sign-in redirect; nothing audited.</summary>
    [Fact]
    public async Task Anonymous_Is401_WithTheApiBody()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Anonymous, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);
        await client.GetAsync(SignInTestData.SignInPath, ct);

        var response = await client.ExportAsync(JournalExportHostExtensions.September(seeded.CourseId), ct);

        AssertApiError(response, HttpStatusCode.Unauthorized, host.Text("Api.Error.SignInRequired", "uk"));
        Assert.Empty(await host.ExportAuditRowsAsync(ct));
    }

    /// <summary>AC-008, rule (2), US-012: a Dean on the forced password change → 403 with the API-6 body, not a redirect.</summary>
    [Fact]
    public async Task ADeanOnTheForcedPasswordChange_Is403_WithTheApiBody()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.DeanWithTemporaryPassword, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);
        await client.GetAsync(DeanAccountTestData.Paths.ForcedChange, ct);

        var response = await client.ExportAsync(JournalExportHostExtensions.September(seeded.CourseId), ct);

        AssertApiError(response, HttpStatusCode.Forbidden, host.Text("Api.Error.PasswordChangeRequired", "uk"));
        Assert.Empty(await host.ExportAuditRowsAsync(ct));
    }

    /// <summary>S-03, rule (4), API-7: without the antiforgery header → 400 with the API-6 body; nothing exported or audited.</summary>
    [Theory]
    [InlineData(Actor.Admin)]
    [InlineData(Actor.Dean)]
    public async Task WithoutTheToken_Is400_WithTheApiBody(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(JournalExportHostExtensions.September(seeded.CourseId), ct, withToken: false);

        AssertApiError(response, HttpStatusCode.BadRequest, host.Text("Error.PageExpired", "uk"));
        Assert.Empty(await host.ExportAuditRowsAsync(ct));
    }

    /// <summary>S-03, rule (5): a GET of the export is 405 with the API-6 body and exports nothing.</summary>
    [Fact]
    public async Task AGet_Is405_WithTheApiBody()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedJournalAsync(ct);

        var response = await client.SendForBytesAsync(HttpMethod.Get, JournalExportHostExtensions.ExportPath, null, ct);

        AssertApiError(response, HttpStatusCode.MethodNotAllowed, host.Text("Error.NotFound", "uk"));
        Assert.Empty(await host.ExportAuditRowsAsync(ct));
    }

    /// <summary>Rule (5): an unknown <c>/api/v1</c> path is 404 with the API-6 body, not the HTML error page.</summary>
    [Fact]
    public async Task AnUnknownApiPath_Is404_WithTheApiBody()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var response = await client.SendForBytesAsync(HttpMethod.Get, "/api/v1/exports/journal-pdf", null, ct);

        AssertApiError(response, HttpStatusCode.NotFound, host.Text("Error.NotFound", "uk"));
    }

    /// <summary>API-6, NFR-073: the message is in the signed-in user's language.</summary>
    [Fact]
    public async Task TheApiMessage_IsInTheUsersLanguage()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await InstallationTestHost.CreateAsync(database, ct);
        await using var _host = host;
        await ReadOnlyModeHost.SeedAsync(host, ReadOnlyModeHost.Cause.NotReadOnly, ct);
        await host.InsertAppUserAsync(ct, uiLanguage: "en");
        host.ControlPlaneHandler = WorkspaceConnectionHostExtensions.ApprovingChannel();
        host.Start();
        var (client, _) = await host.SignInWithGoogleAsync(ct);

        var response = await client.ExportAsync(JournalExportHostExtensions.September(987654), ct);

        AssertApiError(response, HttpStatusCode.NotFound, host.Text("Journal.Validation.CourseUnknown", "en"));
    }

    /// <summary>Outside <c>/api/v1</c> nothing changes: an anonymous page request still goes to sign-in.</summary>
    [Fact]
    public async Task OutsideTheApi_AnonymousStillGoesToSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Anonymous, ct);
        await using var _host = host;

        var page = await client.GetAsync(ReportTemplateTestData.ReportPath, ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(SignInTestData.SignInPath, page.LocationPath);
    }
}
