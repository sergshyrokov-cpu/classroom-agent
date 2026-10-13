using System.Net;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Tests.TestInfrastructure;
using Data = ClassroomAgent.Tests.TestInfrastructure.DatabaseStatisticsTestData;
using static ClassroomAgent.Tests.TestInfrastructure.SynchronizationRequestHostExtensions;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-024 AC-008, AC-016, the press of AC-009, S-05, S-07: the Database page's own press goes through the US-019 use
/// case and returns to <c>/database</c> (openapi POST /database/synchronization-requests 302), re-renders the page with
/// 409 when the connection is not usable, and is refused with the BR-025 reason in read-only mode, audited as in US-019.
/// The port is substituted, so nothing reaches Google (TC-4).
/// </summary>
public sealed class DatabaseSynchronizationRequestTests(PostgreSqlFixture database)
{
    public static TheoryData<Actor> BothRoles => new(Actor.Admin, Actor.Dean);

    /// <summary>Opens the Database page for the antiforgery token (the landing page while it does not exist yet) and posts the press.</summary>
    private static async Task<PageResponse> PressAsync(
        FormClient client,
        CancellationToken cancellationToken,
        string url = Data.PressPath,
        IEnumerable<KeyValuePair<string, string>>? fields = null)
    {
        var page = await client.GetAsync(Data.Path, cancellationToken);
        if (!Html.HasInput(page.Body, Html.AntiforgeryFieldName))
        {
            await client.GetAsync(SignInTestData.LandingPath, cancellationToken);
        }

        return await client.PostFormAsync(url, fields ?? [], cancellationToken);
    }

    private static string RoleCode(Actor actor) =>
        actor == Actor.Admin ? SynchronizationRequestTestData.Audit.RoleAdmin : SynchronizationRequestTestData.Audit.RoleDean;

    private static string ReasonKeyOf(ReadOnlyModeHost.Cause cause) => cause switch
    {
        ReadOnlyModeHost.Cause.NeverConfirmed => SignInTestData.TextKeys.RefusedNotYetConfirmed,
        ReadOnlyModeHost.Cause.Suspended => SignInTestData.TextKeys.RefusedSuspendedByOwner,
        ReadOnlyModeHost.Cause.GracePeriodExpired => SignInTestData.TextKeys.RefusedGracePeriodExpired,
        _ => throw new ArgumentOutOfRangeException(nameof(cause), cause, null),
    };

    /// <summary>AC-008, S-07: an accepted press returns to /database with no query, requests once, writes one audit row and shows the message once.</summary>
    [Theory]
    [MemberData(nameof(BothRoles))]
    public async Task AnAcceptedPress_ReturnsToTheDatabasePage_RequestsOnce_AndShowsTheMessageOnce(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, requests) = await StartAsync(database, actor, ct);
        await using var _host = host;

        var response = await PressAsync(client, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(Data.Path, response.LocationPath);
        Assert.DoesNotContain("?", response.Location, StringComparison.Ordinal);
        Assert.Equal(1, requests.Calls);
        var row = Assert.Single(await host.SynchronizationRequestAuditRowsAsync(ct));
        Assert.Equal(SynchronizationRequestTestData.Audit.Succeeded, row.Outcome);
        Assert.Equal(RoleCode(actor), row.ActorRole);
        var first = await client.GetAsync(response.LocationPath!, ct);
        var second = await client.GetAsync(response.LocationPath!, ct);
        Assert.Equal(
            host.Text(SynchronizationRequestTestData.TextKeys.Requested, "uk"),
            Html.ElementText(first.Body, Data.Markup.Message));
        Assert.Null(Html.ElementText(second.Body, Data.Markup.Message));
    }

