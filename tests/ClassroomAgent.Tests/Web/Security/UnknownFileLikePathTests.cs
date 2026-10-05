using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-041 AC-001, AC-003, AC-004 for the installation: an unmatched path answers <c>404</c> to anyone, also when its
/// last segment contains a dot; existing static files and real protected pages are unchanged (spec FR-001, FR-003 …
/// FR-006; SC-4 v66).
/// </summary>
public sealed class UnknownFileLikePathTests(PostgreSqlFixture database)
{
    public static TheoryData<Actor, string> CallersAndPaths()
    {
        var data = new TheoryData<Actor, string>();
        foreach (var actor in new[] { Actor.Anonymous, Actor.Admin, Actor.Dean })
        {
            foreach (var path in new[] { "/robots.txt", "/css/no-such-file.css", "/favicon.ico", "/no/such/report.xlsx" })
            {
                data.Add(actor, path);
            }
        }

        return data;
    }

    /// <summary>AC-001, FR-001: the error page with 404 — never a redirect to sign-in.</summary>
    [Theory]
    [MemberData(nameof(CallersAndPaths))]
    public async Task AMissingFileLikePath_AnswersTheErrorPageWith404(Actor actor, string path)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        using var _client = client;

        var page = await client.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        Assert.Null(page.Location);
        Assert.Contains(host.Text(SignInTestData.TextKeys.NotFound, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-001, FR-001: any method, not only GET.</summary>
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task AMissingFileLikePath_AnyMethod_Answers404ToAnAnonymousCaller(string method)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var anonymous = host.CreateClient();

        var response = await anonymous.SendAsync(new HttpMethod(method), "/robots.txt", new FormUrlEncodedContent([]), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.Null(response.Location);
    }

    /// <summary>AC-001, FR-003, spec §7: the catch-all reads and writes nothing — no account, no audit row.</summary>
    [Fact]
    public async Task AMissingFileLikePath_WritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var anonymous = host.CreateClient();

        var page = await anonymous.GetAsync("/robots.txt", ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        Assert.Empty(await host.AppUsersAsync(ct));
        Assert.Empty(await host.AuditRowsAsync(ct));
    }

    /// <summary>AC-001, spec §6: the path is not echoed into the response.</summary>
    [Fact]
    public async Task AMissingFileLikePath_IsNotEchoed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var anonymous = host.CreateClient();

        var page = await anonymous.GetAsync("/zz-marker-41.txt", ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        Assert.DoesNotContain("zz-marker-41", page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-003, FR-004: every existing file of <c>wwwroot</c> is still served anonymously with 200.</summary>
    [Fact]
    public async Task AnExistingStaticFile_IsStillServedAnonymously()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var anonymous = host.CreateClient();

        foreach (var path in NoStore.StaticFilesOf("ClassroomAgent.Web"))
        {
            var response = await anonymous.GetAsync(path, ct);

            Assert.True(response.Status == HttpStatusCode.OK, $"GET {path}: {(int)response.Status}");
        }
    }

    /// <summary>AC-004, FR-006: a real protected page still sends an anonymous caller to sign-in.</summary>
    [Fact]
    public async Task ARealProtectedPage_StillRedirectsAnAnonymousCallerToSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var anonymous = host.CreateClient();

        var page = await anonymous.GetAsync("/workspace/journal", ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.NotNull(page.Location);
        Assert.Contains(SignInTestData.SignInPath, page.Location!.ToString(), StringComparison.Ordinal);
    }

    /// <summary>AC-004, FR-005: on the private port an unmatched path under a private prefix answers 404 with no error page.</summary>
    [Theory]
    [InlineData("/health/x.txt")]
    [InlineData("/health/live.json")]
    public async Task OnThePrivatePort_AnUnmatchedFileLikePathUnderAPrivatePrefix_Answers404(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var response = await host.SendPrivateAsync("GET", path, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.False(response.Headers.ContainsKey("Location"), $"GET {path}: redirected to {response.Headers.GetValueOrDefault("Location")}");
        Assert.DoesNotContain(host.Text(SignInTestData.TextKeys.NotFound, "uk"), response.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-004, FR-005: on the private port a path outside the private paths keeps its 404 from the port filter.</summary>
    [Fact]
    public async Task OnThePrivatePort_AFileLikePathOutsideThePrivatePaths_Answers404()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var response = await host.SendPrivateAsync("GET", "/robots.txt", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.False(response.Headers.ContainsKey("Location"));
    }
}
