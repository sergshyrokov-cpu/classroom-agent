using System.Net;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.SynchronizationRequestHostExtensions;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-019 AC-001 … AC-003, AC-006: an Admin or a Dean presses "Synchronize"; the request is passed to the
/// synchronization-request port once and the user is sent back to their own page, where a one-time message says the
/// synchronization was requested (openapi POST /synchronization/requests 302; spec FR-001 … FR-005). The port is
/// substituted, so nothing reaches Google (TC-4).
/// </summary>
public sealed class SynchronizationRequestTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task AdminPress_RedirectsToTheConnectionPage_AndRequestsOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, requests) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var response = await client.PressSynchronizeAsync(SynchronizationRequestTestData.AdminReturnPage, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(SynchronizationRequestTestData.AdminReturnPage, response.LocationPath);
        Assert.Equal(1, requests.Calls);
    }

    [Fact]
    public async Task AdminPress_TheConnectionPageShowsRequested_Once()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, requests) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var response = await client.PressSynchronizeAsync(SynchronizationRequestTestData.AdminReturnPage, ct);
        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(1, requests.Calls);
        var first = await client.GetAsync(response.LocationPath!, ct);
        var second = await client.GetAsync(response.LocationPath!, ct);

        var message = host.Text(SynchronizationRequestTestData.TextKeys.Requested, "uk");
        Assert.Contains(message, first.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(message, second.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeanPress_RedirectsToTheHomePage_AndRequestsOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, requests) = await StartAsync(database, Actor.Dean, ct);
        await using var _host = host;

        var response = await client.PressSynchronizeAsync(SynchronizationRequestTestData.DeanReturnPage, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(SynchronizationRequestTestData.DeanReturnPage, response.LocationPath);
        Assert.Equal(1, requests.Calls);
    }

    /// <summary>AC-002: the Dean is told the request was made — and nothing about any run (no status, time or diagnosis).</summary>
    [Fact]
    public async Task DeanPress_TheHomePageShowsRequested_AndNothingAboutAnyRun()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, requests) = await StartAsync(database, Actor.Dean, ct);
        await using var _host = host;

        var response = await client.PressSynchronizeAsync(SynchronizationRequestTestData.DeanReturnPage, ct);
        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(1, requests.Calls);
        var home = await client.GetAsync(response.LocationPath!, ct);

        Assert.Contains(host.Text(SynchronizationRequestTestData.TextKeys.Requested, "uk"), home.Text, StringComparison.Ordinal);
        string[] lastSyncKeys =
        [
            LastSynchronizationTestData.Keys.Title,
            LastSynchronizationTestData.Keys.StatusNeverRun,
            LastSynchronizationTestData.Keys.StatusRunning,
            LastSynchronizationTestData.Keys.StatusCompleted,
            LastSynchronizationTestData.Keys.StatusFailed,
            LastSynchronizationTestData.Keys.StartedAt,
            LastSynchronizationTestData.Keys.FinishedAt,
            LastSynchronizationTestData.Keys.LastSuccess,
            LastSynchronizationTestData.Keys.LastSuccessNone,
            LastSynchronizationTestData.Keys.DiagnosisGoogleUnavailable,
            LastSynchronizationTestData.Keys.DiagnosisUnexpected,
        ];
        foreach (var key in lastSyncKeys)
        {
            Assert.DoesNotContain(host.Text(key, "uk"), home.Text, StringComparison.Ordinal);
        }
    }

    /// <summary>AC-003: another run or purge is in progress — the request is remembered and the message says so.</summary>
    [Theory]
    [InlineData(Actor.Admin)]
    [InlineData(Actor.Dean)]
    public async Task PressDuringOtherWork_ShowsRequestedAfterCurrentWork(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, requests) = await StartAsync(database, actor, ct);
        await using var _host = host;
        requests.Timing = SynchronizationRequestTiming.AfterCurrentWork;
        var page = actor == Actor.Admin
            ? SynchronizationRequestTestData.AdminReturnPage
            : SynchronizationRequestTestData.DeanReturnPage;

        var response = await client.PressSynchronizeAsync(page, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(page, response.LocationPath);
        Assert.Equal(1, requests.Calls);
        var shown = await client.GetAsync(response.LocationPath!, ct);
        Assert.Contains(
            host.Text(SynchronizationRequestTestData.TextKeys.RequestedAfterCurrentWork, "uk"),
            shown.Text,
            StringComparison.Ordinal);
    }
}
