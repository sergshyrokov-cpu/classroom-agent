using System.Net;
using ClassroomAgent.Application.Authorization;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Cl = ClassroomAgent.Tests.TestInfrastructure.CourseListHostExtensions;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-020 AC-015, spec FR-014, S-01, S-02, TC-5: the course list is open to an Admin and a Dean, sends an anonymous
/// visitor to sign-in and a Dean on the forced temporary-password session to the password change. Both roles are
/// allowed, so the forbidden case is the unauthenticated request. The allowed case asserts the page's own 200, so a
/// missing route cannot pass it.
/// </summary>
public sealed class CourseListAuthorizationTests(PostgreSqlFixture database)
{
    private const string ChangePasswordPath = "/sign-in/change-password";

    private const string CoursesPattern = "workspace/courses";

    public static TheoryData<Actor> AllowedActors => new(Actor.Admin, Actor.Dean);

    /// <summary>AC-015, FR-014: an Admin and a Dean see the list with its data.</summary>
    [Theory]
    [MemberData(nameof(AllowedActors))]
    public async Task AnAdminAndADean_CanOpenTheCourseList(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        await host.SeedCourseListAsync(ct);

        var response = await client.GetAsync(CourseListTestData.Path, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Contains(Cl.PhysicsName, response.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-015, S-01: an anonymous visitor is sent to sign-in and nothing is shown or written.</summary>
    [Fact]
    public async Task Anonymous_IsSentToSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Anonymous, ct);
        await using var _host = host;
        await host.SeedCourseListAsync(ct);
        var before = await host.CourseListFingerprintAsync(ct);

        var response = await client.GetAsync(CourseListTestData.Path, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(SignInTestData.SignInPath, response.LocationPath);
        Assert.DoesNotContain(Cl.PhysicsName, response.Text, StringComparison.Ordinal);
        Assert.Equal(before, await host.CourseListFingerprintAsync(ct));
    }

    /// <summary>AC-015, S-02, US-012: a Dean with a temporary password is sent to change it.</summary>
    [Fact]
    public async Task ADeanWithATemporaryPassword_IsSentToTheForcedChange()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.DeanWithTemporaryPassword, ct);
        await using var _host = host;
        await host.SeedCourseListAsync(ct);

        var response = await client.GetAsync(CourseListTestData.Path, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(ChangePasswordPath, response.LocationPath);
        Assert.DoesNotContain(Cl.PhysicsName, response.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-015, FR-014: the route exists, is not anonymous, and declares the ViewCourses policy.</summary>
    [Fact]
    public async Task ThePath_IsAProtectedEndpoint_WithTheViewCoursesPolicy()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var endpoints = HostEndpoint.All(host.Services).Where(e => e.Pattern == CoursesPattern).ToList();

        var endpoint = Assert.Single(endpoints);
        Assert.False(endpoint.AllowsAnonymous);
        Assert.False(endpoint.IsFallback);
        Assert.Contains(
            endpoint.Endpoint.Metadata.OfType<IAuthorizeData>(),
            a => string.Equals(a.Policy, InstallationPolicies.ViewCourses, StringComparison.Ordinal));
    }

    /// <summary>AC-015: the policy is registered, so the attribute cannot name a policy that does not exist.</summary>
    [Fact]
    public async Task TheViewCoursesPolicy_IsRegistered()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var provider = host.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        var policy = await provider.GetPolicyAsync(InstallationPolicies.ViewCourses);
        var control = await provider.GetPolicyAsync("NoSuchPolicyForTheControl");

        Assert.Null(control);
        Assert.NotNull(policy);
    }
}
