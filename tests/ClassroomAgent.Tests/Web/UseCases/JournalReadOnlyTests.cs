using System.Globalization;
using System.Net;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-025 AC-009 / AC-013: the journal is a read, so it works in every BR-025 cause and outside it, writes nothing
/// and never reaches Google (spec FR-013). Proved on the use case resolved from the host and over HTTP.
/// </summary>
public sealed class JournalReadOnlyTests(PostgreSqlFixture database)
{
    public static TheoryData<ReadOnlyModeHost.Cause> AllCauses => new(
        ReadOnlyModeHost.Cause.NeverConfirmed,
        ReadOnlyModeHost.Cause.Suspended,
        ReadOnlyModeHost.Cause.GracePeriodExpired,
        ReadOnlyModeHost.Cause.NotReadOnly);

    [Theory]
    [MemberData(nameof(AllCauses))]
    public async Task TheUseCase_ReturnsTheSeededJournal_WritesNothing_AndCallsNoGoogle(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await InstallationTestHost.CreateAsync(database, ct);
        await using var _host = host;
        await ReadOnlyModeHost.SeedAsync(host, cause, ct);
        host.Start();
        var seeded = await host.SeedJournalAsync(ct);
        var before = await host.TeachingFingerprintAsync(ct);
        JournalPageResult? result = null;

        await ReadOnlyModeHost.InScopeAsync<GetJournalQuery>(host, async q =>
            result = await q.ExecuteAsync(
                new JournalRequest(
                    [seeded.CourseId.ToString(CultureInfo.InvariantCulture)],
                    [JournalTestData.Period.FromText],
                    [JournalTestData.Period.ToText],
                    []),
                CultureInfo.GetCultureInfo("uk"),
                ct));

        Assert.NotNull(result);
        Assert.Equal(JournalPageOutcome.Shown, result.Outcome);
        Assert.NotNull(result.Page.Journal);
        Assert.Equal(3, result.Page.Journal.Columns.Count);
        Assert.Equal(2, result.Page.Journal.Rows.Count);
        Assert.Equal(before, await host.TeachingFingerprintAsync(ct));
        Assert.Empty(host.Classroom.ImpersonatedAs);
    }

    [Theory]
    [MemberData(nameof(AllCauses))]
    public async Task ThePage_ShowsTheJournal_WritesNothing_AndCallsNoGoogle(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, JournalHostExtensions.Actor.Admin, ct, cause);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);
        var before = await host.TeachingFingerprintAsync(ct);

        var page = await client.GetAsync(JournalTestData.SeptemberUrl(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(SeededJournal.StudentName, page.Text, StringComparison.Ordinal);
        Assert.Equal(before, await host.TeachingFingerprintAsync(ct));
        Assert.Empty(host.Classroom.ImpersonatedAs);
    }
}
