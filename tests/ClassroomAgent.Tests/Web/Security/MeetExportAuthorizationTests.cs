using System.Net;
using System.Text.Json;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-044 AC-013 and the host-wide <c>/api/v1</c> error rules (spec FR-011, S-01 … S-03; API-5 … API-7; TC-3, TC-5):
/// each Meet export is protected and POST-only, and every refusal under <c>/api/v1</c> is the API-6 JSON body — never a
/// redirect, never the HTML error page. Every test runs for both endpoints.
/// </summary>
public sealed class MeetExportAuthorizationTests(PostgreSqlFixture database)
{
    private const string Reports = MeetExportHostExtensions.ReportsPath;

    private const string Meeting = MeetExportHostExtensions.MeetingPath;

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

    /// <summary>Posts the valid body of the endpoint (or the given raw body) through the matching client helper.</summary>
    private static Task<BinaryResponse> PostAsync(
        FormClient client,
        string path,
        MeetReportsHostExtensions.SeededWorld world,
        CancellationToken ct,
        object? body = null,
        bool withToken = true,
        string contentType = "application/json")
    {
        if (path == Reports)
        {
            return client.ExportReportsAsync(
                body ?? MeetExportHostExtensions.October(world.CourseId), ct, withToken, contentType);
        }

        return client.ExportMeetingAsync(
            body ?? MeetExportHostExtensions.Meeting(world.CourseId, world.MeetingA), ct, withToken, contentType);
    }

    /// <summary>S-01, TC-5: each export is routed, POST only, and not anonymous.</summary>
    [Theory]
    [InlineData(Reports)]
    [InlineData(Meeting)]
    public async Task TheExport_IsAProtectedPostOnlyEndpoint(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var pattern = path.Trim('/');

        var endpoints = HostEndpoint.All(host.Services).Where(e => e.Pattern == pattern).ToList();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, e => Assert.False(e.AllowsAnonymous, e.ToString()));
        Assert.All(endpoints, e => Assert.False(e.IsExemptFromAntiforgery, e.ToString()));
        Assert.Equal(["POST"], endpoints.SelectMany(e => e.Methods ?? ["*"]).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>AC-013: both allowed roles export.</summary>
    [Theory]
    [InlineData(Reports, Actor.Admin)]
    [InlineData(Reports, Actor.Dean)]
    [InlineData(Meeting, Actor.Admin)]
    [InlineData(Meeting, Actor.Dean)]
    public async Task AnAllowedRole_Exports_With200(string path, Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var response = await PostAsync(client, path, world, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Single(await host.MeetExportAuditRowsAsync(ct));
    }

    /// <summary>AC-013, rule (1): no session → 401 with the API-6 body, not a sign-in redirect; nothing audited.</summary>
    [Theory]
    [InlineData(Reports)]
    [InlineData(Meeting)]
    public async Task Anonymous_Is401_WithTheApiBody(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Anonymous, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        await client.GetAsync(SignInTestData.SignInPath, ct);

        var response = await PostAsync(client, path, world, ct);

        AssertApiError(response, HttpStatusCode.Unauthorized, host.Text("Api.Error.SignInRequired", "uk"));
        Assert.Empty(await host.MeetExportAuditRowsAsync(ct));
    }

    /// <summary>AC-013, rule (2), US-012: a Dean on the forced password change → 403 with the API-6 body.</summary>
    [Theory]
    [InlineData(Reports)]
    [InlineData(Meeting)]
    public async Task ADeanOnTheForcedPasswordChange_Is403_WithTheApiBody(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.DeanWithTemporaryPassword, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        await client.GetAsync(DeanAccountTestData.Paths.ForcedChange, ct);

        var response = await PostAsync(client, path, world, ct);

        AssertApiError(response, HttpStatusCode.Forbidden, host.Text("Api.Error.PasswordChangeRequired", "uk"));
        Assert.Empty(await host.MeetExportAuditRowsAsync(ct));
    }

    /// <summary>S-03, rule (4), API-7: without the antiforgery header → 400 with the API-6 body; nothing audited.</summary>
    [Theory]
    [InlineData(Reports, Actor.Admin)]
    [InlineData(Reports, Actor.Dean)]
    [InlineData(Meeting, Actor.Admin)]
    [InlineData(Meeting, Actor.Dean)]
    public async Task WithoutTheToken_Is400_WithTheApiBody(string path, Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var response = await PostAsync(client, path, world, ct, withToken: false);

        AssertApiError(response, HttpStatusCode.BadRequest, host.Text("Error.PageExpired", "uk"));
        Assert.Empty(await host.MeetExportAuditRowsAsync(ct));
    }

    /// <summary>S-03, rule (5): a GET of an export is 405 with the API-6 body and exports nothing.</summary>
    [Theory]
    [InlineData(Reports)]
    [InlineData(Meeting)]
    public async Task AGet_Is405_WithTheApiBody(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedWorldAsync(ct);

        var response = await client.SendForBytesAsync(HttpMethod.Get, path, null, ct);

        AssertApiError(response, HttpStatusCode.MethodNotAllowed, host.Text("Error.NotFound", "uk"));
        Assert.Empty(await host.MeetExportAuditRowsAsync(ct));
    }

    /// <summary>API-2, API-5: a body that is not JSON is 415 with the API-6 body.</summary>
    [Theory]
    [InlineData(Reports)]
    [InlineData(Meeting)]
    public async Task AFormBody_Is415_WithTheApiBody(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var response = await PostAsync(
            client, path, world, ct, body: "courseId=" + world.CourseId, contentType: "application/x-www-form-urlencoded");

        AssertApiError(response, HttpStatusCode.UnsupportedMediaType, host.Text("Export.Validation.RequestMalformed", "uk"));
        Assert.Empty(await host.MeetExportAuditRowsAsync(ct));
    }

    /// <summary>API-5: a body that is no JSON object of strings is 400 with the API-6 body and no field errors.</summary>
    [Theory]
    [InlineData(Reports, "[]")]
    [InlineData(Reports, "not json")]
    [InlineData(Reports, "{\"courseId\":5}")]
    [InlineData(Reports, "{\"courseId\":\"1\",\"courseId\":\"2\"}")]
    [InlineData(Meeting, "[]")]
    [InlineData(Meeting, "not json")]
    [InlineData(Meeting, "{\"courseId\":5}")]
    [InlineData(Meeting, "{\"courseId\":\"1\",\"courseId\":\"2\"}")]
    public async Task AMalformedBody_Is400_WithoutFieldErrors(string path, string body)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var response = await PostAsync(client, path, world, ct, body: body);

        var json = AssertApiError(response, HttpStatusCode.BadRequest, host.Text("Export.Validation.RequestMalformed", "uk"));
        Assert.False(
            json.TryGetProperty("fieldErrors", out var errors)
            && errors.ValueKind == JsonValueKind.Array
            && errors.GetArrayLength() > 0);
        Assert.Empty(await host.MeetExportAuditRowsAsync(ct));
    }
}
