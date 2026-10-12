using System.Net;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;
using Tl = ClassroomAgent.Tests.TestInfrastructure.TeacherListHostExtensions;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-022 AC-010: the teacher list is a read, so it works in every BR-025 cause and outside it, writes nothing and
/// never reaches Google (spec FR-013). Proved on the use case resolved from the host and over HTTP.
/// </summary>
public sealed class TeacherListReadOnlyTests(PostgreSqlFixture database)
{
    public static TheoryData<ReadOnlyModeHost.Cause> AllCauses => new(
        ReadOnlyModeHost.Cause.NeverConfirmed,
        ReadOnlyModeHost.Cause.Suspended,
        ReadOnlyModeHost.Cause.GracePeriodExpired,
        ReadOnlyModeHost.Cause.NotReadOnly);

    /// <summary>AC-010, FR-013: the use case lists the teachers in every cause, writes nothing and calls no Google.</summary>
    [Theory]
    [MemberData(nameof(AllCauses))]
    public async Task TheUseCase_ReturnsTheSeededTeachers_WritesNothing_AndCallsNoGoogle(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await InstallationTestHost.CreateAsync(database, ct);
        await using var _host = host;
        await ReadOnlyModeHost.SeedAsync(host, cause, ct);
        host.Start();
        var world = await host.SeedTeacherListAsync(ct);
        var before = await host.CourseListFingerprintAsync(ct);
        TeacherListPageResult? result = null;

        await ReadOnlyModeHost.InScopeAsync<GetTeacherListQuery>(host, async q =>
            result = await q.ExecuteAsync(new TeacherListRequest([], []), ct));

        Assert.NotNull(result);
        Assert.Equal(TeacherListPageOutcome.Shown, result.Outcome);
        Assert.NotNull(result.Page.List);
        Assert.Equal(2, result.Page.List.TotalCount);
        Assert.Equal([world.Ukrainka, world.Shevchenko], result.Page.List.Rows.Select(r => r.ParticipantId));
        Assert.Equal(before, await host.CourseListFingerprintAsync(ct));
        Assert.Equal(0, host.GoogleCallCount());
    }

    /// <summary>AC-010, FR-013: the page shows the teachers in every cause, writes nothing and calls no Google.</summary>
    [Theory]
    [MemberData(nameof(AllCauses))]
    public async Task ThePage_ShowsTheTeachers_WritesNothing_AndCallsNoGoogle(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, JournalHostExtensions.Actor.Admin, ct, cause);
        await using var _host = host;
        await host.SeedTeacherListAsync(ct);
        var before = await host.CourseListFingerprintAsync(ct);

        var page = await client.GetAsync(TeacherListTestData.Url("all"), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(Tl.ShevchenkoLabel, page.Text, StringComparison.Ordinal);
        Assert.Equal(before, await host.CourseListFingerprintAsync(ct));
        Assert.Equal(0, host.GoogleCallCount());
    }
}
