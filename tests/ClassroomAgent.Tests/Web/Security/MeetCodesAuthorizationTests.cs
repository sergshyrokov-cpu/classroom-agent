using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-032 AC-014, spec §7, TC-5, api-design §4: each of the five new operations is open to an Admin and a Dean, sends an
/// anonymous visitor to sign-in and a Dean on the forced temporary-password session to the password change. Both roles
/// are allowed, so the forbidden case is the unauthenticated request. The allowed case asserts the operation's own
/// success status, so a missing route cannot pass it.
/// </summary>
public sealed class MeetCodesAuthorizationTests(PostgreSqlFixture database)
{
    private const string ChangePasswordPath = "/sign-in/change-password";

    public static TheoryData<string> Operations => new("page", "choice", "link", "confirmation", "mark");

    public static TheoryData<Actor, string> AllowedCases
    {
        get
        {
            var data = new TheoryData<Actor, string>();
            foreach (var actor in new[] { Actor.Admin, Actor.Dean })
            {
                foreach (var operation in new[] { "page", "choice", "link", "confirmation", "mark" })
                {
                    data.Add(actor, operation);
                }
            }

            return data;
        }
    }

    private static bool IsPost(string operation) => operation is "link" or "confirmation" or "mark";

    private static async Task<(string Path, KeyValuePair<string, string>[] Fields)> PrepareAsync(
        InstallationTestHost host, string operation, CancellationToken ct)
    {
        await host.PrepareAsync(ct);
        var alpha = await host.SeedRosterCourseAsync("Test Course Alpha", [Mc.Email("mc.teacher1")], [], ct);
        var organizer = Mc.Email("mc.teacher1");
        var at = new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);
        await host.SeedMeetingAsync("auth-0001-aaa", organizer, at, ct);
        await host.SeedMeetingAsync("auth-0002-bbb", organizer, at, ct);
        await host.SeedAutoLinkAsync("auth-0002-bbb", alpha, at, ct);
        return operation switch
        {
            "page" => (Mc.Path, []),
            "choice" => (Mc.ChoicePath("auth-0001-aaa"), []),
            "link" => (Mc.LinkPath("auth-0001-aaa"),
                [Mc.Field(Mc.Fields.CourseId, alpha), Mc.Field(Mc.Fields.ExpectedState, Mc.States.Unassigned)]),
            "confirmation" => (Mc.ConfirmationPath("auth-0002-bbb"), [Mc.Field(Mc.Fields.ExpectedCourseId, alpha)]),
            "mark" => (Mc.MarkPath("auth-0001-aaa"), [Mc.Field(Mc.Fields.ExpectedState, Mc.States.Unassigned)]),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
        };
    }

    private static Task<PageResponse> SendAsync(
        FormClient client, string operation, string path, KeyValuePair<string, string>[] fields, string tokenPage, CancellationToken ct) =>
        IsPost(operation)
            ? client.PostWithTokenAsync(path, fields, ct, tokenPage: tokenPage)
            : client.GetAsync(path, ct);

    [Theory]
    [MemberData(nameof(AllowedCases))]
    public async Task AnAdminAndADean_CanUseEveryOperation(Actor actor, string operation)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        var (path, fields) = await PrepareAsync(host, operation, ct);

        var response = await SendAsync(client, operation, path, fields, SignInTestData.LandingPath, ct);

        Assert.NotEqual(HttpStatusCode.Forbidden, response.Status);
        if (IsPost(operation))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.Status);
            Assert.StartsWith(Mc.Path, response.LocationPath, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, response.Status);
        }
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task Anonymous_IsSentToSignIn_OnEveryOperation(string operation)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Anonymous, ct);
        await using var _host = host;
        var (path, fields) = await PrepareAsync(host, operation, ct);
        var before = await host.McLinksFingerprintAsync(ct);

        var response = await SendAsync(client, operation, path, fields, SignInTestData.SignInPath, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(SignInTestData.SignInPath, response.LocationPath);
        Assert.Equal(before, await host.McLinksFingerprintAsync(ct));
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task ARestrictedDean_IsSentToTheForcedChange_OnEveryOperation(string operation)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.DeanWithTemporaryPassword, ct);
        await using var _host = host;
        var (path, fields) = await PrepareAsync(host, operation, ct);
        var before = await host.McLinksFingerprintAsync(ct);

        var response = await SendAsync(client, operation, path, fields, ChangePasswordPath, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(ChangePasswordPath, response.LocationPath);
        Assert.Equal(before, await host.McLinksFingerprintAsync(ct));
    }
}
