using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-010 AC-009: the instruction is ready to hand over — from the page itself, with a copy-to-clipboard
/// affordance and no download endpoint (OD-002; spec FR-007, FR-008, S-08, S-09). What leaves the installation
/// is only what the Admin themselves sends, it carries no secret, and the page is complete without scripting.
/// </summary>
public sealed class ConnectionInstructionHandoverTests(PostgreSqlFixture database)
{
    /// <summary>AC-009: the text to hand over is in the markup, and it carries the school's own two values.</summary>
    [Fact]
    public async Task TheHandedOverText_CarriesTheClientIdAndTheDomain()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);
        var text = ConnectionInstructionHostExtensions.HandedOverText(page);

        Assert.Contains(ConnectionInstructionTestData.ClientId, text, StringComparison.Ordinal);
        Assert.Contains(ConnectionInstructionTestData.Domain, text, StringComparison.Ordinal);
    }

    /// <summary>AC-009: and the scope list, in full.</summary>
    [Fact]
    public async Task TheHandedOverText_CarriesEveryScope()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);
        var text = ConnectionInstructionHostExtensions.HandedOverText(page);

        foreach (var scope in ConnectionInstructionTestData.Scopes)
        {
            Assert.Contains(scope, text, StringComparison.Ordinal);
        }
    }

    /// <summary>AC-009: and the technical-account requirements, so the super-admin needs nothing else.</summary>
    [Fact]
    public async Task TheHandedOverText_CarriesTheTechnicalAccountRequirements()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);
        var text = ConnectionInstructionHostExtensions.HandedOverText(page);

        foreach (var key in ConnectionInstructionTestData.TextKeys.TechnicalAccountStatements)
        {
            Assert.Contains(host.Text(key, "uk"), text, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC-009, SC-7 (a Hard Stop): the handed-over text carries **no secret** — not the OAuth client secret,
    /// not the reference to it, not a session identifier and not an antiforgery token.
    /// </summary>
    [Fact]
    public async Task TheHandedOverText_CarriesNoSecret()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);
        var text = ConnectionInstructionHostExtensions.HandedOverText(page);

        Assert.DoesNotContain(InstallationConfigurationKeys.OAuthClientSecretValue, text, StringComparison.Ordinal);
        Assert.DoesNotContain(InstallationConfigurationKeys.OAuthClientSecretReferenceValue, text, StringComparison.Ordinal);
        Assert.DoesNotContain(InstallationConfigurationKeys.OAuthClientIdValue, text, StringComparison.Ordinal);
        Assert.DoesNotContain(SignInTestData.SessionCookieName, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Html.AntiforgeryFieldName, text, StringComparison.Ordinal);
    }

    /// <summary>AC-009, SC-7: nor does the page as a whole leak the secret or its reference.</summary>
    [Fact]
    public async Task ThePage_CarriesNoSecretAnywhere()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.DoesNotContain(InstallationConfigurationKeys.OAuthClientSecretValue, page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(InstallationConfigurationKeys.OAuthClientSecretReferenceValue, page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-009, OD-002: the copy affordance is present, with its translated label.</summary>
    [Fact]
    public async Task ThePage_CarriesTheCopyAffordance()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);

        Assert.True(
            ConnectionInstructionHostExtensions.HasElement(page, ConnectionInstructionTestData.CopyButtonId),
            "The page carries no copy-to-clipboard control (OD-002).");
        Assert.Contains(host.Text(ConnectionInstructionTestData.TextKeys.CopyButton, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-009, spec I-5: the affordance is a **static script file**, not inline script, so a
    /// Content-Security-Policy added later needs no <c>unsafe-inline</c>.
    /// </summary>
    [Fact]
    public async Task TheCopyAffordance_IsAStaticScriptFileAndNotInlineScript()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);

        Assert.Empty(ConnectionInstructionHostExtensions.InlineScripts(page));
        Assert.NotEmpty(ConnectionInstructionHostExtensions.ScriptSources(page));
    }

    /// <summary>
    /// AC-009: the script the page references is really served — from the static files directory, which SC-4's
    /// existing "Static files — CSS, JS, images" entry covers, so the anonymous closed list gains nothing.
    /// </summary>
    [Fact]
    public async Task TheReferencedScript_IsServedAsAStaticFile()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);
        var source = Assert.Single(
            ConnectionInstructionHostExtensions.ScriptSources(page),
            s => s.StartsWith("/js/", StringComparison.Ordinal));
        var served = await client.GetAsync(source, ct);

        Assert.Equal(HttpStatusCode.OK, served.Status);
    }

    /// <summary>
    /// AC-009, OD-002: the affordance degrades safely. The page is complete and the text selectable without any
    /// script — proven by asserting that the instruction is entirely in the markup the server sent, so a browser
    /// with scripting disabled shows the same words.
    /// </summary>
    [Fact]
    public async Task WithoutScripting_TheInstructionIsStillComplete()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);
        var text = ConnectionInstructionHostExtensions.HandedOverText(page);

        Assert.Contains(ConnectionInstructionTestData.ClientId, text, StringComparison.Ordinal);
        Assert.All(ConnectionInstructionTestData.Scopes, scope => Assert.Contains(scope, text, StringComparison.Ordinal));
        Assert.Empty(ConnectionInstructionHostExtensions.InlineScripts(page));
    }

    /// <summary>
    /// AC-009, OD-002: there is **no download endpoint**. A stored document would outlive a rotated client ID,
    /// which is what AC-006 exists to prevent, so no route of this Story returns a file.
    /// </summary>
    [Theory]
    [InlineData(".txt")]
    [InlineData(".pdf")]
    [InlineData("/download")]
    [InlineData("/export")]
    public async Task NoDownloadRouteExists(string suffix)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        // The instruction itself must be served, or "no download route" would hold of a Story that shipped nothing.
        var instruction = await client.OpenInstructionAsync(ct);
        var response = await client.GetAsync(ConnectionInstructionTestData.Path + suffix, ct);

        Assert.Equal(HttpStatusCode.OK, instruction.Status);
        Assert.Equal(HttpStatusCode.NotFound, response.Status);
    }

    /// <summary>
    /// AC-009, spec FR-007: the instruction path accepts no <c>POST</c>. Nothing on this page is submitted, so
    /// the contract documents no <c>400</c> and no <c>409</c> (api-design §2.1).
    /// </summary>
    [Fact]
    public async Task TheInstructionPath_AcceptsNoPost()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var instruction = await client.OpenInstructionAsync(ct);
        await client.GetAsync(SignInTestData.LandingPath, ct);
        var response = await client.PostFormAsync(ConnectionInstructionTestData.Path, [], ct);

        Assert.Equal(HttpStatusCode.OK, instruction.Status);
        Assert.NotEqual(HttpStatusCode.OK, response.Status);
        Assert.NotEqual(HttpStatusCode.Redirect, response.Status);
    }
}
