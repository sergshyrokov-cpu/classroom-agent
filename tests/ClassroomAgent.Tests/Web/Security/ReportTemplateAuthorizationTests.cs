using System.Globalization;
using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-027 AC-002, AC-008: every template and report endpoint is open to an Admin and a Dean, sends an anonymous visitor
/// to sign-in and a Dean with a temporary password to the forced change; the three POSTs refuse a missing antiforgery
/// token and store nothing (api-design §4; SC-4; TC-5).
/// </summary>
public sealed class ReportTemplateAuthorizationTests(PostgreSqlFixture database)
{
    private const string ChangePasswordPath = "/sign-in/change-password";

    public static TheoryData<string> Gets => new(
        "list", "new", "copy", "edit", "deletion-page", "report");

    public static TheoryData<string> Posts => new("create", "change", "delete");

    public static TheoryData<string> Endpoints => new(
        "list", "new", "copy", "edit", "deletion-page", "report", "create", "change", "delete");

    public static TheoryData<Actor, string> AllowedCases
    {
        get
        {
            var data = new TheoryData<Actor, string>();
            foreach (var actor in new[] { Actor.Admin, Actor.Dean })
            {
                foreach (var endpoint in new[] { "list", "new", "copy", "edit", "deletion-page", "report", "create", "change", "delete" })
                {
                    data.Add(actor, endpoint);
                }
            }

            return data;
        }
    }

    private static bool IsPost(string endpoint) => endpoint is "create" or "change" or "delete";

    private static string PathOf(string endpoint, long id)
    {
        var reference = id.ToString(CultureInfo.InvariantCulture);
        return endpoint switch
        {
            "list" => ReportTemplateTestData.ListPath,
            "new" => ReportTemplateTestData.NewPath,
            "copy" => ReportTemplateTestData.CopyPath(ReportTemplateTestData.BuiltInKey),
            "edit" => ReportTemplateTestData.EditPath(reference),
            "deletion-page" or "delete" => ReportTemplateTestData.DeletionPath(reference),
            "report" => ReportTemplateTestData.ReportPath,
            "create" => ReportTemplateTestData.ListPath,
            "change" => ReportTemplateTestData.ChangePath(reference),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, null),
        };
    }

    private static IEnumerable<KeyValuePair<string, string>> BodyOf(string endpoint) =>
        endpoint switch
        {
            "create" => ReportTemplateFormBuilder.Valid("Test Template Auth New").Http(),
            "change" => ReportTemplateFormBuilder.Valid("Test Template Auth Changed").Http(),
            _ => [],
        };

    private static async Task<long> SeedTemplateAsync(InstallationTestHost host, CancellationToken ct)
    {
        const string author = "author.auth@school-one.example.test";
        await host.InsertAppUserAsync(ct, email: author);
        var authorId = await host.AccountIdAsync(author, ct);
        return await host.InsertTemplateAsync(ct, "Test Template Auth Seed", authorId);
    }

    private static Task<PageResponse> SendAsync(FormClient client, string endpoint, long id, CancellationToken ct) =>
        IsPost(endpoint)
            ? client.PostFormAsync(PathOf(endpoint, id), BodyOf(endpoint), ct)
            : client.GetAsync(PathOf(endpoint, id), ct);

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Anonymous_IsSentToSignIn_OnEveryEndpoint(string endpoint)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Anonymous, ct);
        await using var _host = host;
        var id = await SeedTemplateAsync(host, ct);
        var before = await host.TemplateFingerprintAsync(ct);
        // The sign-in page supplies a token, so the refusal under test is the authorization one.
        await client.GetAsync(SignInTestData.SignInPath, ct);

        var response = await SendAsync(client, endpoint, id, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(SignInTestData.SignInPath, response.LocationPath);
        Assert.Equal(before, await host.TemplateFingerprintAsync(ct));
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task ARestrictedDean_IsSentToTheForcedChange_OnEveryEndpoint(string endpoint)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.DeanWithTemporaryPassword, ct);
        await using var _host = host;
        var id = await SeedTemplateAsync(host, ct);
        var before = await host.TemplateFingerprintAsync(ct);
        await client.GetAsync(ChangePasswordPath, ct);

        var response = await SendAsync(client, endpoint, id, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(ChangePasswordPath, response.LocationPath);
        Assert.Equal(before, await host.TemplateFingerprintAsync(ct));
    }

    [Theory]
    [MemberData(nameof(AllowedCases))]
    public async Task AnAdminAndADean_CanUseEveryTemplatePage(Actor actor, string endpoint)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        var id = await SeedTemplateAsync(host, ct);
        await client.GetAsync(ReportTemplateTestData.NewPath, ct);

        var response = await SendAsync(client, endpoint, id, ct);

        Assert.Equal(IsPost(endpoint) ? HttpStatusCode.Redirect : HttpStatusCode.OK, response.Status);
        if (IsPost(endpoint))
        {
            Assert.Equal(ReportTemplateTestData.ListPath, response.LocationPath);
        }
    }

    [Theory]
    [MemberData(nameof(Posts))]
    public async Task APostWithoutTheAntiforgeryToken_Is400(string endpoint)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var id = await SeedTemplateAsync(host, ct);
        var before = await host.TemplateFingerprintAsync(ct);

        var response = await client.PostFormAsync(PathOf(endpoint, id), BodyOf(endpoint), ct, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal(before, await host.TemplateFingerprintAsync(ct));
    }
}
