using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-010 AC-003: the instruction lists exactly the six read-only scopes <c>trebovaniya.md</c> §6 fixes, and
/// nothing else (spec FR-004; NFR-021, SC-8). The list is a requirement, not a configuration: a scope added in
/// code without a requirements change fails here, and so does a scope §6 excludes.
/// </summary>
/// <remarks>
/// These assertions require the instruction to be rendered **once** on the page — inside the
/// <c>&lt;pre id="connection-instruction"&gt;</c> the Admin copies, which is also the visible text (OD-002: the
/// text stays selectable, and the affordance only copies it). A page that printed the scopes twice, once for
/// reading and once for copying, would fail the exactly-six assertions below; that is deliberate, because two
/// copies can disagree.
/// </remarks>
public sealed class ConnectionInstructionScopeTests(PostgreSqlFixture database)
{
    /// <summary>AC-003: every one of the six scopes is rendered, as the full URI the Google console accepts.</summary>
    [Theory]
    [MemberData(nameof(ConnectionInstructionTestData.ScopeList), MemberType = typeof(ConnectionInstructionTestData))]
    public async Task EveryRequiredScope_IsRendered(string scope)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);

        Assert.Contains(scope, page.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-003: the rendered list is **exactly** the six, in the fixed order of the contract — not a superset.
    /// A seventh scope, however plausible, fails here.
    /// </summary>
    [Fact]
    public async Task TheRenderedList_IsExactlyTheSixScopesInOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);
        var rendered = ScopeUris(page.Text);

        Assert.Equal(ConnectionInstructionTestData.Scopes, rendered);
    }

    /// <summary>
    /// AC-003: <c>drive.file</c> and <c>classroom.profile.photos</c> — the two the prototype requested, which no
    /// Epic uses and which §6 (v25) excludes — appear nowhere, and neither do the identity scopes of the
    /// Admin's own sign-in: delegation and sign-in are different mechanisms (§6, v78).
    /// </summary>
    [Theory]
    [MemberData(nameof(ConnectionInstructionTestData.ForbiddenScopes), MemberType = typeof(ConnectionInstructionTestData))]
    public async Task AForbiddenScope_AppearsNowhere(string fragment)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);

        // The page must really be the instruction: an absence on an error page is not an absence in the list.
        Assert.Equal(System.Net.HttpStatusCode.OK, page.Status);
        Assert.NotEmpty(ScopeUris(page.Text));
        Assert.DoesNotContain(fragment, page.Text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC-003, NFR-021: every scope rendered is read-only. The three Classroom scopes that are not spelled
    /// <c>.readonly</c> are the two profile ones and the materials one; only the fixed list is acceptable, so
    /// this test states the rule the list satisfies rather than trusting the spelling.
    /// </summary>
    [Fact]
    public async Task EveryRenderedScope_IsOneOfTheRequirementsReadOnlyScopes()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);
        var rendered = ScopeUris(page.Text);

        // Asserted before Assert.All: over an empty list every predicate holds, which would be no evidence.
        Assert.Equal(ConnectionInstructionTestData.Scopes.Length, rendered.Count);
        Assert.All(rendered, scope => Assert.Contains(scope, ConnectionInstructionTestData.Scopes));
    }

    /// <summary>
    /// AC-003: the scope list is not a configuration. It is the same in every installation state, including one
    /// that has never confirmed its legitimacy — it comes from code, not from the Control Plane.
    /// </summary>
    [Fact]
    public async Task TheScopeList_IsTheSameWithoutASuccessfulCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(
            database,
            ct,
            ReadOnlyModeHost.Cause.NeverConfirmed);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);

        Assert.Equal(ConnectionInstructionTestData.Scopes, ScopeUris(page.Text));
    }

    /// <summary>AC-003: each scope is rendered as data — exactly as the requirement spells it, untranslated.</summary>
    [Fact]
    public async Task TheScopesAreNotTranslated()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = WorkspaceConnectionHostExtensions.ApprovingChannel();
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await host.InsertLegitimacyStateAsync(ct, InstallationTestHost.DefaultStart - TimeSpan.FromHours(1));
        await host.InsertAppUserAsync(ct, uiLanguage: "en");
        host.ControlPlaneHandler = channel;
        host.Start();
        var (client, _) = await host.SignInWithGoogleAsync(ct);

        var page = await client.OpenInstructionAsync(ct);

        Assert.Equal(ConnectionInstructionTestData.Scopes, ScopeUris(page.Text));
    }

    /// <summary>Every Google scope URI the page renders, in the order it renders them.</summary>
    private static IReadOnlyList<string> ScopeUris(string text) =>
        System.Text.RegularExpressions.Regex
            .Matches(text, @"https://www\.googleapis\.com/auth/[A-Za-z0-9._-]+", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(2))
            .Select(m => m.Value)
            .ToList();
}
