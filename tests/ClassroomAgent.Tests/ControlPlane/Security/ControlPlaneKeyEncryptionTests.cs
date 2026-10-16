using System.Net;
using System.Runtime.Versioning;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Security;

/// <summary>
/// US-049 on the Control Plane over HTTP (spec FR-001 … FR-003, FR-006; AC-001, AC-002, AC-003): the Owner signs in
/// and submits forms with keys written encrypted; a restart over keys the account cannot decrypt refuses earlier
/// session cookies and antiforgery tokens yet lets the Owner sign in again; a restart over its own keys keeps both.
/// A DPAPI key made undecryptable stands in for one written under another Windows account (spec I-2).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ControlPlaneKeyEncryptionTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task TheOwnerSetsUpSignsOutAndSignsInAgain_WithEveryKeyWrittenEncrypted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var home = await owner.GetAsync("/", ct);
        var signOut = await owner.PostFormAsync("/sign-out", [], ct);
        var (again, signIn) = await host.SignInAsync(TestData.Login, TestData.Password, ct);
        using (again)
        {
            Assert.Equal(HttpStatusCode.OK, home.Status);
            Assert.Equal(HttpStatusCode.Redirect, signOut.Status);
            Assert.Equal(HttpStatusCode.Redirect, signIn.Status);
            Assert.Equal("/", signIn.LocationPath);
        }

        KeyRingFiles.AssertAllEncryptedForCurrentUser(host.KeyDirectory);
    }

    [Fact]
    public async Task AfterARestartOverKeysTheAccountCannotDecrypt_EarlierSessionAndFormTokenAreRefused_AndTheOwnerSignsInAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var first = await ControlPlaneTestHost.StartAsync(database, ct);
        var earlier = await EarlierCredentialsAsync(first, ct);
        var blobs = KeyRingFiles.MakeEveryKeyUndecryptable(first.KeyDirectory);
        var before = KeyRingFiles.Snapshot(first.KeyDirectory);

        await using var second = RestartOverSameKeys(first);
        var session = await ReplaySessionAsync(second, earlier.Session, ct);
        var form = await ReplaySignInFormAsync(second, earlier, ct);
        var (fresh, signIn) = await second.SignInAsync(TestData.Login, TestData.Password, ct);
        using (fresh)
        {
            var home = await fresh.GetAsync("/", ct);

            Assert.Equal(HttpStatusCode.Redirect, session.Status);
            Assert.Equal("/sign-in", session.LocationPath);
            Assert.Equal(HttpStatusCode.BadRequest, form.Status);
            Assert.Equal(HttpStatusCode.Redirect, signIn.Status);
            Assert.Equal(HttpStatusCode.OK, home.Status);
        }

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
        await using var first = await ControlPlaneTestHost.StartAsync(database, ct);
        var earlier = await EarlierCredentialsAsync(first, ct);
        KeyRingFiles.AssertAllEncryptedForCurrentUser(first.KeyDirectory);

        await using var second = RestartOverSameKeys(first);
        var session = await ReplaySessionAsync(second, earlier.Session, ct);
        var form = await ReplaySignInFormAsync(second, earlier, ct);

        Assert.Equal(HttpStatusCode.OK, session.Status);
        Assert.Equal(HttpStatusCode.Redirect, form.Status);
        Assert.Equal("/", form.LocationPath);
    }

    /// <summary>The Owner's session cookies, and an anonymous visitor's sign-in form token with its antiforgery cookie.</summary>
    private static async Task<EarlierCredentials> EarlierCredentialsAsync(ControlPlaneTestHost host, CancellationToken ct)
    {
        using var owner = await host.CreateOwnerAsync(ct);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/", ct)).Status);
        using var visitor = host.CreateClient();
        await visitor.GetAsync("/sign-in", ct);
        return new EarlierCredentials(owner.Cookies, visitor.Cookies, visitor.LastToken!);
    }

    private static ControlPlaneTestHost RestartOverSameKeys(ControlPlaneTestHost first) =>
        ControlPlaneTestHost.Restart(
            first,
            TestData.SetupCode,
            new Dictionary<string, string> { [ConfigurationKeys.DataProtectionKeyDirectory] = first.KeyDirectory });

    private static async Task<PageResponse> ReplaySessionAsync(
        ControlPlaneTestHost host,
        IReadOnlyDictionary<string, string> cookies,
        CancellationToken ct)
    {
        using var replay = host.CreateClient();
        replay.ReplaceCookies(cookies);
        return await replay.GetAsync("/", ct);
    }

    private static async Task<PageResponse> ReplaySignInFormAsync(
        ControlPlaneTestHost host,
        EarlierCredentials earlier,
        CancellationToken ct)
    {
        using var visitor = host.CreateClient();
        visitor.ReplaceCookies(earlier.VisitorCookies);
        return await visitor.SendAsync(
            HttpMethod.Post,
            "/sign-in",
            new FormUrlEncodedContent([.. TestData.SignInFields(), new(Html.AntiforgeryFieldName, earlier.FormToken)]),
            ct);
    }

    private sealed record EarlierCredentials(
        IReadOnlyDictionary<string, string> Session,
        IReadOnlyDictionary<string, string> VisitorCookies,
        string FormToken);
}