    /// <summary>AC-008: a press during other work says the request was remembered.</summary>
    [Fact]
    public async Task APressDuringOtherWork_ShowsRequestedAfterCurrentWork()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, requests) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        requests.Timing = SynchronizationRequestTiming.AfterCurrentWork;

        var response = await PressAsync(client, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(Data.Path, response.LocationPath);
        Assert.Equal(1, requests.Calls);
        var shown = await client.GetAsync(response.LocationPath!, ct);
        Assert.Equal(
            host.Text(SynchronizationRequestTestData.TextKeys.RequestedAfterCurrentWork, "uk"),
            Html.ElementText(shown.Body, Data.Markup.Message));
    }

    /// <summary>S-07: a returnUrl in the query or in the body is ignored; the user is sent to /database, never off the site.</summary>
    [Fact]
    public async Task ASubmittedReturnUrl_IsIgnored_NoOpenRedirect()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var response = await PressAsync(
            client,
            ct,
            Data.PressPath + "?returnUrl=https%3A%2F%2Fevil.example",
            [new("returnUrl", "https://evil.example")]);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(Data.Path, response.LocationPath);
        Assert.DoesNotContain("evil.example", response.Location, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>AC-016: no usable connection — 409 on the Database page with the role's message, nothing enqueued, one refusal audit row.</summary>
    [Theory]
    [MemberData(nameof(BothRoles))]
    public async Task WithoutAUsableConnection_ThePressIs409_OnTheDatabasePage_WithTheRolesMessage(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, requests) = await StartAsync(database, actor, ct, connection: SeededConnection.None);
        await using var _host = host;
        var messageKey = actor == Actor.Admin
            ? SynchronizationRequestTestData.TextKeys.ConnectionNotUsableAdmin
            : SynchronizationRequestTestData.TextKeys.ConnectionNotUsableDean;

        var response = await PressAsync(client, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Contains(Data.Markup.CounterAttribute + "=", response.Body, StringComparison.Ordinal);
        Assert.Contains("id=\"" + Data.Markup.Form + "\"", response.Body, StringComparison.Ordinal);
        Assert.Equal(host.Text(messageKey, "uk"), Html.ElementText(response.Body, Data.Markup.Message));
        Assert.Equal(0, requests.Calls);
        var row = Assert.Single(await host.SynchronizationRequestAuditRowsAsync(ct));
        Assert.Equal(SynchronizationRequestTestData.Audit.Refused, row.Outcome);
        Assert.Equal(SynchronizationRequestTestData.Audit.ConnectionNotUsable, row.RefusalCategory);
    }

    /// <summary>AC-009, S-05: in read-only mode the press is refused with the BR-025 reason, nothing is enqueued, one refusal audit row.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_ThePressIs409_WithTheReason_AndNothingIsEnqueued(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, requests) = await StartAsync(database, Actor.Dean, ct, cause);
        await using var _host = host;

        var response = await PressAsync(client, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Contains(host.Text(ReasonKeyOf(cause), "uk"), response.Text, StringComparison.Ordinal);
        Assert.Equal(0, requests.Calls);
        var row = Assert.Single(await host.SynchronizationRequestAuditRowsAsync(ct));
        Assert.Equal(SynchronizationRequestTestData.Audit.Refused, row.Outcome);
        Assert.Equal(SynchronizationRequestTestData.Audit.ReadOnlyMode, row.RefusalCategory);
    }

    /// <summary>AC-009, S-05: a school both read-only and unconfigured answers with the read-only reason; the guard runs first.</summary>
    [Fact]
    public async Task ReadOnlyAndUnconfigured_AnswersWithTheReadOnlyReason()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, requests) = await StartAsync(
            database, Actor.Admin, ct, ReadOnlyModeHost.Cause.Suspended, SeededConnection.None);
        await using var _host = host;

        var response = await PressAsync(client, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Equal(0, requests.Calls);
        var row = Assert.Single(await host.SynchronizationRequestAuditRowsAsync(ct));
        Assert.Equal(SynchronizationRequestTestData.Audit.ReadOnlyMode, row.RefusalCategory);
        Assert.DoesNotContain(
            host.Text(SynchronizationRequestTestData.TextKeys.ConnectionNotUsableAdmin, "uk"),
            response.Text,
            StringComparison.Ordinal);
    }

    /// <summary>OD-011 (a): the US-019 endpoint is untouched and still returns each role to its own page.</summary>
    [Theory]
    [InlineData(Actor.Admin, SynchronizationRequestTestData.AdminReturnPage)]
    [InlineData(Actor.Dean, SynchronizationRequestTestData.DeanReturnPage)]
    public async Task TheUs019Endpoint_StillReturnsToTheRolesOwnPage(Actor actor, string expectedPage)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, requests) = await StartAsync(database, actor, ct);
        await using var _host = host;

        var response = await client.PressSynchronizeAsync(expectedPage, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(expectedPage, response.LocationPath);
        Assert.Equal(1, requests.Calls);
    }
}
