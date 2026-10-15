using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-038 spec FR-009, FR-010, FR-013, FR-014, VR-003, VR-004, I-10, I-12, I-13; AC-007, AC-008, AC-009, AC-016, AC-017
/// in the Application layer with every port substituted (TC-1, TC-5).
/// </summary>
public sealed class LiftErasureExclusionUseCaseTests
{
    private const string Email = "o.koval@school-one.example.test";

    public static TheoryData<LegitimacyModeReason> ReadOnlyReasons => new(
        LegitimacyModeReason.NotYetConfirmed,
        LegitimacyModeReason.SuspendedByOwner,
        LegitimacyModeReason.GracePeriodExpired);

    public static TheoryData<string?> RequiredCases => new(null, string.Empty, "   ");

    public static TheoryData<string> InvalidCases => new("o.koval", "o.koval@@school.test", "@school.test", "o.koval@", "a@b@c.test", "o koval@school.test");

    private static Task<ExclusionLiftingResult> LiftAsync(ErasureUnitWorld world, string? email) =>
        world.Lift.ExecuteAsync(ErasureUnitWorld.AdminId, email, "req-2", TestContext.Current.CancellationToken);

    /// <summary>A world with a salt and the given exclusions of <see cref="Email"/> (and one of another address).</summary>
    private static (ErasureUnitWorld World, IReadOnlyList<ErasedParticipant> Matching) WorldWithExclusions(int matching, bool readOnly = false, LegitimacyModeReason reason = LegitimacyModeReason.SuspendedByOwner)
    {
        var world = new ErasureUnitWorld(readOnly, reason);
        var salt = ErasureSalt.Generate();
        world.Salts.Salt = salt;
        var found = new List<ErasedParticipant>();
        for (var i = 0; i < matching; i++)
        {
            found.Add(world.Exclusions.Seed(ErasedParticipant.Record($"10000000000000010{i}", EmailFingerprint.Of(Email, salt), world.Time.GetUtcNow())));
        }

        world.Exclusions.Seed(ErasedParticipant.Record("100000000000000999", EmailFingerprint.Of("other@school-one.example.test", salt), world.Time.GetUtcNow()));
        return (world, found);
    }

    /// <summary>AC-008, AC-017, FR-014, I-12: every BR-025 cause refuses; one refused audit row naming no target.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyReasons))]
    public async Task InReadOnlyMode_TheLiftingIsRefused_WithOneRefusedAuditRow_AndNothingElse(LegitimacyModeReason reason)
    {
        var (world, _) = WorldWithExclusions(1, readOnly: true, reason: reason);
        world.Calls.Clear();

        var refusal = await Assert.ThrowsAsync<ReadOnlyModeException>(() => LiftAsync(world, Email));

        Assert.Equal(reason, refusal.Reason);
        var audit = Assert.Single(world.Audit.Added);
        Assert.Equal(AuditAction.ErasureExclusionLifted, audit.Action);
        Assert.Equal(AuditOutcome.Refused, audit.Outcome);
        Assert.Equal(AuditRefusalCategory.ReadOnlyMode, audit.RefusalCategory);
        Assert.Equal(ErasureUnitWorld.AdminId, audit.ActorId);
        Assert.Equal(AppRole.Admin, audit.ActorRole);
        Assert.Null(audit.TargetType);
        Assert.Null(audit.TargetId);
        Assert.Equal(["guard", "audit", "save"], world.Calls);
        Assert.Equal([PermittedServiceWrite.AuditEvent], world.Work.SavesDeclaring);
        Assert.Empty(world.Exclusions.Removed);
    }

    /// <summary>api-design §2.4: the guard runs before the email is validated.</summary>
    [Fact]
    public async Task InReadOnlyMode_AnInvalidEmail_IsStillARefusal()
    {
        var world = new ErasureUnitWorld(readOnly: true);

        await Assert.ThrowsAsync<ReadOnlyModeException>(() => LiftAsync(world, "not an address"));

        Assert.Equal([LiftErasureExclusionUseCase.Operation], world.Guard.Operations);
        Assert.Equal(ErasureTestData.Operations.Lift, LiftErasureExclusionUseCase.Operation);
    }

    /// <summary>VR-003: a missing or blank email is refused as required; nothing is read or written.</summary>
    [Theory]
    [MemberData(nameof(RequiredCases))]
    public async Task AMissingEmail_IsRequired_AndNothingIsTouched(string? email)
    {
        var world = new ErasureUnitWorld();

        var result = await LiftAsync(world, email);

        Assert.Equal(new ExclusionLiftingResult(ExclusionLiftingOutcome.InvalidEmail, LiftFieldError.EmailRequired, 0), result);
        Assert.Equal(["guard"], world.Calls);
    }

    /// <summary>VR-003: more than 254 characters after trimming is too long.</summary>
    [Fact]
    public async Task AnEmailOver254Characters_IsTooLong()
    {
        var world = new ErasureUnitWorld();
        var email = new string('a', 255 - "@school.test".Length) + "@school.test";

        var result = await LiftAsync(world, email);

        Assert.Equal(new ExclusionLiftingResult(ExclusionLiftingOutcome.InvalidEmail, LiftFieldError.EmailTooLong, 0), result);
        Assert.Equal(["guard"], world.Calls);
    }

