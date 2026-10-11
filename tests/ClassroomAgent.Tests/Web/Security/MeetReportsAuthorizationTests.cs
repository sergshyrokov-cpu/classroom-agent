using System.Globalization;
using System.Net;
using ClassroomAgent.Application.Authorization;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Mr = ClassroomAgent.Tests.TestInfrastructure.MeetReportsHostExtensions;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-033 AC-014, spec §7, TC-5, api-design §4: both pages are open to an Admin and a Dean, send an anonymous visitor to
/// sign-in and a Dean on the forced temporary-password session to the password change. Both roles are allowed, so the
/// forbidden case is the unauthenticated request. The allowed case asserts the page's own 200, so a missing route
/// cannot pass it. The endpoint enumeration of US-008 must classify both paths as protected.
/// </summary>
public sealed class MeetReportsAuthorizationTests(PostgreSqlFixture database)
{
    private const string ChangePasswordPath = "/sign-in/change-password";

    private const string ReportsPattern = "workspace/meet-reports";

    private const string MeetingPattern = "workspace/meet-reports/meetings/{meetingid}";

    public static TheoryData<string> Pages => new("reports", "meeting");

    public static TheoryData<Actor, string> AllowedCases
    {
        get
        {
            var data = new TheoryData<Actor, string>();
            foreach (var actor in new[] { Actor.Admin, Actor.Dean })
            {
                data.Add(actor, "reports");
                data.Add(actor, "meeting");
            }

            return data;
        }
    }

    private static string PathOf(string page, Mr.SeededWorld world) => page switch
    {
        "reports" => MeetReportsTestData.OctoberUrl(world.CourseId),
        "meeting" => MeetReportsTestData.MeetingUrl(
            world.MeetingA.ToString(CultureInfo.InvariantCulture), world.CourseId,
            MeetReportsTestData.Period.FromText, MeetReportsTestData.Period.ToText),
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, null),
    };

    /// <summary>AC-014, FR-001: an Admin and a Dean see both pages with their data.</summary>
    [Theory]
    [MemberData(nameof(AllowedCases))]
    public async Task AnAdminAndADean_CanOpenBothPages(Actor actor, string page)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var response = await client.GetAsync(PathOf(page, world), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Contains(Mr.StudentOneName, response.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-014: an anonymous visitor is sent to sign-in and nothing is shown or written.</summary>
    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Anonymous_IsSentToSignIn_OnBothPages(string page)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Anonymous, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        var before = await host.WriteFingerprintAsync(ct);

        var response = await client.GetAsync(PathOf(page, world), ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(SignInTestData.SignInPath, response.LocationPath);
        Assert.DoesNotContain(Mr.StudentOneName, response.Text, StringComparison.Ordinal);
        Assert.Equal(before, await host.WriteFingerprintAsync(ct));
    }

    /// <summary>AC-014, US-012: a Dean with a temporary password is sent to change it, on both pages.</summary>
    [Theory]
    [MemberData(nameof(Pages))]
    public async Task ADeanWithATemporaryPassword_IsSentToTheForcedChange_OnBothPages(string page)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.DeanWithTemporaryPassword, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var response = await client.GetAsync(PathOf(page, world), ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(ChangePasswordPath, response.LocationPath);
        Assert.DoesNotContain(Mr.StudentOneName, response.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-014, api-design §4: both routes exist, are not anonymous, and declare the ViewMeetReports policy.</summary>
    [Theory]
    [InlineData(ReportsPattern)]
    [InlineData(MeetingPattern)]
    public async Task BothPaths_AreProtectedEndpoints_WithTheViewMeetReportsPolicy(string pattern)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var endpoints = HostEndpoint.All(host.Services).Where(e => e.Pattern == pattern).ToList();

        var endpoint = Assert.Single(endpoints);
        Assert.False(endpoint.AllowsAnonymous);
        Assert.False(endpoint.IsFallback);
        Assert.Contains(
            endpoint.Endpoint.Metadata.OfType<IAuthorizeData>(),
            a => string.Equals(a.Policy, InstallationPolicies.ViewMeetReports, StringComparison.Ordinal));
    }

    /// <summary>AC-014: the policy is registered, so the attribute cannot name a policy that does not exist.</summary>
    [Fact]
    public async Task TheViewMeetReportsPolicy_IsRegistered()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var provider = host.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        var policy = await provider.GetPolicyAsync(InstallationPolicies.ViewMeetReports);
        var control = await provider.GetPolicyAsync("NoSuchPolicyForTheControl");

        Assert.Null(control);
        Assert.NotNull(policy);
    }
}
