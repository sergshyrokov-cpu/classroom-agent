using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-012 AC-011: five consecutive failures lock sign-in for fifteen minutes, and the lock lifts by itself
/// (spec FR-013; SC-2 v62). The clock is the injected <see cref="ManualTimeProvider"/>, so the boundary is
/// proved exactly rather than waited for.
/// </summary>
public sealed class DeanLockoutTests
{
    /// <summary>AC-011: four failures do not lock — the boundary belongs to the fifth.</summary>
    [Fact]
    public async Task FourFailures_DoNotLock()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();

        await FailAsync(world, 4, ct);

        Assert.Equal(4, dean.AccessFailedCount);
        Assert.False(dean.IsLockedOut(world.Time.GetUtcNow()));
    }

    /// <summary>AC-011: the fifth consecutive failure locks sign-in.</summary>
    [Fact]
    public async Task TheFifthFailure_Locks()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();

        await FailAsync(world, 5, ct);

        Assert.True(dean.IsLockedOut(world.Time.GetUtcNow()));
        Assert.Equal(
            world.Time.GetUtcNow() + SignInDeanUseCase.LockoutDuration,
            dean.LockoutEnd);
    }

    /// <summary>AC-011: while locked, even the correct password is refused with the locked-out outcome.</summary>
    [Fact]
    public async Task WhileLocked_EvenTheCorrectPasswordIsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        world.SeedDean();
        await FailAsync(world, 5, ct);

        var outcome = await world.SignIn.ExecuteAsync(
            DeanAccountTestData.DeanEmail,
            DeanAccountTestData.TemporaryPassword,
            "r-6",
            ct);

        Assert.Equal(DeanSignInResult.LockedOut, outcome.Result);
    }

    /// <summary>AC-011: one second before the fifteen minutes are up, the lock is still in force.</summary>
    [Fact]
    public async Task OneSecondBeforeTheLimit_TheLockHolds()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        world.SeedDean();
        await FailAsync(world, 5, ct);

        world.Time.Advance(SignInDeanUseCase.LockoutDuration - TimeSpan.FromSeconds(1));
        var outcome = await world.SignIn.ExecuteAsync(
            DeanAccountTestData.DeanEmail,
            DeanAccountTestData.TemporaryPassword,
            "r-6",
            ct);

        Assert.Equal(DeanSignInResult.LockedOut, outcome.Result);
    }

    /// <summary>
    /// AC-011: the lock lifts by itself at fifteen minutes — no Admin action is needed, and there is no
    /// permanent lockout (SC-2 v62).
    /// </summary>
    [Fact]
    public async Task AtTheLimit_TheLockLiftsByItself()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        world.SeedDean();
        await FailAsync(world, 5, ct);

        world.Time.Advance(SignInDeanUseCase.LockoutDuration);
        var outcome = await world.SignIn.ExecuteAsync(
            DeanAccountTestData.DeanEmail,
            DeanAccountTestData.TemporaryPassword,
            "r-6",
            ct);

        Assert.Equal(DeanSignInResult.TemporaryPassword, outcome.Result);
    }

    /// <summary>AC-011: the policy numbers are the SC-2 ones and nothing else.</summary>
    [Fact]
    public void ThePolicyNumbers_AreTheOnesSc2Fixes()
    {
        Assert.Equal(5, SignInDeanUseCase.MaximumFailedAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), SignInDeanUseCase.LockoutDuration);
    }

    private static async Task FailAsync(DeanAccountWorld world, int times, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < times; attempt++)
        {
            await world.SignIn.ExecuteAsync(
                DeanAccountTestData.DeanEmail,
                DeanAccountTestData.WrongPassword,
                "r-" + attempt,
                cancellationToken);
        }
    }
}
