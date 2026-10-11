using System.Net;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;
using Cl = ClassroomAgent.Tests.TestInfrastructure.CourseListHostExtensions;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-020 AC-014 / AC-017: the course list is a read, so it works in every BR-025 cause and outside it, writes nothing
/// and never reaches Google (spec FR-015, S-04, S-12). Proved on the use case resolved from the host and over HTTP.
/// </summary>
public sealed class CourseListReadOnlyTests(PostgreSqlFixture database)
{
    public static TheoryData<ReadOnlyModeHost.Cause> AllCauses => new(
        ReadOnlyModeHost.Cause.NeverConfirmed,
        ReadOnlyModeHost.Cause.Suspended,
        ReadOnlyModeHost.Cause.GracePeriodExpired,
        ReadOnlyModeHost.Cause.NotReadOnly);

    [Theory]
    [MemberData(nameof(AllCauses))]
    public async Task TheUseCase_ReturnsTheSeededCourses_WritesNothing_AndCallsNoGoogle(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await InstallationTestHost.CreateAsync(database, ct);
        await using var _host = host;
        await ReadOnlyModeHost.SeedAsync(host, cause, ct);
        host.Start();
        var world = await host.SeedCourseListAsync(ct);
        var before = await host.CourseListFingerprintAsync(ct);
        CourseListPageResult? result = null;

        await ReadOnlyModeHost.InScopeAsync<GetCourseListQuery>(host, async q =>
            result = await q.ExecuteAsync(new CourseListRequest([], [], [], []), ct));

        Assert.NotNull(result);
        Assert.Equal(CourseListPageOutcome.Shown, result.Outcome);
        Assert.NotNull(result.Page.List);
        Assert.Equal(2, result.Page.List.TotalCount);
        Assert.Equal([world.Physics, world.Chemistry], result.Page.List.Rows.Select(r => r.CourseId));
        Assert.Equal(before, await host.CourseListFingerprintAsync(ct));
        Assert.Equal(0, host.GoogleCallCount());
    }

    [Theory]
    [MemberData(nameof(AllCauses))]
    public async Task ThePage_ShowsTheCourses_WritesNothing_AndCallsNoGoogle(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, JournalHostExtensions.Actor.Admin, ct, cause);
        await using var _host = host;
        await host.SeedCourseListAsync(ct);
        var before = await host.CourseListFingerprintAsync(ct);

        var page = await client.GetAsync(CourseListTestData.Url(status: "all", q: "ф"), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(Cl.PhysicsName, page.Text, StringComparison.Ordinal);
        Assert.Equal(before, await host.CourseListFingerprintAsync(ct));
        Assert.Equal(0, host.GoogleCallCount());
    }
}
