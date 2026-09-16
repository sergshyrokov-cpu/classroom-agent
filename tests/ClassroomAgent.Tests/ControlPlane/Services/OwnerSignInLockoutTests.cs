using ClassroomAgent.ControlPlane.Services;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.ControlPlane.Services;

/// <summary>AC-005: the SC-2 lockout of the Owner sign-in service (FR-008, FR-009, spec I-4).</summary>
public sealed class OwnerSignInLockoutTests(PostgreSqlFixture database)
{
    private const string WrongPassword = "this is not the password";

    private static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);

    [Fact]
    public async Task FourFailures_ThenCorrectPassword_SignsIn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWithOwnerAsync(ct);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            Assert.Equal(OwnerSignInOutcome.Refused, (await SignInAsync(host, WrongPassword, ct)).Outcome);
        }

        var result = await SignInAsync(host, TestData.Password, ct);

        Assert.Equal(OwnerSignInOutcome.SignedIn, result.Outcome);
        Assert.NotNull(result.Session);
        var owner = await host.OwnerAsync(ct);
        Assert.Equal(0, owner!.AccessFailedCount);
        Assert.True(owner.LockoutEnd is null || owner.LockoutEnd <= host.Time.GetUtcNow());
    }

    [Fact]
    public async Task FiveFailures_LockSignIn_EvenWithCorrectPassword()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWithOwnerAsync(ct);

        await FailFiveTimesAsync(host, ct);
        var result = await SignInAsync(host, TestData.Password, ct);

        Assert.Equal(OwnerSignInOutcome.Refused, result.Outcome);
        Assert.Null(result.Session);
        Assert.Equal(host.Time.GetUtcNow() + Lockout, (await host.OwnerAsync(ct))!.LockoutEnd);
    }

    [Fact]
    public async Task DuringLockout_WrongPassword_DoesNotChangeCounterOrExtendLockout()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWithOwnerAsync(ct);
        await FailFiveTimesAsync(host, ct);
        var locked = await host.OwnerAsync(ct);

        host.Time.Advance(TimeSpan.FromMinutes(5));
        var wrong = await SignInAsync(host, WrongPassword, ct);
        var correct = await SignInAsync(host, TestData.Password, ct);

        Assert.Equal(OwnerSignInOutcome.Refused, wrong.Outcome);
        Assert.Equal(OwnerSignInOutcome.Refused, correct.Outcome);
        var after = await host.OwnerAsync(ct);
        Assert.Equal(locked!.AccessFailedCount, after!.AccessFailedCount);
        Assert.Equal(locked.LockoutEnd, after.LockoutEnd);
    }

    [Fact]
    public async Task AfterFifteenMinutes_CorrectPassword_SignsIn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWithOwnerAsync(ct);
        await FailFiveTimesAsync(host, ct);

        host.Time.Advance(Lockout - TimeSpan.FromSeconds(1));
        var justBefore = await SignInAsync(host, TestData.Password, ct);
        host.Time.Advance(TimeSpan.FromSeconds(2));
        var justAfter = await SignInAsync(host, TestData.Password, ct);

        Assert.Equal(OwnerSignInOutcome.Refused, justBefore.Outcome);
        Assert.Equal(OwnerSignInOutcome.SignedIn, justAfter.Outcome);
    }

    [Fact]
    public async Task AfterLockoutEnds_CounterStartsFromZero()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWithOwnerAsync(ct);
        await FailFiveTimesAsync(host, ct);
        host.Time.Advance(Lockout + TimeSpan.FromSeconds(1));

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            Assert.Equal(OwnerSignInOutcome.Refused, (await SignInAsync(host, WrongPassword, ct)).Outcome);
        }

        var result = await SignInAsync(host, TestData.Password, ct);

        Assert.Equal(OwnerSignInOutcome.SignedIn, result.Outcome);
    }

    [Fact]
    public async Task RepeatedLockouts_AlwaysReleaseAfterFifteenMinutes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWithOwnerAsync(ct);

        for (var cycle = 1; cycle <= 3; cycle++)
        {
            await FailFiveTimesAsync(host, ct);
            Assert.Equal(OwnerSignInOutcome.Refused, (await SignInAsync(host, TestData.Password, ct)).Outcome);
            host.Time.Advance(Lockout + TimeSpan.FromSeconds(1));
            Assert.Equal(OwnerSignInOutcome.SignedIn, (await SignInAsync(host, TestData.Password, ct)).Outcome);
        }
    }

    private async Task<ControlPlaneTestHost> StartWithOwnerAsync(CancellationToken cancellationToken)
    {
        var host = await ControlPlaneTestHost.StartAsync(database, cancellationToken);
        try
        {
            using var client = await host.CreateOwnerAsync(cancellationToken);
            return host;
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }
    }

    private static async Task FailFiveTimesAsync(ControlPlaneTestHost host, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            Assert.Equal(OwnerSignInOutcome.Refused, (await SignInAsync(host, WrongPassword, cancellationToken)).Outcome);
        }
    }

    private static async Task<OwnerSignInResult> SignInAsync(
        ControlPlaneTestHost host,
        string password,
        CancellationToken cancellationToken)
    {
        using var scope = host.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<OwnerSignInService>();
        return await service.SignInAsync(TestData.Login, password, "test-request", cancellationToken);
    }
}
