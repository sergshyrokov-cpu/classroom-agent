using System.Net;
using System.Runtime.Versioning;
using ClassroomAgent.Tests.TestInfrastructure;
using ClassroomAgent.Web.Security;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-049 on the installation over HTTP (spec FR-001 … FR-003, FR-006; AC-001, AC-002, AC-003): a Dean signs in and
/// submits a form with keys written encrypted; a restart over keys the account cannot decrypt refuses earlier session
/// cookies and antiforgery tokens yet lets the Dean sign in again; a restart over its own keys keeps both. A DPAPI key
/// made undecryptable stands in for one written under another Windows account (spec I-2).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class InstallationKeyEncryptionTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task ADeanSignsInAndSubmitsAForm_WithEveryKeyWrittenEncrypted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var (dean, _) = await host.SignInDeanAsync(ct);
        using (dean)
        {
            var home = await dean.GetAsync(SignInRoutes.Landing, ct);
            var signOut = await dean.PostFormAsync(SignInRoutes.SignOut, [], ct);

            Assert.Equal(HttpStatusCode.OK, home.Status);
            Assert.Equal(HttpStatusCode.Redirect, signOut.Status);
        }

        KeyRingFiles.AssertAllEncryptedForCurrentUser(host.KeyDirectory);
    }

    [Fact]
    public async Task AfterARestartOverKeysTheAccountCannotDecrypt_EarlierSessionAndFormTokenAreRefused_AndTheDeanSignsInAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var first = await InstallationTestHost.StartAsync(database, ct);
        var earlier = await EarlierCredentialsAsync(first, ct);
        var blobs = KeyRingFiles.MakeEveryKeyUndecryptable(first.KeyDirectory);
        var before = KeyRingFiles.Snapshot(first.KeyDirectory);

        await using var second = RestartOverSameKeys(first);
        var session = await ReplaySessionAsync(second, earlier.Session, ct);
        var form = await ReplaySignInFormAsync(second, earlier, ct);
        using var fresh = second.CreateClient();
        var signIn = await fresh.SignInAsDeanAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.TemporaryPassword, ct);
        var home = await fresh.GetAsync(SignInRoutes.Landing, ct);

        Assert.Equal(HttpStatusCode.Redirect, session.Status);
        Assert.Equal(SignInRoutes.SignInPage, session.LocationPath);
        Assert.Equal(HttpStatusCode.BadRequest, form.Status);
        Assert.Equal(HttpStatusCode.Redirect, signIn.Status);
        Assert.Equal(HttpStatusCode.OK, home.Status);
        KeyRingFiles.AssertUnchanged(first.KeyDirectory, before);
        var created = KeyRingFiles.NewSince(first.KeyDirectory, before);
        Assert.NotEmpty(created);
        Assert.All(created, key => Assert.True(key.IsEncryptedForCurrentUser, key.Describe()));
        var logs = await second.ReadLogFilesAsync(ct);
        Assert.All(blobs, blob => Assert.DoesNotContain(logs, content => content.Contains(blob, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task AfterARestartOverItsOwnEncryptedKeys_EarlierSessionAndFormTokenAreStillAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var first = await InstallationTestHost.StartAsync(database, ct);
        var earlier = await EarlierCredentialsAsync(first, ct);
        KeyRingFiles.AssertAllEncryptedForCurrentUser(first.KeyDirectory);

        await using var second = RestartOverSameKeys(first);
        var session = await ReplaySessionAsync(second, earlier.Session, ct);
        var form = await ReplaySignInFormAsync(second, earlier, ct);

        Assert.Equal(HttpStatusCode.OK, session.Status);
        Assert.Equal(HttpStatusCode.Redirect, form.Status);
    }

    /// <summary>A signed-in Dean's session cookies, and an anonymous visitor's sign-in form token with its antiforgery cookie.</summary>
    private static async Task<EarlierCredentials> EarlierCredentialsAsync(InstallationTestHost host, CancellationToken ct)
    {
        var (dean, _) = await host.SignInDeanAsync(ct);
        using (dean)
        {
            Assert.Equal(HttpStatusCode.OK, (await dean.GetAsync(SignInRoutes.Landing, ct)).Status);
            using var visitor = host.CreateClient();
            await visitor.GetAsync(DeanAccountTestData.Paths.SignIn, ct);
            return new EarlierCredentials(dean.Cookies, visitor.Cookies, visitor.LastToken!);
        }
    }

    /// <summary>A restart over the same database, installation id and key directory.</summary>
    private static InstallationTestHost RestartOverSameKeys(InstallationTestHost first)
    {
        var second = InstallationTestHost.Restart(first, first.Time.GetUtcNow());
        second.Settings[InstallationConfigurationKeys.DataProtectionKeyDirectory] = first.KeyDirectory;
        second.Start();
        return second;
    }

    private static async Task<PageResponse> ReplaySessionAsync(
        InstallationTestHost host,
        IReadOnlyDictionary<string, string> cookies,
        CancellationToken ct)
    {
        using var replay = host.CreateClient();
        replay.ReplaceCookies(cookies);
        return await replay.GetAsync(SignInRoutes.Landing, ct);
    }

    private static async Task<PageResponse> ReplaySignInFormAsync(
        InstallationTestHost host,
        EarlierCredentials earlier,
        CancellationToken ct)
    {
        using var visitor = host.CreateClient();
        visitor.ReplaceCookies(earlier.VisitorCookies);
        return await visitor.SendAsync(
            HttpMethod.Post,
            DeanAccountTestData.Paths.SignIn,
            new FormUrlEncodedContent(
            [
                new(DeanAccountTestData.Fields.Email, DeanAccountTestData.DeanEmail),
                new(DeanAccountTestData.Fields.Password, DeanAccountTestData.TemporaryPassword),
                new(Html.AntiforgeryFieldName, earlier.FormToken),
            ]),
            ct);
    }

    private sealed record EarlierCredentials(
        IReadOnlyDictionary<string, string> Session,
        IReadOnlyDictionary<string, string> VisitorCookies,
        string FormToken);
}
