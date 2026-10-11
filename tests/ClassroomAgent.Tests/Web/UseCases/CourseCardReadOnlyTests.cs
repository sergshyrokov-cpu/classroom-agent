using System.Net;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;
using Cc = ClassroomAgent.Tests.TestInfrastructure.CourseCardHostExtensions;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-021 AC-010, spec FR-014: the course card is a read, so it works in every BR-025 cause and outside it, writes nothing
/// and never reaches Google. Proved on the use case resolved from the host and over HTTP.
/// </summary>
public sealed class CourseCardReadOnlyTests(PostgreSqlFixture database)
{
    public static TheoryData<ReadOnlyModeHost.Cause> AllCauses => new(
        ReadOnlyModeHost.Cause.NeverConfirmed,
        ReadOnlyModeHost.Cause.Suspended,
        ReadOnlyModeHost.Cause.GracePeriodExpired,
        ReadOnlyModeHost.Cause.NotReadOnly);

    [Theory]
    [MemberData(nameof(AllCauses))]
    public async Task TheUseCase_ReturnsTheCard_WritesNothing_AndCallsNoGoogle(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await InstallationTestHost.CreateAsync(database, ct);
        await using var _host = host;
        await ReadOnlyModeHost.SeedAsync(host, cause, ct);
        host.Start();
        var world = await host.SeedCourseCardAsync(ct);
        var before = await host.CourseListFingerprintAsync(ct);
        CourseCardResult? result = null;

        await ReadOnlyModeHost.InScopeAsync<GetCourseCardQuery>(host, async q =>
            result = await q.ExecuteAsync(new CourseCardRequest(CourseCardTestData.Id(world.Physics)), ct));

        Assert.NotNull(result);
        Assert.Equal(CourseCardOutcome.Shown, result.Outcome);
        Assert.NotNull(result.Page);
        Assert.Equal(2, result.Page.Teachers.Count);
        Assert.Equal(7, result.Page.Students.Count);
        Assert.Equal(3, result.Page.Leavers.Count);
        Assert.Equal(before, await host.CourseListFingerprintAsync(ct));
        Assert.Equal(0, host.GoogleCallCount());
    }

    [Theory]
    [MemberData(nameof(AllCauses))]
    public async Task ThePage_ShowsTheCard_WritesNothing_AndCallsNoGoogle(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, JournalHostExtensions.Actor.Admin, ct, cause);
        await using var _host = host;
        var world = await host.SeedCourseCardAsync(ct);
        var before = await host.CourseListFingerprintAsync(ct);

        var page = await client.GetAsync(CourseCardTestData.Path(world.Physics), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(Cc.PhysicsName, page.Text, StringComparison.Ordinal);
        Assert.Equal(before, await host.CourseListFingerprintAsync(ct));
        Assert.Equal(0, host.GoogleCallCount());
    }
}
