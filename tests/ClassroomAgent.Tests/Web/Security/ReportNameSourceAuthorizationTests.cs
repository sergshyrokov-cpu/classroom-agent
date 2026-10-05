using System.Globalization;
using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-042 AC-009: the endpoints that gained the name source — the report with <c>names</c> and the two template saves
/// with the field <c>names</c> — stay open to an Admin and a Dean, send an anonymous visitor to sign-in and a Dean with a
/// temporary password to the forced change (api-design §5; SC-4; TC-5).
/// </summary>
public sealed class ReportNameSourceAuthorizationTests(PostgreSqlFixture database)
{
    private const string ChangePasswordPath = "/sign-in/change-password";

    private const long AnyCourse = 1;

    public static TheoryData<string> Endpoints => new("report", "create", "change");

    public static TheoryData<Actor, string> AllowedCases
    {
        get
        {
            var data = new TheoryData<Actor, string>();
            foreach (var actor in new[] { Actor.Admin, Actor.Dean })
            {
                foreach (var endpoint in new[] { "report", "create", "change" })
                {
                    data.Add(actor, endpoint);
                }
            }

            return data;
        }
    }

    private static bool IsPost(string endpoint) => endpoint is "create" or "change";

    private static string PathOf(string endpoint, long id, long courseId) =>
        endpoint switch
        {
            "report" => ReportTemplateTestData.SeptemberReportUrl(courseId, names: "email"),
            "create" => ReportTemplateTestData.ListPath,
            "change" => ReportTemplateTestData.ChangePath(id.ToString(CultureInfo.InvariantCulture)),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, null),
        };

    private static IEnumerable<KeyValuePair<string, string>> BodyOf(string endpoint) =>
        endpoint switch
        {
            "create" => ReportTemplateFormBuilder.Valid("Test Template Auth New")
                .Set(ReportTemplateTestData.NamesField, "email").Http(),
            "change" => ReportTemplateFormBuilder.Valid("Test Template Auth Changed")
                .Set(ReportTemplateTestData.NamesField, "email").Http(),
            _ => [],
        };

    private static async Task<long> SeedTemplateAsync(InstallationTestHost host, CancellationToken ct)
    {
        const string author = "author.auth@school-one.example.test";
        await host.InsertAppUserAsync(ct, email: author);
        var authorId = await host.AccountIdAsync(author, ct);
        return await host.InsertTemplateAsync(ct, "Test Template Auth Seed", authorId);
    }

    private static Task<PageResponse> SendAsync(FormClient client, string endpoint, long id, long courseId, CancellationToken ct) =>
        IsPost(endpoint)
            ? client.PostFormAsync(PathOf(endpoint, id, courseId), BodyOf(endpoint), ct)
            : client.GetAsync(PathOf(endpoint, id, courseId), ct);

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Anonymous_IsSentToSignIn(string endpoint)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Anonymous, ct);
        await using var _host = host;
        var id = await SeedTemplateAsync(host, ct);
        var before = await host.TemplateFingerprintAsync(ct);
        // The sign-in page supplies a token, so the refusal under test is the authorization one.
        await client.GetAsync(SignInTestData.SignInPath, ct);

        var response = await SendAsync(client, endpoint, id, AnyCourse, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(SignInTestData.SignInPath, response.LocationPath);
        Assert.Equal(before, await host.TemplateFingerprintAsync(ct));
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task ARestrictedDean_IsSentToTheForcedChange(string endpoint)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.DeanWithTemporaryPassword, ct);
        await using var _host = host;
        var id = await SeedTemplateAsync(host, ct);
        var before = await host.TemplateFingerprintAsync(ct);
        await client.GetAsync(ChangePasswordPath, ct);

        var response = await SendAsync(client, endpoint, id, AnyCourse, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(ChangePasswordPath, response.LocationPath);
        Assert.Equal(before, await host.TemplateFingerprintAsync(ct));
    }

    [Theory]
    [MemberData(nameof(AllowedCases))]
    public async Task AnAdminAndADean_CanUseTheNameSource(Actor actor, string endpoint)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);
        var id = await SeedTemplateAsync(host, ct);
        await client.GetAsync(ReportTemplateTestData.NewPath, ct);

        var response = await SendAsync(client, endpoint, id, seeded.CourseId, ct);

        Assert.Equal(IsPost(endpoint) ? HttpStatusCode.Redirect : HttpStatusCode.OK, response.Status);
        if (IsPost(endpoint))
        {
            Assert.Equal(ReportTemplateTestData.ListPath, response.LocationPath);
        }
    }
}
