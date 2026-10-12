using System.Net;
using ClassroomAgent.Application.Authorization;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Authorization;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Cl = ClassroomAgent.Tests.TestInfrastructure.CourseListHostExtensions;
using Data = ClassroomAgent.Tests.TestInfrastructure.TeacherListTestData;
using Tl = ClassroomAgent.Tests.TestInfrastructure.TeacherListHostExtensions;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-022 AC-009, spec FR-012, S-01, S-02, TC-5: the teacher list is open to an Admin and a Dean, sends an anonymous
/// visitor to sign-in and a Dean on the forced temporary-password session to the password change. Both roles are
/// allowed, so the forbidden case is the unauthenticated request. The allowed case asserts the page's own 200, so a
/// missing route cannot pass it.
/// </summary>
public sealed class TeacherListAuthorizationTests(PostgreSqlFixture database)
{
    private const string ChangePasswordPath = "/sign-in/change-password";

    public static TheoryData<Actor> AllowedActors => new(Actor.Admin, Actor.Dean);

    /// <summary>AC-009, FR-012: an Admin and a Dean see the list with its data.</summary>
    [Theory]
    [MemberData(nameof(AllowedActors))]
    public async Task AnAdminAndADean_CanOpenTheTeacherList(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        await host.SeedTeacherListAsync(ct);

        var response = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Contains(Tl.ShevchenkoLabel, response.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-009, S-01: an anonymous visitor is sent to sign-in and nothing is shown or written.</summary>
    [Fact]
    public async Task Anonymous_IsSentToSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Anonymous, ct);
        await using var _host = host;
        await host.SeedTeacherListAsync(ct);
        var before = await host.CourseListFingerprintAsync(ct);

        var response = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(SignInTestData.SignInPath, response.LocationPath);
        Assert.DoesNotContain(Tl.ShevchenkoLabel, response.Text, StringComparison.Ordinal);
        Assert.Equal(before, await host.CourseListFingerprintAsync(ct));
    }

    /// <summary>AC-009, S-02, US-012: a Dean with a temporary password is sent to change it.</summary>
    [Fact]
    public async Task ADeanWithATemporaryPassword_IsSentToTheForcedChange()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.DeanWithTemporaryPassword, ct);
        await using var _host = host;
        await host.SeedTeacherListAsync(ct);

        var response = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(ChangePasswordPath, response.LocationPath);
        Assert.DoesNotContain(Tl.ShevchenkoLabel, response.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-009, FR-012: the route exists, is not anonymous, and declares the ViewCourses policy.</summary>
    [Fact]
    public async Task ThePath_IsAProtectedEndpoint_WithTheViewCoursesPolicy()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var endpoints = HostEndpoint.All(host.Services).Where(e => e.Pattern == Data.Pattern).ToList();

        var endpoint = Assert.Single(endpoints);
        Assert.False(endpoint.AllowsAnonymous);
        Assert.False(endpoint.IsFallback);
        Assert.Contains(
            endpoint.Endpoint.Metadata.OfType<IAuthorizeData>(),
            a => string.Equals(a.Policy, InstallationPolicies.ViewCourses, StringComparison.Ordinal));
    }
}
