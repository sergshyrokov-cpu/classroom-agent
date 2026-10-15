using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-038 spec FR-004 … FR-008, FR-013, FR-014, FR-016, OD-001 a; AC-002, AC-003, AC-006, AC-008, AC-009, AC-015,
/// AC-017, AC-019 in the Application layer with every port substituted (TC-1, TC-5): the guard first and a refusal
/// audited under the audit service write; the mark before anything else; one transaction whose first step is the
/// exclusive fence; the exclusion of the Google user id and the salted fingerprint; one audit row with the counts.
/// </summary>
public sealed class EraseParticipantUseCaseTests
{
    private const long ParticipantId = 41;
    private const string GoogleUserId = "100000000000000041";
    private const string StoredEmail = "  O.Koval@School-One.Example.Test ";
    private const string NormalizedEmail = "o.koval@school-one.example.test";

    private static readonly ErasureCounts Counts = new(2, 3, 4, 1, 1);

    public static TheoryData<LegitimacyModeReason> ReadOnlyReasons => new(
        LegitimacyModeReason.NotYetConfirmed,
        LegitimacyModeReason.SuspendedByOwner,
        LegitimacyModeReason.GracePeriodExpired);

    private static ErasureUnitWorld WorldWithTarget(string? email = StoredEmail, bool readOnly = false, LegitimacyModeReason reason = LegitimacyModeReason.SuspendedByOwner)
    {
        var world = new ErasureUnitWorld(readOnly, reason);
        world.Reads.Targets[ParticipantId] = new ParticipantErasureTarget(ParticipantId, GoogleUserId, email);
        world.Store.Answer = Counts;
        return world;
    }

    private static Task<ParticipantErasureResult> EraseAsync(ErasureUnitWorld world, bool mark = true) =>
        world.Erase.ExecuteAsync(ErasureUnitWorld.AdminId, ParticipantId, mark, "req-1", TestContext.Current.CancellationToken);

    /// <summary>AC-008, AC-017, FR-014, I-12: every BR-025 cause refuses with the read-only exception and one refused audit row.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyReasons))]
    public async Task InReadOnlyMode_TheErasureIsRefused_WithOneRefusedAuditRow_AndNothingElse(LegitimacyModeReason reason)
    {
        var world = WorldWithTarget(readOnly: true, reason: reason);

        var refusal = await Assert.ThrowsAsync<ReadOnlyModeException>(() => EraseAsync(world));

        Assert.Equal(reason, refusal.Reason);
        var audit = Assert.Single(world.Audit.Added);
        Assert.Equal(AuditAction.ParticipantErased, audit.Action);
        Assert.Equal(AuditOutcome.Refused, audit.Outcome);
        Assert.Equal(AuditRefusalCategory.ReadOnlyMode, audit.RefusalCategory);
        Assert.Equal(AuditActorType.AppUser, audit.ActorType);
        Assert.Equal(ErasureUnitWorld.AdminId, audit.ActorId);
        Assert.Equal(AppRole.Admin, audit.ActorRole);
        Assert.Equal(AuditTargetType.ClassroomParticipant, audit.TargetType);
        Assert.Equal(ParticipantId, audit.TargetId);
        Assert.Null(audit.ErasedMemberships);
        Assert.Equal(["guard", "audit", "save"], world.Calls);
        Assert.Equal([PermittedServiceWrite.AuditEvent], world.Work.SavesDeclaring);
        Assert.Empty(world.Store.Erasures);
        Assert.Empty(world.Exclusions.Added);
        Assert.Equal(0, world.Work.Transactions);
    }

    /// <summary>api-design §2.4: the guard runs before the mark, so a refused request is a refusal whatever its body.</summary>
    [Fact]
    public async Task InReadOnlyMode_AnUnsetMark_IsStillARefusal()
    {
        var world = WorldWithTarget(readOnly: true);

        await Assert.ThrowsAsync<ReadOnlyModeException>(() => EraseAsync(world, mark: false));

        Assert.Single(world.Audit.Added);
    }

