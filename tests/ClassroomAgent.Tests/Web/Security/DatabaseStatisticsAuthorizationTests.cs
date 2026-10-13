using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Authorization;
using Data = ClassroomAgent.Tests.TestInfrastructure.DatabaseStatisticsTestData;
using Expected = ClassroomAgent.Tests.TestInfrastructure.DatabaseStatisticsHostExtensions.Expected;
using JournalActor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Sr = ClassroomAgent.Tests.TestInfrastructure.SynchronizationRequestHostExtensions;
using SrActor = ClassroomAgent.Tests.TestInfrastructure.SynchronizationRequestHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-024 AC-010, spec FR-007, FR-010, S-01, S-02, S-06, TC-5: the page is open to an Admin and a Dean, sends an
/// anonymous visitor to sign-in and a Dean on the forced temporary-password session to the password change; the press
/// does the same and needs the antiforgery token. Both roles are allowed, so the forbidden cases are the unauthenticated
/// request and the restricted session. The allowed case asserts the page's own 200 with a counter, so a missing route
/// cannot pass it.
/// </summary>
public sealed class DatabaseStatisticsAuthorizationTests(PostgreSqlFixture database)
{
    private const string ChangePasswordPath = "/sign-in/change-password";

    public static TheoryData<JournalActor> AllowedJournalActors => new(JournalActor.Admin, JournalActor.Dean);

    public static TheoryData<SrActor> AllowedActors => new(SrActor.Admin, SrActor.Dean);

    /// <summary>
    /// Opens the Database page for the antiforgery token (the landing page while the page does not exist yet, the
    /// US-019 pattern) and posts the press.
    /// </summary>
    private static async Task<PageResponse> PressAsync(
        FormClient client,
        CancellationToken cancellationToken,
        bool withToken = true)
    {
        var page = await client.GetAsync(Data.Path, cancellationToken);
        if (withToken && !Html.HasInput(page.Body, Html.AntiforgeryFieldName))
        {
            await client.GetAsync(SignInTestData.LandingPath, cancellationToken);
        }

        return await client.PostFormAsync(Data.PressPath, [], cancellationToken, withToken);
    }

    /// <summary>AC-010, FR-010: an Admin and a Dean see the page with its data.</summary>
    [Theory]
    [MemberData(nameof(AllowedJournalActors))]
    public async Task AnAdminAndADean_CanOpenTheDatabasePage(JournalActor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        await host.SeedStatisticsWorldAsync(ct);

        var response = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(Expected.Teachers, Data.Counter(response.Body, Data.Counters.Teachers));
    }

    /// <summary>AC-010, S-01: an anonymous visitor is sent to sign-in and nothing is shown or written.</summary>
    [Fact]
    public async Task Anonymous_IsSentToSignIn_ForTheGet()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, JournalActor.Anonymous, ct);
        await using var _host = host;
        await host.SeedStatisticsWorldAsync(ct);
        var before = await host.DatabaseStatisticsFingerprintAsync(ct);

        var response = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(SignInTestData.SignInPath, response.LocationPath);
        Assert.DoesNotContain(Data.Markup.CounterAttribute, response.Body, StringComparison.Ordinal);
        Assert.Equal(before, await host.DatabaseStatisticsFingerprintAsync(ct));
    }

    /// <summary>AC-010, S-02, US-012: a Dean with a temporary password is sent to change it.</summary>
    [Fact]
    public async Task ADeanWithATemporaryPassword_IsSentToTheForcedChange_ForTheGet()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, JournalActor.DeanWithTemporaryPassword, ct);
        await using var _host = host;
        await host.SeedStatisticsWorldAsync(ct);

        var response = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(ChangePasswordPath, response.LocationPath);
        Assert.DoesNotContain(Data.Markup.CounterAttribute, response.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-010, S-01: an anonymous press is sent to sign-in, requests nothing and writes no audit row.</summary>
    [Fact]
    public async Task Anonymous_IsSentToSignIn_ForThePress()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, requests) = await Sr.StartAsync(database, SrActor.Anonymous, ct);
        await using var _host = host;
        // The sign-in page supplies a token, so the refusal under test is the authorization one, not the antiforgery one.
        await client.GetAsync(SignInTestData.SignInPath, ct);

        var response = await client.PostFormAsync(Data.PressPath, [], ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(SignInTestData.SignInPath, response.LocationPath);
        Assert.Equal(0, requests.Calls);
        Assert.Empty(await host.SynchronizationRequestAuditRowsAsync(ct));
    }

    /// <summary>AC-010, S-02: a Dean with a temporary password is sent to change it; the press requests nothing.</summary>
    [Fact]
    public async Task ADeanWithATemporaryPassword_IsSentToTheForcedChange_ForThePress()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, requests) = await Sr.StartAsync(database, SrActor.DeanWithTemporaryPassword, ct);
        await using var _host = host;

        var response = await PressAsync(client, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(ChangePasswordPath, response.LocationPath);
        Assert.Equal(0, requests.Calls);
        Assert.Empty(await host.SynchronizationRequestAuditRowsAsync(ct));
    }

    /// <summary>AC-010, S-06: a press without the antiforgery token is 400 and requests nothing.</summary>
    [Theory]
    [MemberData(nameof(AllowedActors))]
    public async Task WithoutAnAntiforgeryToken_ThePressIs400_AndNothingHappens(SrActor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, requests) = await Sr.StartAsync(database, actor, ct);
        await using var _host = host;

        var response = await PressAsync(client, ct, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal(0, requests.Calls);
        Assert.Empty(await host.SynchronizationRequestAuditRowsAsync(ct));
    }

    /// <summary>AC-010, FR-010, FR-007: each route exists once, is not anonymous, and declares its policy.</summary>
    [Fact]
    public async Task TheTwoPaths_AreProtectedEndpoints_WithTheirPolicies()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var endpoints = HostEndpoint.All(host.Services);

        var page = Assert.Single(endpoints, e => e.Pattern == Data.Pattern);
        Assert.False(page.AllowsAnonymous);
        Assert.False(page.IsFallback);
        Assert.Contains(
            page.Endpoint.Metadata.OfType<IAuthorizeData>(),
            a => string.Equals(a.Policy, Data.Policy, StringComparison.Ordinal));
        var press = Assert.Single(endpoints, e => e.Pattern == Data.PressPattern);
        Assert.False(press.AllowsAnonymous);
        Assert.False(press.IsFallback);
        Assert.Contains(
            press.Endpoint.Metadata.OfType<IAuthorizeData>(),
            a => string.Equals(a.Policy, SynchronizationRequestTestData.Policy, StringComparison.Ordinal));
    }
}
