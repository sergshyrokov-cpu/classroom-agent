using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-012 AC-005, AC-006, AC-007, AC-009: disabling, re-enabling and resetting a Dean's password (spec FR-007,
/// FR-008, FR-009). Each of the three does exactly what BR-014 says and nothing more — the differences between
/// them are the rule.
/// </summary>
public sealed class DeanAccountAdministrationTests
{
    /// <summary>AC-005: the account is disabled and its security stamp rotates, ending open sessions (I-4).</summary>
    [Fact]
    public async Task Disabling_MarksTheAccountAndRotatesTheStamp()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();
        var stamp = dean.SecurityStamp;

        var outcome = await world.SetState.ExecuteAsync(admin.Id, dean.Id, disable: true, "r-1", ct);

        Assert.Null(outcome.Refusal);
        Assert.True(dean.IsDisabled);
        Assert.NotEqual(stamp, dean.SecurityStamp);
    }

    /// <summary>
    /// AC-005: disabling leaves the password, the counter, the lockout and the last sign-in untouched — the row
    /// has to stay as it was for history, audit and the retention clock (BR-014, PC-11).
    /// </summary>
    [Fact]
    public async Task Disabling_TouchesNothingElse()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();
        var hash = dean.PasswordHash;
        var temporary = dean.PasswordIsTemporary;

        await world.SetState.ExecuteAsync(admin.Id, dean.Id, disable: true, "r-1", ct);

        Assert.Equal(hash, dean.PasswordHash);
        Assert.Equal(temporary, dean.PasswordIsTemporary);
        Assert.Equal(0, dean.AccessFailedCount);
        Assert.Null(dean.LockoutEnd);
    }

    /// <summary>AC-006: re-enabling clears the disabled state and changes nothing else (spec FR-008).</summary>
    [Fact]
    public async Task ReEnabling_ClearsTheStateAndNothingElse()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();
        await world.SetState.ExecuteAsync(admin.Id, dean.Id, disable: true, "r-1", ct);
        var hash = dean.PasswordHash;

        var outcome = await world.SetState.ExecuteAsync(admin.Id, dean.Id, disable: false, "r-2", ct);

        Assert.Null(outcome.Refusal);
        Assert.False(dean.IsDisabled);
        Assert.Equal(hash, dean.PasswordHash);

        // "Nothing else" means exactly that: the temporary mark keeps the value it had, it is not cleared
        // (spec FR-008 — re-enabling sets no temporary password and clears none).
        Assert.True(dean.PasswordIsTemporary);
    }

    /// <summary>AC-006: re-enabling does not clear a lockout — only a reset does (BR-014 v64).</summary>
    [Fact]
    public async Task ReEnabling_DoesNotClearALockout()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();
        LockOut(world, dean);
        await world.SetState.ExecuteAsync(admin.Id, dean.Id, disable: true, "r-1", ct);

        await world.SetState.ExecuteAsync(admin.Id, dean.Id, disable: false, "r-2", ct);

        Assert.True(dean.IsLockedOut(world.Time.GetUtcNow()));
    }

    /// <summary>AC-005, AC-006, AC-016: each direction writes its own audit action (db-design §4.1).</summary>
    [Fact]
    public async Task EachDirection_WritesItsOwnAuditAction()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();

        await world.SetState.ExecuteAsync(admin.Id, dean.Id, disable: true, "r-1", ct);
        await world.SetState.ExecuteAsync(admin.Id, dean.Id, disable: false, "r-2", ct);

        Assert.Collection(
            world.Audit.Written,
            first =>
            {
                Assert.Equal(AuditAction.DeanAccountDisabled, first.Action);
                Assert.Equal(dean.Id, first.TargetId);
                Assert.Equal(AuditOutcome.Succeeded, first.Outcome);
            },
            second =>
            {
                Assert.Equal(AuditAction.DeanAccountReEnabled, second.Action);
                Assert.Equal(dean.Id, second.TargetId);
            });
    }

    /// <summary>AC-005: an account already in the requested state is refused, and nothing is written.</summary>
    [Fact]
    public async Task AnAccountAlreadyInThatState_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();

        var outcome = await world.SetState.ExecuteAsync(admin.Id, dean.Id, disable: false, "r-1", ct);

        Assert.Equal(DeanAccountRefusal.AlreadyInThatState, outcome.Refusal);
        Assert.False(dean.IsDisabled);
        Assert.Empty(world.Audit.Written);
    }

    /// <summary>
    /// AC-005, I-8: an id that is not a Dean and an id that matches nothing answer identically, so the screen
    /// cannot be used to discover that an id belongs to an Admin (spec VR-005, S-03).
    /// </summary>
    [Fact]
    public async Task AnAdminIdAndAnUnknownId_AreRefusedIdentically()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();

        var onAdmin = await world.SetState.ExecuteAsync(admin.Id, admin.Id, disable: true, "r-1", ct);
        var onNobody = await world.SetState.ExecuteAsync(admin.Id, 9999, disable: true, "r-2", ct);

        Assert.Equal(DeanAccountRefusal.NoSuchDeanAccount, onAdmin.Refusal);
        Assert.Equal(DeanAccountRefusal.NoSuchDeanAccount, onNobody.Refusal);
        Assert.False(admin.IsDisabled);
        Assert.Empty(world.Audit.Written);
    }

    /// <summary>AC-007: a reset replaces the hash, marks it temporary and rotates the stamp (spec FR-009).</summary>
    [Fact]
    public async Task AReset_ReplacesTheHashAndMarksItTemporary()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();
        await world.ForcedChange.ExecuteAsync(dean.Id, DeanAccountTestData.NewPassword, "r-0", ct);
        var stamp = dean.SecurityStamp;

        var outcome = await world.Reset.ExecuteAsync(admin.Id, dean.Id, "a fresh temporary one", "r-1", ct);

        Assert.Null(outcome.Refusal);
        Assert.True(dean.PasswordIsTemporary);
        Assert.Equal(DeanAccountWorld.Hashes.Marker + "a fresh temporary one", dean.PasswordHash);
        Assert.NotEqual(stamp, dean.SecurityStamp);
    }

    /// <summary>AC-007: a reset clears the lockout and zeroes the counter (BR-014 v64).</summary>
    [Fact]
    public async Task AReset_ClearsTheLockout()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();
        LockOut(world, dean);

        await world.Reset.ExecuteAsync(admin.Id, dean.Id, "a fresh temporary one", "r-1", ct);

        Assert.Equal(0, dean.AccessFailedCount);
        Assert.Null(dean.LockoutEnd);
        Assert.False(dean.IsLockedOut(world.Time.GetUtcNow()));
    }

    /// <summary>AC-007: a reset never re-enables a disabled account (spec S-08).</summary>
    [Fact]
    public async Task AReset_LeavesADisabledAccountDisabled()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();
        await world.SetState.ExecuteAsync(admin.Id, dean.Id, disable: true, "r-1", ct);

        var outcome = await world.Reset.ExecuteAsync(admin.Id, dean.Id, "a fresh temporary one", "r-2", ct);

        Assert.Null(outcome.Refusal);
        Assert.True(dean.IsDisabled);
    }

    /// <summary>AC-004: the reset password obeys the policy, checked against that account's own login.</summary>
    [Fact]
    public async Task AResetPasswordThatBreaksThePolicy_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();
        var hash = dean.PasswordHash;

        var outcome = await world.Reset.ExecuteAsync(admin.Id, dean.Id, DeanAccountTestData.DeanEmail, "r-1", ct);

        Assert.Equal(DeanAccountRefusal.PasswordPolicy, outcome.Refusal);
        Assert.Equal(PasswordPolicyViolation.EqualsLogin, outcome.Violation);
        Assert.Equal(hash, dean.PasswordHash);
    }

    /// <summary>AC-009: every management action consults the guard first and writes its refusal row.</summary>
    [Fact]
    public async Task InReadOnlyMode_DisablingIsRefusedAndAudited()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld(readOnly: true);
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();

        await Assert.ThrowsAsync<ReadOnlyModeException>(() =>
            world.SetState.ExecuteAsync(admin.Id, dean.Id, disable: true, "r-1", ct));

        Assert.False(dean.IsDisabled);
        var row = Assert.Single(world.Audit.Written);
        Assert.Equal(AuditAction.DeanAccountDisabled, row.Action);
        Assert.Equal(AuditRefusalCategory.ReadOnlyMode, row.RefusalCategory);
        Assert.Equal(dean.Id, row.TargetId);
    }

    /// <summary>AC-009: the same for a reset — no hash is computed at all.</summary>
    [Fact]
    public async Task InReadOnlyMode_AResetIsRefusedBeforeAnyHashing()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld(readOnly: true);
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();
        var hashesBefore = world.Hasher.Hashed.Count;

        await Assert.ThrowsAsync<ReadOnlyModeException>(() =>
            world.Reset.ExecuteAsync(admin.Id, dean.Id, "a fresh temporary one", "r-1", ct));

        Assert.Equal(hashesBefore, world.Hasher.Hashed.Count);
        var row = Assert.Single(world.Audit.Written);
        Assert.Equal(AuditAction.DeanAccountPasswordReset, row.Action);
        Assert.Equal(AuditRefusalCategory.ReadOnlyMode, row.RefusalCategory);
    }

    private static void LockOut(DeanAccountWorld world, Domain.Entities.AppUser dean)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            dean.RecordFailedSignIn(world.Time.GetUtcNow(), 5, TimeSpan.FromMinutes(15));
        }
    }
}