    /// <summary>FR-014, SC-10: the guard is asked first, with the constant operation name.</summary>
    [Fact]
    public async Task TheGuard_IsAskedFirst_WithTheOperationName()
    {
        var world = WorldWithTarget();

        await EraseAsync(world);

        Assert.Equal("guard", world.Calls[0]);
        Assert.Equal([EraseParticipantUseCase.Operation], world.Guard.Operations);
        Assert.Equal(ErasureTestData.Operations.Erase, EraseParticipantUseCase.Operation);
    }

    /// <summary>AC-003, VR-002: without the mark nothing is read, written or audited.</summary>
    [Fact]
    public async Task WithoutTheMark_TheErasureIsRefused_AndNothingIsTouched()
    {
        var world = WorldWithTarget();

        var result = await EraseAsync(world, mark: false);

        Assert.Equal(ParticipantErasureOutcome.IrreversibleMarkRequired, result.Outcome);
        Assert.Null(result.Counts);
        Assert.Equal(["guard"], world.Calls);
        Assert.Empty(world.Audit.Added);
    }

    /// <summary>FR-004, FR-011, db-design §7.1: one transaction, the exclusive fence its first step, the target read after it.</summary>
    [Fact]
    public async Task TheErasure_RunsInOneTransaction_FenceFirst_ThenTheTarget_ThenTheDeletes()
    {
        var world = WorldWithTarget();

        await EraseAsync(world);

        Assert.Equal(1, world.Work.Transactions);
        Assert.True(world.Work.Committed);
        Assert.Equal([true], world.Fence.EnteredInsideTransaction);
        var begin = world.Calls.IndexOf("begin");
        Assert.Equal("fence:exclusive", world.Calls[begin + 1]);
        Assert.True(world.Calls.IndexOf("target") > begin + 1, "The participant must be read after the fence.");
        Assert.True(world.Calls.IndexOf("erase") > world.Calls.IndexOf("target"));
        Assert.DoesNotContain("fence:writer", world.Calls);
        Assert.True(world.Calls.LastIndexOf("audit") < world.Calls.IndexOf("commit"));
        Assert.True(world.Calls.LastIndexOf("exclusion:add") < world.Calls.IndexOf("commit"));
    }

    /// <summary>FR-006, VR-004: the store receives the participant's email normalized as the Workspace normalization does.</summary>
    [Fact]
    public async Task TheStore_ReceivesTheIdTheGoogleUserIdAndTheNormalizedEmail()
    {
        var world = WorldWithTarget();

        await EraseAsync(world);

        var erasure = Assert.Single(world.Store.Erasures);
        Assert.Equal((ParticipantId, GoogleUserId, NormalizedEmail), erasure);
    }

    /// <summary>AC-002, FR-008, FR-013: the result carries the committed counts.</summary>
    [Fact]
    public async Task ASucceededErasure_ReturnsErased_WithTheStoresCounts()
    {
        var world = WorldWithTarget();

        var result = await EraseAsync(world);

        Assert.Equal(ParticipantErasureOutcome.Erased, result.Outcome);
        Assert.Equal(Counts, result.Counts);
    }

    /// <summary>AC-006, FR-008, FR-010: one exclusion — the Google user id, the salted fingerprint of the email, the clock's instant.</summary>
    [Fact]
    public async Task TheExclusion_HoldsTheGoogleUserId_TheFingerprintWithTheSalt_AndTheInstant()
    {
        var world = WorldWithTarget();

        await EraseAsync(world);

        var exclusion = Assert.Single(world.Exclusions.Added);
        Assert.Equal(GoogleUserId, exclusion.GoogleUserId);
        Assert.Equal(world.Time.GetUtcNow(), exclusion.ErasedAt);
        var salt = Assert.IsType<ErasureSalt>(world.Salts.Salt);
        Assert.Equal(EmailFingerprint.Of(NormalizedEmail, salt).Bytes.ToArray(), exclusion.EmailFingerprint);
        Assert.Equal(ErasureTestData.Fingerprint(NormalizedEmail, salt.Value), exclusion.EmailFingerprint);
    }