    /// <summary>VR-003: anything but local@domain with exactly one @ and non-empty parts is invalid.</summary>
    [Theory]
    [MemberData(nameof(InvalidCases))]
    public async Task AMalformedEmail_IsInvalid(string email)
    {
        var world = new ErasureUnitWorld();

        var result = await LiftAsync(world, email);

        Assert.Equal(new ExclusionLiftingResult(ExclusionLiftingOutcome.InvalidEmail, LiftFieldError.EmailInvalid, 0), result);
        Assert.Equal(["guard"], world.Calls);
        Assert.Empty(world.Audit.Added);
    }

    /// <summary>VR-003: exactly 254 characters with surrounding spaces is accepted (trimmed first).</summary>
    [Fact]
    public async Task An254CharacterEmail_WithSurroundingSpaces_IsAccepted()
    {
        var world = new ErasureUnitWorld();
        world.Salts.Salt = ErasureSalt.Generate();
        var email = new string('a', 254 - "@school.test".Length) + "@school.test";

        var result = await LiftAsync(world, "  " + email + "  ");

        Assert.Equal(ExclusionLiftingOutcome.NoExclusionForEmail, result.Outcome);
    }

    /// <summary>I-13: the domain need not be the installation's.</summary>
    [Fact]
    public async Task AnEmailOfAnotherDomain_IsAccepted_AndLiftsItsExclusion()
    {
        var world = new ErasureUnitWorld();
        var salt = ErasureSalt.Generate();
        world.Salts.Salt = salt;
        world.Exclusions.Seed(ErasedParticipant.Record("100000000000000777", EmailFingerprint.Of("guest@elsewhere.example", salt), world.Time.GetUtcNow()));

        var result = await LiftAsync(world, "guest@elsewhere.example");

        Assert.Equal(ExclusionLiftingOutcome.Lifted, result.Outcome);
        Assert.Equal(1, result.Lifted);
    }

    /// <summary>db-design §3.3, §8: no salt yet means no fingerprinted exclusion — "no exclusion", nothing written.</summary>
    [Fact]
    public async Task WithNoSaltStored_TheAnswerIsNoExclusion_AndNothingIsWritten()
    {
        var world = new ErasureUnitWorld();

        var result = await LiftAsync(world, Email);

        Assert.Equal(new ExclusionLiftingResult(ExclusionLiftingOutcome.NoExclusionForEmail, null, 0), result);
        Assert.Empty(world.Audit.Added);
        Assert.Empty(world.Exclusions.Removed);
        Assert.DoesNotContain("save", world.Calls);
    }

    /// <summary>AC-007, FR-009 step 4: a valid email with no exclusion — "no exclusion", nothing changes, no audit row.</summary>
    [Fact]
    public async Task AnEmailWithNoExclusion_IsAnsweredNoExclusion_AndNothingChanges()
    {
        var (world, _) = WorldWithExclusions(0);

        var result = await LiftAsync(world, Email);

        Assert.Equal(new ExclusionLiftingResult(ExclusionLiftingOutcome.NoExclusionForEmail, null, 0), result);
        Assert.Empty(world.Exclusions.Removed);
        Assert.Empty(world.Audit.Added);
        Assert.Single(world.Exclusions.Stored);
    }

    /// <summary>AC-007, VR-004: any letter case and surrounding spaces find the exclusion; it is removed; the other stays.</summary>
    [Fact]
    public async Task TheEmailInAnyLetterCase_LiftsTheExclusion_AndOnlyIt()
    {
        var (world, matching) = WorldWithExclusions(1);

        var result = await LiftAsync(world, "  O.KOVAL@School-One.Example.Test ");

        Assert.Equal(new ExclusionLiftingResult(ExclusionLiftingOutcome.Lifted, null, 1), result);
        Assert.Equal(matching, world.Exclusions.Removed);
        Assert.Equal("100000000000000999", Assert.Single(world.Exclusions.Stored).GoogleUserId);
        Assert.Equal(EmailFingerprint.Of(Email, world.Salts.Salt!), Assert.Single(world.Exclusions.FingerprintLookups));
    }

    /// <summary>AC-016, I-10, AC-009: two exclusions of one email — both removed in one transaction, one audit row each.</summary>
    [Fact]
    public async Task TwoExclusionsOfOneEmail_AreBothLifted_InOneTransaction_WithOneAuditRowEach()
    {
        var (world, matching) = WorldWithExclusions(2);

        var result = await LiftAsync(world, Email);

        Assert.Equal(2, result.Lifted);
        Assert.Equal(1, world.Work.Transactions);
        Assert.True(world.Work.Committed);
        Assert.Equal(2, world.Audit.Added.Count);
        Assert.All(world.Audit.Added, audit =>
        {
            Assert.Equal(AuditAction.ErasureExclusionLifted, audit.Action);
            Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
            Assert.Equal(ErasureUnitWorld.AdminId, audit.ActorId);
            Assert.Equal(AppRole.Admin, audit.ActorRole);
            Assert.Equal(AuditTargetType.ErasedParticipant, audit.TargetType);
        });
        Assert.Equal(matching.Select(e => (long?)e.Id).Order(), world.Audit.Added.Select(a => a.TargetId).Order());
        Assert.True(world.Calls.LastIndexOf("exclusion:remove") < world.Calls.IndexOf("commit"));
        Assert.True(world.Calls.LastIndexOf("audit") < world.Calls.IndexOf("commit"));
    }

    /// <summary>db-design §7.3: the lifting does not take the fence.</summary>
    [Fact]
    public async Task TheLifting_DoesNotTakeTheFence()
    {
        var (world, _) = WorldWithExclusions(1);

        await LiftAsync(world, Email);

        Assert.Empty(world.Fence.EnteredInsideTransaction);
    }
}
