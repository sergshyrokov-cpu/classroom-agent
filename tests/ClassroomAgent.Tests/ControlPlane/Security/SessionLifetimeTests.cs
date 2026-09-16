using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Security;

/// <summary>AC-005, OD-002: 30 minutes sliding idle expiry and an 8-hour absolute limit.</summary>
public sealed class SessionLifetimeTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task ThirtyMinutesWithoutRequest_SessionExpires()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        host.Time.Advance(TimeSpan.FromMinutes(29));
        var withinIdleLimit = await owner.GetAsync("/", ct);
        host.Time.Advance(TimeSpan.FromMinutes(30) + TimeSpan.FromSeconds(1));
        var afterIdleLimit = await owner.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.OK, withinIdleLimit.Status);
        Assert.Equal(HttpStatusCode.Redirect, afterIdleLimit.Status);
        Assert.Equal("/sign-in", afterIdleLimit.LocationPath);
    }

    [Fact]
    public async Task EightHoursAfterSignIn_SessionExpiresDespiteActivity()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        for (var step = 1; step <= 23; step++)
        {
            host.Time.Advance(TimeSpan.FromMinutes(20));
            var active = await owner.GetAsync("/", ct);
            Assert.True(active.Status == HttpStatusCode.OK, $"Expected 200 at {step * 20} minutes, got {(int)active.Status}.");
        }

        host.Time.Advance(TimeSpan.FromMinutes(19));
        var justBeforeLimit = await owner.GetAsync("/", ct);
        host.Time.Advance(TimeSpan.FromMinutes(2));
        var pastLimit = await owner.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.OK, justBeforeLimit.Status);
        Assert.Equal(HttpStatusCode.Redirect, pastLimit.Status);
        Assert.Equal("/sign-in", pastLimit.LocationPath);
    }
}
