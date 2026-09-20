using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-008 AC-013: the installation session ends after 60 minutes of inactivity and in any case 8 hours after
/// the sign-in, with no "remember me". Both limits run on the injectable clock, so nothing waits
/// (spec FR-015; NFR-072; TC-5).
/// </summary>
public sealed class InstallationSessionLifetimeTests(PostgreSqlFixture database)
{
    private static ScriptedHttpHandler Allowed() =>
        ScriptedHttpHandler.AdminLoginCheckJson(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(true));

    [Fact]
    public async Task SixtyMinutesWithoutARequest_TheSessionExpires()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        var (client, _) = await host.SignInWithGoogleAsync(ct);

        host.Time.Advance(TimeSpan.FromMinutes(59));
        var withinLimit = await client.GetAsync(SignInTestData.LandingPath, ct);
        host.Time.Advance(TimeSpan.FromMinutes(60) + TimeSpan.FromSeconds(1));
        var pastLimit = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(HttpStatusCode.OK, withinLimit.Status);
        Assert.Equal(HttpStatusCode.Redirect, pastLimit.Status);
        Assert.Equal(SignInTestData.SignInPath, pastLimit.LocationPath);
    }

    [Fact]
    public async Task EightHoursAfterSignIn_TheSessionExpiresDespiteActivity()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        var (client, _) = await host.SignInWithGoogleAsync(ct);

        for (var step = 1; step <= 23; step++)
        {
            host.Time.Advance(TimeSpan.FromMinutes(20));
            var active = await client.GetAsync(SignInTestData.LandingPath, ct);
            Assert.True(active.Status == HttpStatusCode.OK, $"Expected 200 at {step * 20} minutes, got {(int)active.Status}.");
        }

        host.Time.Advance(TimeSpan.FromMinutes(19));
        var justBefore = await client.GetAsync(SignInTestData.LandingPath, ct);
        host.Time.Advance(TimeSpan.FromMinutes(2));
        var past = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(HttpStatusCode.OK, justBefore.Status);
        Assert.Equal(HttpStatusCode.Redirect, past.Status);
        Assert.Equal(SignInTestData.SignInPath, past.LocationPath);
    }

    /// <summary>AC-013: the idle limit slides — activity keeps the session alive inside the 8-hour ceiling.</summary>
    [Fact]
    public async Task ActivityWithinTheIdleLimit_KeepsTheSessionAlive()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        var (client, _) = await host.SignInWithGoogleAsync(ct);

        for (var step = 1; step <= 6; step++)
        {
            host.Time.Advance(TimeSpan.FromMinutes(50));
            var response = await client.GetAsync(SignInTestData.LandingPath, ct);
            Assert.True(response.Status == HttpStatusCode.OK, $"Expected 200 at step {step}, got {(int)response.Status}.");
        }
    }
}
