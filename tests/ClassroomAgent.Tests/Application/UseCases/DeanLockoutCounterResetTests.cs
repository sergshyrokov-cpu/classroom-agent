using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-048 AC-001…AC-003, FR-001, VR-001, VR-002: the failed-attempt count starts again when a lockout begins, so a
/// lockout always needs five consecutive wrong passwords (§9 v62, SC-2). The clock is the injected
/// <see cref="ManualTimeProvider"/>, so the end of a lockout is reached by moving it, not by waiting (spec §9).
/// </summary>
public sealed class DeanLockoutCounterResetTests
{
    /// <summary>AC-001: the fifth failure locks sign-in and returns the count to zero.</summary>
    [Fact]
    public async Task TheFifthFailure_LocksAndReturnsTheCountToZero()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();

        await FailAsync(world, 5, ct);

        Assert.Equal(0, dean.AccessFailedCount);
        Assert.True(dean.IsLockedOut(world.Time.GetUtcNow()));
        Assert.Equal(world.Time.GetUtcNow() + SignInDeanUseCase.LockoutDuration, dean.LockoutEnd);
    }

    /// <summary>AC-001: during the lockout even the correct password is refused at step 2, and the count stays zero.</summary>
    [Fact]
    public async Task DuringTheLockout_TheCorrectPasswordIsRefused_AndTheCountStaysZero()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();
        await FailAsync(world, 5, ct);

        var correct = await SignInAsync(world, DeanAccountTestData.TemporaryPassword, "r-correct", ct);
        var wrong = await SignInAsync(world, DeanAccountTestData.WrongPassword, "r-wrong", ct);

        Assert.Equal(DeanSignInResult.LockedOut, correct.Result);
        Assert.Equal(DeanSignInResult.LockedOut, wrong.Result);
        Assert.Equal(0, dean.AccessFailedCount);
        Assert.All(world.Audit.Written.TakeLast(2), row => Assert.Equal(AuditRefusalCategory.LockedOut, row.RefusalCategory));
    }

    /// <summary>
    /// AC-002: after the lockout ends one wrong password is a plain "wrong password" refusal, the count is 1 and
    /// sign-in is not locked — the correct password on the next attempt signs the Dean in.
    /// </summary>
    [Fact]
    public async Task AfterTheLockout_OneWrongPassword_DoesNotLockAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();
        dean.SetOwnPassword(world.Hasher.Hash(DeanAccountTestData.NewPassword), world.Time.GetUtcNow());
        await FailAsync(world, 5, ct);
        world.Time.Advance(SignInDeanUseCase.LockoutDuration);

        var wrong = await SignInAsync(world, DeanAccountTestData.WrongPassword, "r-after", ct);

        Assert.Equal(DeanSignInResult.WrongPassword, wrong.Result);
        var row = world.Audit.Written[^1];
        Assert.Equal(AuditAction.DeanSignIn, row.Action);
        Assert.Equal(AuditRefusalCategory.WrongPassword, row.RefusalCategory);
        Assert.Equal(1, dean.AccessFailedCount);
        Assert.False(dean.IsLockedOut(world.Time.GetUtcNow()));

        var correct = await SignInAsync(world, DeanAccountTestData.NewPassword, "r-correct", ct);

        Assert.Equal(DeanSignInResult.SignedIn, correct.Result);
        Assert.Equal(0, dean.AccessFailedCount);
    }

    /// <summary>AC-003: after the lockout ends four wrong passwords do not lock — the boundary belongs to the fifth again.</summary>
    [Fact]
    public async Task AfterTheLockout_FourFailures_DoNotLock()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();
        await FailAsync(world, 5, ct);
        world.Time.Advance(SignInDeanUseCase.LockoutDuration);

        await FailAsync(world, 4, ct);

        Assert.Equal(4, dean.AccessFailedCount);
        Assert.False(dean.IsLockedOut(world.Time.GetUtcNow()));
    }

    /// <summary>
    /// AC-003: after the lockout ends five more failures lock again for fifteen minutes from the fifth, and that
    /// lockout too expires by itself — no lockout is permanent (SC-2 v62).
    /// </summary>
    [Fact]
    public async Task AfterTheLockout_FiveMoreFailures_LockAgain_AndTheLockLiftsByItself()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();
        await FailAsync(world, 5, ct);
        world.Time.Advance(SignInDeanUseCase.LockoutDuration);

        await FailAsync(world, 5, ct);

        Assert.True(dean.IsLockedOut(world.Time.GetUtcNow()));
        Assert.Equal(world.Time.GetUtcNow() + SignInDeanUseCase.LockoutDuration, dean.LockoutEnd);
        Assert.Equal(0, dean.AccessFailedCount);

        world.Time.Advance(SignInDeanUseCase.LockoutDuration);
        var correct = await SignInAsync(world, DeanAccountTestData.TemporaryPassword, "r-correct", ct);

        Assert.Equal(DeanSignInResult.TemporaryPassword, correct.Result);
    }

    /// <summary>
    /// VR-002: across three lockouts in a row, the stored count never exceeds four while no lockout is in force, and
    /// each lockout begins at exactly the fifth failure after the previous one ended.
    /// </summary>
    [Fact]
    public async Task AcrossSeveralLockouts_TheCountNeverExceedsFourWithoutALockout()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();

        for (var lockout = 0; lockout < 3; lockout++)
        {
            for (var failure = 1; failure <= 5; failure++)
            {
                await SignInAsync(world, DeanAccountTestData.WrongPassword, $"r-{lockout}-{failure}", ct);
                var locked = dean.IsLockedOut(world.Time.GetUtcNow());
                Assert.Equal(failure == 5, locked);
                Assert.InRange(dean.AccessFailedCount, 0, 4);
                Assert.Equal(failure == 5 ? 0 : failure, dean.AccessFailedCount);
            }

            world.Time.Advance(SignInDeanUseCase.LockoutDuration);
        }
    }

    /// <summary>FR-001: the entity zeroes the count in the same call that starts the lockout; the numbers come from the caller.</summary>
    [Fact]
    public void RecordFailedSignIn_AtTheMaximum_StartsTheLockoutAndZeroesTheCount()
    {
        var at = InstallationTestHost.DefaultStart;
        var dean = AppUser.CreateDean(DeanAccountTestData.DeanEmail, "hash", UiLanguage.Uk, at);

        dean.RecordFailedSignIn(at, 3, TimeSpan.FromMinutes(7));
        dean.RecordFailedSignIn(at, 3, TimeSpan.FromMinutes(7));
        Assert.Equal(2, dean.AccessFailedCount);
        Assert.Null(dean.LockoutEnd);

        dean.RecordFailedSignIn(at, 3, TimeSpan.FromMinutes(7));

        Assert.Equal(0, dean.AccessFailedCount);
        Assert.Equal(at + TimeSpan.FromMinutes(7), dean.LockoutEnd);
    }

    /// <summary>VR-001: a maximum below one is still rejected.</summary>
    [Fact]
    public void RecordFailedSignIn_WithAMaximumBelowOne_IsRejected()
    {
        var at = InstallationTestHost.DefaultStart;
        var dean = AppUser.CreateDean(DeanAccountTestData.DeanEmail, "hash", UiLanguage.Uk, at);

        Assert.Throws<ArgumentOutOfRangeException>(() => dean.RecordFailedSignIn(at, 0, TimeSpan.FromMinutes(15)));
    }

    private static Task<DeanSignInOutcome> SignInAsync(
        DeanAccountWorld world,
        string password,
        string requestId,
        CancellationToken cancellationToken) =>
        world.SignIn.ExecuteAsync(DeanAccountTestData.DeanEmail, password, requestId, cancellationToken);

    private static async Task FailAsync(DeanAccountWorld world, int times, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < times; attempt++)
        {
            await SignInAsync(world, DeanAccountTestData.WrongPassword, "r-" + attempt, cancellationToken);
        }
    }
}
