using System.Net;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-011 AC-002, AC-003: a run is eight steps in a fixed order — one delegated-token request per scope of §6, then
/// one Classroom read and one Admin Reports read — as the technical account of the usable connection, and the page
/// shows every step and the verdict (spec FR-001…FR-003, VR-002, VR-003, S-04, S-05; openapi POST 200).
/// </summary>
public sealed class AccessCheckRunTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task WhenEverythingIsInPlace_TheRunAnswers200_WithTheVerdictAccessInPlace()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.RunAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(AccessCheckTestData.Verdict.AccessInPlace, AccessCheckHostExtensions.VerdictOf(page));
        Assert.Contains(host.Text(AccessCheckTestData.TextKeys.VerdictAccessInPlace, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>VR-002, S-04: exactly the six scopes, one per token request, in order, none repeated, no other.</summary>
    [Fact]
    public async Task TheRun_RequestsExactlyTheSixScopes_OnePerRequest_InOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.RunAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var scopes = probe.RequestCalls.Where(c => c.Kind == ProbeCallKind.Delegation).Select(c => c.Scope ?? string.Empty).ToList();
        Assert.Equal(AccessCheckTestData.Scopes, scopes);
        Assert.All(
            AccessCheckTestData.ForbiddenScopeFragments,
            fragment => Assert.DoesNotContain(scopes, s => s.Contains(fragment, StringComparison.Ordinal)));
    }

    /// <summary>Spec FR-001: the two reads come after the six delegations, Classroom first, each once.</summary>
    [Fact]
    public async Task TheRun_MakesTheTwoReadsAfterTheDelegations_EachOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        await client.RunAccessCheckAsync(ct);

        var kinds = probe.RequestCalls.Select(c => c.Kind).ToList();
        Assert.Equal(
            [.. Enumerable.Repeat(ProbeCallKind.Delegation, 6), ProbeCallKind.Courses, ProbeCallKind.MeetActivity],
            kinds);
    }

    /// <summary>VR-003, S-05: the impersonated subject is the stored technical account — never the signed-in Admin.</summary>
    [Fact]
    public async Task EveryDelegation_ImpersonatesTheStoredTechnicalAccount_NotTheAdmin()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        await client.RunAccessCheckAsync(ct);

        var delegations = probe.RequestCalls.Where(c => c.Kind == ProbeCallKind.Delegation).ToList();
        Assert.Equal(6, delegations.Count);
        Assert.All(delegations, c => Assert.Equal(AccessCheckTestData.TechnicalAccount, c.TechnicalAccount));
        Assert.All(delegations, c => Assert.NotEqual(SignInTestData.AdminEmail, c.TechnicalAccount, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>VR-001, VR-003: a value posted under any field name has nothing to bind to — the stored account is used.</summary>
    [Fact]
    public async Task APostedAccountOrScope_IsIgnored()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.RunAccessCheckAsync(
            ct,
            extraFields:
            [
                new("technicalAccount", "attacker@school-one.example.test"),
                new("impersonationUserEmail", "attacker@school-one.example.test"),
                new("scope", "https://www.googleapis.com/auth/drive.file"),
            ]);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.NotEmpty(probe.RequestCalls);
        Assert.All(
            probe.RequestCalls.Where(c => c.Kind == ProbeCallKind.Delegation),
            c => Assert.Equal(AccessCheckTestData.TechnicalAccount, c.TechnicalAccount));
        Assert.Equal(AccessCheckTestData.Scopes, probe.RequestCalls.Where(c => c.Kind == ProbeCallKind.Delegation).Select(c => c.Scope));
    }

    /// <summary>Spec FR-002: each read uses the token its own delegation step obtained.</summary>
    [Fact]
    public async Task TheReads_UseTheIssuedToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        await client.RunAccessCheckAsync(ct);

        var reads = probe.RequestCalls.Where(c => c.Kind != ProbeCallKind.Delegation).ToList();
        Assert.Equal(2, reads.Count);
        Assert.All(reads, c => Assert.Equal(FakeGoogleAccessProbe.IssuedToken, c.Token));
    }

    /// <summary>Openapi <c>AccessCheckResult.steps</c>: eight rendered steps — six delegations with their scope, then the reads.</summary>
    [Fact]
    public async Task ThePage_RendersEightSteps_WithScopesInOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.RunAccessCheckAsync(ct);

        var steps = AccessCheckHostExtensions.StepsOf(page);
        Assert.Equal(8, steps.Count);
        Assert.Equal(
            [.. Enumerable.Repeat(AccessCheckTestData.Kind.Delegation, 6), AccessCheckTestData.Kind.ClassroomRead, AccessCheckTestData.Kind.ReportsRead],
            steps.Select(s => s.Kind));
        Assert.Equal(AccessCheckTestData.Scopes, steps.Take(6).Select(s => s.Scope));
        Assert.All(steps.Skip(6), s => Assert.True(string.IsNullOrEmpty(s.Scope)));
        Assert.All(steps, s => Assert.Equal(nameof(AccessCheckStepOutcome.Succeeded), s.Outcome));
    }

    /// <summary>The full scope URI is rendered as data — the form the super-admin pastes (US-010 I-4).</summary>
    [Theory]
    [MemberData(nameof(AccessCheckTestData.ScopeList), MemberType = typeof(AccessCheckTestData))]
    public async Task ThePage_ShowsEveryScopeAsItsFullUri(string scope)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.RunAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(scope, page.Text, StringComparison.Ordinal);
    }

    /// <summary>The reads are labelled by translated text, not by an API name.</summary>
    [Fact]
    public async Task ThePage_LabelsTheTwoReads()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.RunAccessCheckAsync(ct);

        var steps = AccessCheckHostExtensions.StepsOf(page);
        Assert.Equal(8, steps.Count);
        Assert.Contains(host.Text(AccessCheckTestData.TextKeys.StepClassroomRead, "uk"), steps[6].Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(AccessCheckTestData.TextKeys.StepReportsRead, "uk"), steps[7].Text, StringComparison.Ordinal);
    }

    /// <summary>S-07: the delegated token never reaches the browser.</summary>
    [Fact]
    public async Task TheIssuedToken_NeverReachesThePage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.RunAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.NotEmpty(probe.RequestCalls);
        Assert.DoesNotContain(FakeGoogleAccessProbe.IssuedToken, page.Body, StringComparison.Ordinal);
        Assert.All(page.SetCookies, c => Assert.DoesNotContain(FakeGoogleAccessProbe.IssuedToken, c, StringComparison.Ordinal));
    }

    /// <summary>OD-003, api-design §2.2: no Post-Redirect-Get — the answer to the POST carries the result.</summary>
    [Fact]
    public async Task TheRun_IsNotARedirect_TheResultIsInTheAnswer()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.RunAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Null(page.Location);
        Assert.NotNull(AccessCheckHostExtensions.VerdictOf(page));
    }

    /// <summary>OD-003: opening the page after a run shows no result — nothing was stored.</summary>
    [Fact]
    public async Task AfterARun_ThePageShowsNoStoredResult()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        var run = await client.RunAccessCheckAsync(ct);
        Assert.Equal(HttpStatusCode.OK, run.Status);

        var page = await client.OpenAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Null(AccessCheckHostExtensions.VerdictOf(page));
    }

    /// <summary>OD-005: repeated runs are not limited — each one runs.</summary>
    [Fact]
    public async Task TwoRunsInARow_BothRun()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var first = await client.RunAccessCheckAsync(ct);
        var second = await client.RunAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(HttpStatusCode.OK, second.Status);
        Assert.Equal(16, probe.RequestCalls.Count);
    }
}
