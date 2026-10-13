using System.Net;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Data = ClassroomAgent.Tests.TestInfrastructure.DatabaseStatisticsTestData;
using Expected = ClassroomAgent.Tests.TestInfrastructure.DatabaseStatisticsHostExtensions.Expected;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-024 AC-009, AC-012, S-04: the Database page is a read, so it works in every BR-025 cause and outside it, writes
/// nothing and never reaches Google (spec FR-008). Proved on the use case resolved from the host and over HTTP.
/// </summary>
public sealed class DatabaseStatisticsReadOnlyTests(PostgreSqlFixture database)
{
    public static TheoryData<ReadOnlyModeHost.Cause> AllCauses => new(
        ReadOnlyModeHost.Cause.NeverConfirmed,
        ReadOnlyModeHost.Cause.Suspended,
        ReadOnlyModeHost.Cause.GracePeriodExpired,
        ReadOnlyModeHost.Cause.NotReadOnly);

    /// <summary>AC-009, AC-012, FR-008: the use case counts the seeded school in every cause, writes nothing and calls no Google.</summary>
    [Theory]
    [MemberData(nameof(AllCauses))]
    public async Task TheUseCase_ReturnsTheSeededCounters_WritesNothing_AndCallsNoGoogle(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await InstallationTestHost.CreateAsync(database, ct);
        await using var _host = host;
        await ReadOnlyModeHost.SeedAsync(host, cause, ct);
        host.Start();
        await host.SeedStatisticsWorldAsync(ct);
        var before = await host.DatabaseStatisticsFingerprintAsync(ct);
        DatabaseStatisticsView? view = null;

        await ReadOnlyModeHost.InScopeAsync<GetDatabaseStatisticsQuery>(host, async q =>
            view = await q.ExecuteAsync(AppRole.Dean, ct));

        Assert.NotNull(view);
        Assert.Equal(Expected.Total, view.Courses.Total);
        Assert.Equal(Expected.Teachers, view.Teachers);
        Assert.Equal(Expected.MeetCodesAwaitingDecision, view.MeetCodesAwaitingDecision);
        Assert.Equal(before, await host.DatabaseStatisticsFingerprintAsync(ct));
        Assert.Equal(0, host.GoogleCallCount());
    }

    /// <summary>AC-009, AC-012, FR-008: the page shows the counters and the form in every cause, writes nothing and calls no Google.</summary>
    [Theory]
    [MemberData(nameof(AllCauses))]
    public async Task ThePage_ShowsTheCountersAndTheForm_WritesNothing_AndCallsNoGoogle(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct, cause);
        await using var _host = host;
        await host.SeedStatisticsWorldAsync(ct);
        var before = await host.DatabaseStatisticsFingerprintAsync(ct);

        var page = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(Expected.Total, Data.Counter(page.Body, Data.Counters.Total));
        Assert.Equal(Expected.Teachers, Data.Counter(page.Body, Data.Counters.Teachers));
        Assert.Contains("id=\"" + Data.Markup.Form + "\"", page.Body, StringComparison.Ordinal);
        Assert.Equal(before, await host.DatabaseStatisticsFingerprintAsync(ct));
        Assert.Equal(0, host.GoogleCallCount());
    }
}
