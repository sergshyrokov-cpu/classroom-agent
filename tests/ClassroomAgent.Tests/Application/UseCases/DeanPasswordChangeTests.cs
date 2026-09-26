using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-012 AC-012, AC-013: the forced change of a temporary password and the Dean's own later change
/// (spec FR-006, FR-014). Both are service writes BR-026 permits in read-only mode, and neither ever leaves the
/// Admin with a working password (SC-2, spec S-07).
/// </summary>
public sealed class DeanPasswordChangeTests
{
    /// <summary>AC-012: the forced change clears the temporary mark and rotates the stamp.</summary>
    [Fact]
    public async Task TheForcedChange_ClearsTheTemporaryMark()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();
        var stamp = dean.SecurityStamp;

        var outcome = await world.ForcedChange.ExecuteAsync(dean.Id, DeanAccountTestData.NewPassword, "r-1", ct);

        Assert.False(outcome.Refused);
        Assert.False(dean.PasswordIsTemporary);
        Assert.Equal(DeanAccountWorld.Hashes.Marker + DeanAccountTestData.NewPassword, dean.PasswordHash);
        Assert.NotEqual(stamp, dean.SecurityStamp);
    }

    /// <summary>
    /// AC-012: the new password may not equal the temporary one (BR-014 v64, spec VR-003). The check is made
    /// against the hash still stored — no plaintext is kept anywhere to compare with.
    /// </summary>
    [Fact]
    public async Task ANewPasswordEqualToTheTemporaryOne_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();

        var outcome = await world.ForcedChange.ExecuteAsync(
            dean.Id,
            DeanAccountTestData.TemporaryPassword,
            "r-1",
            ct);

        Assert.True(outcome.Refused);
        Assert.Equal(PasswordPolicyViolation.EqualsTemporaryPassword, outcome.Violation);
        Assert.True(dean.PasswordIsTemporary);
    }

    /// <summary>AC-004, AC-012: the new password obeys the policy like any other.</summary>
    [Fact]
    public async Task ANewPasswordThatBreaksThePolicy_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();

        var outcome = await world.ForcedChange.ExecuteAsync(
            dean.Id,
            DeanAccountTestData.TooShortPassword,
            "r-1",
            ct);

        Assert.True(outcome.Refused);
        Assert.Equal(PasswordPolicyViolation.TooShort, outcome.Violation);
        Assert.True(dean.PasswordIsTemporary);
    }

    /// <summary>AC-012, AC-016: the forced change writes one audit row, by the Dean themselves.</summary>
    [Fact]
    public async Task TheForcedChange_IsAuditedAsTheDeansOwn()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();

        await world.ForcedChange.ExecuteAsync(dean.Id, DeanAccountTestData.NewPassword, "r-1", ct);

        var row = Assert.Single(world.Audit.Written);
        Assert.Equal(AuditAction.DeanPasswordChanged, row.Action);
        Assert.Equal(AuditOutcome.Succeeded, row.Outcome);
        Assert.Equal(dean.Id, row.ActorId);
        Assert.Equal(AppRole.Dean, row.ActorRole);
        Assert.Equal(dean.Id, row.TargetId);
    }

    /// <summary>AC-013: the voluntary change needs the current password.</summary>
    [Fact]
    public async Task TheVoluntaryChange_NeedsTheCurrentPassword()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();
        await world.ForcedChange.ExecuteAsync(dean.Id, DeanAccountTestData.NewPassword, "r-1", ct);
        var hash = dean.PasswordHash;

        var outcome = await world.ChangeOwn.ExecuteAsync(
            dean.Id,
            DeanAccountTestData.WrongPassword,
            "another good password",
            "r-2",
            ct);

        Assert.True(outcome.Refused);
        Assert.True(outcome.WrongCurrentPassword);
        Assert.Equal(hash, dean.PasswordHash);
    }

    /// <summary>
    /// AC-013: a wrong current password is **not** a failed sign-in attempt — it moves no counter and triggers
    /// no lockout (spec FR-013 governs sign-in alone).
    /// </summary>
    [Fact]
    public async Task AWrongCurrentPassword_IsNotAFailedSignInAttempt()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();
        await world.ForcedChange.ExecuteAsync(dean.Id, DeanAccountTestData.NewPassword, "r-1", ct);

        for (var attempt = 0; attempt < 6; attempt++)
        {
            await world.ChangeOwn.ExecuteAsync(dean.Id, DeanAccountTestData.WrongPassword, "another good password", "r-x", ct);
        }

        Assert.Equal(0, dean.AccessFailedCount);
        Assert.False(dean.IsLockedOut(world.Time.GetUtcNow()));
    }

    /// <summary>AC-013: with the correct current password the change succeeds and the stamp rotates.</summary>
    [Fact]
    public async Task TheVoluntaryChange_ReplacesTheHashAndRotatesTheStamp()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();
        await world.ForcedChange.ExecuteAsync(dean.Id, DeanAccountTestData.NewPassword, "r-1", ct);
        var stamp = dean.SecurityStamp;

        var outcome = await world.ChangeOwn.ExecuteAsync(
            dean.Id,
            DeanAccountTestData.NewPassword,
            "a third good password",
            "r-2",
            ct);

        Assert.False(outcome.Refused);
        Assert.Equal(DeanAccountWorld.Hashes.Marker + "a third good password", dean.PasswordHash);
        Assert.NotEqual(stamp, dean.SecurityStamp);
    }

    /// <summary>
    /// AC-013, I-7: **no rule forbids a voluntary new password equal to the current one**, so it is accepted.
    /// The test exists so a later implementer cannot quietly add a requirement nobody wrote (spec VR-003).
    /// </summary>
    [Fact]
    public async Task ANewPasswordEqualToTheCurrentOne_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();
        await world.ForcedChange.ExecuteAsync(dean.Id, DeanAccountTestData.NewPassword, "r-1", ct);

        var outcome = await world.ChangeOwn.ExecuteAsync(
            dean.Id,
            DeanAccountTestData.NewPassword,
            DeanAccountTestData.NewPassword,
            "r-2",
            ct);

        Assert.False(outcome.Refused);
        Assert.Null(outcome.Violation);
    }

    /// <summary>
    /// AC-013: both changes work in read-only mode — BR-026 names "a Dean changing their own password" among
    /// the permitted service writes, and the use cases consult no guard.
    /// </summary>
    [Fact]
    public async Task BothChanges_WorkInReadOnlyMode()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld(readOnly: true);
        var dean = world.SeedDean();

        var forced = await world.ForcedChange.ExecuteAsync(dean.Id, DeanAccountTestData.NewPassword, "r-1", ct);
        var voluntary = await world.ChangeOwn.ExecuteAsync(
            dean.Id,
            DeanAccountTestData.NewPassword,
            "a third good password",
            "r-2",
            ct);

        Assert.False(forced.Refused);
        Assert.False(voluntary.Refused);
        Assert.Empty(world.ReadOnly.Operations);
    }

    /// <summary>AC-016: the voluntary change writes one audit row too.</summary>
    [Fact]
    public async Task TheVoluntaryChange_IsAudited()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();
        await world.ForcedChange.ExecuteAsync(dean.Id, DeanAccountTestData.NewPassword, "r-1", ct);

        await world.ChangeOwn.ExecuteAsync(dean.Id, DeanAccountTestData.NewPassword, "a third good password", "r-2", ct);

        Assert.Equal(2, world.Audit.Written.Count);
        Assert.All(world.Audit.Written, row => Assert.Equal(AuditAction.DeanPasswordChanged, row.Action));
    }

    /// <summary>S-10: no outcome of either change carries a password or a hash.</summary>
    [Fact]
    public async Task NoOutcome_CarriesAPasswordOrAHash()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();

        var outcome = await world.ForcedChange.ExecuteAsync(dean.Id, DeanAccountTestData.NewPassword, "r-1", ct);

        var rendered = outcome.ToString() ?? string.Empty;
        Assert.DoesNotContain(DeanAccountTestData.NewPassword, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(DeanAccountWorld.Hashes.Marker, rendered, StringComparison.Ordinal);
    }
}
