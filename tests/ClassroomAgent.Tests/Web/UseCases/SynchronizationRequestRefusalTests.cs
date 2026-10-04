using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.SynchronizationRequestHostExtensions;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-019 AC-005, AC-009: the refusals of openapi POST 409. In read-only mode — any BR-025 cause — nothing is enqueued;
/// without a usable connection there is nothing to synchronize. Both are audited as refused, and the read-only guard
/// runs first (spec FR-006 … FR-009; S-03; TC-5).
/// </summary>
public sealed class SynchronizationRequestRefusalTests(PostgreSqlFixture database)
{
    public static TheoryData<ReadOnlyModeHost.Cause, Actor> CausesByActor
    {
        get
        {
            var data = new TheoryData<ReadOnlyModeHost.Cause, Actor>();
            foreach (var cause in new[]
            {
                ReadOnlyModeHost.Cause.NeverConfirmed,
                ReadOnlyModeHost.Cause.Suspended,
                ReadOnlyModeHost.Cause.GracePeriodExpired,
            })
            {
                data.Add(cause, Actor.Admin);
                data.Add(cause, Actor.Dean);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(CausesByActor))]
    public async Task InReadOnlyMode_ThePressIsRefusedWith409_AndNothingIsEnqueued(ReadOnlyModeHost.Cause cause, Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, requests) = await StartAsync(database, actor, ct, cause);
        await using var _host = host;

        var response = await client.PressSynchronizeAsync(PageOf(actor), ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Equal(0, requests.Calls);
    }

    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheRefusalNamesTheReason(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartAsync(database, Actor.Admin, ct, cause);
        await using var _host = host;

        var response = await client.PressSynchronizeAsync(SynchronizationRequestTestData.AdminReturnPage, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Contains(host.Text(ReasonKeyOf(cause), "uk"), response.Text, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheRefusalIsAudited(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartAsync(database, Actor.Admin, ct, cause);
        await using var _host = host;

        var response = await client.PressSynchronizeAsync(SynchronizationRequestTestData.AdminReturnPage, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        var row = Assert.Single(await host.SynchronizationRequestAuditRowsAsync(ct));
        Assert.Equal(SynchronizationRequestTestData.Audit.Refused, row.Outcome);
        Assert.Equal(SynchronizationRequestTestData.Audit.ReadOnlyMode, row.RefusalCategory);
        Assert.Null(row.TargetType);
        Assert.Null(row.TargetId);
    }

    /// <summary>A school both read-only and unconfigured answers with the read-only reason: the guard runs first.</summary>
    [Fact]
    public async Task ReadOnlyAndUnconfigured_AnswersWithTheReadOnlyReason()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, requests) = await StartAsync(
            database,
            Actor.Admin,
            ct,
            ReadOnlyModeHost.Cause.Suspended,
            SeededConnection.None);
        await using var _host = host;

        var response = await client.PressSynchronizeAsync(SynchronizationRequestTestData.AdminReturnPage, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Equal(0, requests.Calls);
        var row = Assert.Single(await host.SynchronizationRequestAuditRowsAsync(ct));
        Assert.Equal(SynchronizationRequestTestData.Audit.ReadOnlyMode, row.RefusalCategory);
        Assert.DoesNotContain(
            host.Text(SynchronizationRequestTestData.TextKeys.ConnectionNotUsableAdmin, "uk"),
            response.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutAConnection_AnAdminPressIsRefused_WithTheAdminMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, requests) = await StartAsync(database, Actor.Admin, ct, connection: SeededConnection.None);
        await using var _host = host;

        var response = await client.PressSynchronizeAsync(SynchronizationRequestTestData.AdminReturnPage, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Equal(0, requests.Calls);
        Assert.Contains(
            host.Text(SynchronizationRequestTestData.TextKeys.ConnectionNotUsableAdmin, "uk"),
            response.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutAConnection_ADeanPressIsRefused_WithTheDeanMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, requests) = await StartAsync(database, Actor.Dean, ct, connection: SeededConnection.None);
        await using var _host = host;

        var response = await client.PressSynchronizeAsync(SynchronizationRequestTestData.DeanReturnPage, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Equal(0, requests.Calls);
        Assert.Contains(
            host.Text(SynchronizationRequestTestData.TextKeys.ConnectionNotUsableDean, "uk"),
            response.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithAConnectionForAnotherDomain_ThePressIsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, requests) = await StartAsync(
            database,
            Actor.Admin,
            ct,
            connection: SeededConnection.ForAnotherDomain);
        await using var _host = host;

        var response = await client.PressSynchronizeAsync(SynchronizationRequestTestData.AdminReturnPage, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Equal(0, requests.Calls);
    }

    [Fact]
    public async Task WithoutAConnection_TheRefusalIsAudited()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await StartAsync(database, Actor.Admin, ct, connection: SeededConnection.None);
        await using var _host = host;

        await client.PressSynchronizeAsync(SynchronizationRequestTestData.AdminReturnPage, ct);

        var row = Assert.Single(await host.SynchronizationRequestAuditRowsAsync(ct));
        Assert.Equal(SynchronizationRequestTestData.Audit.Refused, row.Outcome);
        Assert.Equal(SynchronizationRequestTestData.Audit.ConnectionNotUsable, row.RefusalCategory);
        Assert.Null(row.TargetType);
        Assert.Null(row.TargetId);
    }

    private static string PageOf(Actor actor) =>
        actor == Actor.Admin
            ? SynchronizationRequestTestData.AdminReturnPage
            : SynchronizationRequestTestData.DeanReturnPage;

    private static string ReasonKeyOf(ReadOnlyModeHost.Cause cause) => cause switch
    {
        ReadOnlyModeHost.Cause.NeverConfirmed => SignInTestData.TextKeys.RefusedNotYetConfirmed,
        ReadOnlyModeHost.Cause.Suspended => SignInTestData.TextKeys.RefusedSuspendedByOwner,
        ReadOnlyModeHost.Cause.GracePeriodExpired => SignInTestData.TextKeys.RefusedGracePeriodExpired,
        _ => throw new ArgumentOutOfRangeException(nameof(cause), cause, null),
    };
}