    /// <summary>FR-010, db-design §3.3: an existing salt is reused, never replaced.</summary>
    [Fact]
    public async Task AnExistingSalt_IsReused()
    {
        var world = WorldWithTarget();
        var salt = ErasureSalt.Generate();
        world.Salts.Salt = salt;

        await EraseAsync(world);

        Assert.Same(salt, world.Salts.Salt);
        Assert.Equal(0, world.Salts.Creations);
        Assert.Equal(ErasureTestData.Fingerprint(NormalizedEmail, salt.Value), Assert.Single(world.Exclusions.Added).EmailFingerprint);
    }

    /// <summary>AC-009, FR-013: one succeeded audit row — the Admin, the participant's internal id and the five counts.</summary>
    [Fact]
    public async Task ASucceededErasure_AddsOneAuditRow_WithTheParticipantIdAndTheCounts()
    {
        var world = WorldWithTarget();

        await EraseAsync(world);

        var audit = Assert.Single(world.Audit.Added);
        Assert.Equal(AuditAction.ParticipantErased, audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Null(audit.RefusalCategory);
        Assert.Equal(ErasureUnitWorld.AdminId, audit.ActorId);
        Assert.Equal(AppRole.Admin, audit.ActorRole);
        Assert.Equal(AuditTargetType.ClassroomParticipant, audit.TargetType);
        Assert.Equal(ParticipantId, audit.TargetId);
        Assert.Equal(Counts.Memberships, audit.ErasedMemberships);
        Assert.Equal(Counts.Submissions, audit.ErasedSubmissions);
        Assert.Equal(Counts.MeetParticipations, audit.ErasedMeetParticipations);
        Assert.Equal(Counts.ClearedMeetOrganizers, audit.ClearedMeetOrganizers);
        Assert.Equal(Counts.ClearedCourseOwners, audit.ClearedCourseOwners);
        Assert.Equal("req-1", audit.RequestId);
    }

    /// <summary>AC-019, OD-001 a: no email — no Meet row is matched and the exclusion has no fingerprint.</summary>
    [Fact]
    public async Task AParticipantWithNoEmail_IsErased_WithNoMeetMatch_AndAnExclusionWithoutFingerprint()
    {
        var world = WorldWithTarget(email: null);

        var result = await EraseAsync(world);

        Assert.Equal(ParticipantErasureOutcome.Erased, result.Outcome);
        Assert.Null(Assert.Single(world.Store.Erasures).NormalizedEmail);
        var exclusion = Assert.Single(world.Exclusions.Added);
        Assert.Equal(GoogleUserId, exclusion.GoogleUserId);
        Assert.Null(exclusion.EmailFingerprint);
    }

    /// <summary>AC-015, FR-016: no participant with that id — not found, nothing deleted, no exclusion, no audit row.</summary>
    [Fact]
    public async Task AnUnknownParticipant_IsNotFound_AndNothingIsWritten()
    {
        var world = new ErasureUnitWorld();

        var result = await EraseAsync(world);

        Assert.Equal(ParticipantErasureOutcome.ParticipantNotFound, result.Outcome);
        Assert.Empty(world.Store.Erasures);
        Assert.Empty(world.Exclusions.Added);
        Assert.Empty(world.Audit.Added);
        Assert.False(world.Work.Committed);
    }

    /// <summary>FR-016: the row vanished between the read and the delete (a concurrent erasure) — not found, no second exclusion.</summary>
    [Fact]
    public async Task AParticipantErasedMeanwhile_IsNotFound_WithNoExclusionAndNoAuditRow()
    {
        var world = WorldWithTarget();
        world.Store.Answer = null;

        var result = await EraseAsync(world);

        Assert.Equal(ParticipantErasureOutcome.ParticipantNotFound, result.Outcome);
        Assert.Empty(world.Exclusions.Added);
        Assert.Empty(world.Audit.Added);
        Assert.False(world.Work.Committed);
    }

    /// <summary>AC-002, AD-9: a failure inside the transaction propagates; nothing is committed.</summary>
    [Fact]
    public async Task AFailureInsideTheTransaction_Propagates_AndNothingIsCommitted()
    {
        var world = WorldWithTarget();
        world.Work.FailSaveInsideTransaction = new InvalidOperationException("synthetic commit failure");

        await Assert.ThrowsAsync<InvalidOperationException>(() => EraseAsync(world));

        Assert.True(world.Work.RolledBack);
        Assert.False(world.Work.Committed);
    }
}
