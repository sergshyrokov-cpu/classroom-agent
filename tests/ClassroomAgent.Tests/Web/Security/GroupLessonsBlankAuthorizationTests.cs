using System.Net;
using System.Text.Json;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-043 AC-009, FR-008 (TC-5): the export with the blank-form layout has the same protection as the program layout —
/// Admin and Dean are allowed, and every refusal under <c>/api/v1</c> is the API-6 JSON body with nothing audited.
/// </summary>
public sealed class GroupLessonsBlankAuthorizationTests(PostgreSqlFixture database)
{
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

    /// <summary>AC-009, FR-008: Admin and Dean may export the blank form.</summary>
    [Theory]
    [InlineData(Actor.Admin)]
    [InlineData(Actor.Dean)]
    public async Task AnAdminAndADean_ExportTheBlankForm(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(GroupLessonsBlank.Body(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        GroupLessonsBlankHostExtensions.AssertIsTheForm(workbook);
    }

    /// <summary>AC-009, FR-008: no session → 401 with the API-6 body, not a sign-in redirect; nothing audited.</summary>
    [Fact]
    public async Task Anonymous_Is401_WithTheApiBody()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Anonymous, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);
        await client.GetAsync(SignInTestData.SignInPath, ct);

        var response = await client.ExportAsync(GroupLessonsBlank.Body(seeded.CourseId), ct);

        AssertApiError(response, HttpStatusCode.Unauthorized, host.Text("Api.Error.SignInRequired", "uk"));
        Assert.Empty(await host.ExportAuditRowsAsync(ct));
    }

    /// <summary>AC-009, FR-008: a Dean on the forced password change → 403 with the API-6 body; nothing audited.</summary>
    [Fact]
    public async Task ADeanOnTheForcedPasswordChange_Is403_WithTheApiBody()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.DeanWithTemporaryPassword, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);
        await client.GetAsync(DeanAccountTestData.Paths.ForcedChange, ct);

        var response = await client.ExportAsync(GroupLessonsBlank.Body(seeded.CourseId), ct);

        AssertApiError(response, HttpStatusCode.Forbidden, host.Text("Api.Error.PasswordChangeRequired", "uk"));
        Assert.Empty(await host.ExportAuditRowsAsync(ct));
    }

    /// <summary>AC-009, FR-008, API-7: without the antiforgery header → 400 with the API-6 body; nothing audited.</summary>
    [Fact]
    public async Task WithoutTheToken_Is400_WithTheApiBody()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.ExportAsync(GroupLessonsBlank.Body(seeded.CourseId), ct, withToken: false);

        AssertApiError(response, HttpStatusCode.BadRequest, host.Text("Error.PageExpired", "uk"));
        Assert.Empty(await host.ExportAuditRowsAsync(ct));
    }
}
