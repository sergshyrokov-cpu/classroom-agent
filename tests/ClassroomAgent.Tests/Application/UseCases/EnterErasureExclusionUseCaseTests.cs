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
/// US-065 spec FR-002 … FR-005, FR-011 … FR-013, VR-001, VR-002, I-5; AC-001, AC-002, AC-006, AC-007, AC-009, AC-011,
/// AC-014, AC-015, AC-021 in the Application layer with every port substituted (TC-1, TC-5).
/// </summary>
public sealed class EnterErasureExclusionUseCaseTests
{
    private const string Email = "o.koval@school-one.example.test";

    public static TheoryData<LegitimacyModeReason> ReadOnlyReasons => new(
        LegitimacyModeReason.NotYetConfirmed,
        LegitimacyModeReason.SuspendedByOwner,
        LegitimacyModeReason.GracePeriodExpired);

    public static TheoryData<string?> RequiredCases => new(null, string.Empty, "   ");

    public static TheoryData<string> InvalidCases => new("o.koval", "o.koval@@school.test", "@school.test", "o.koval@", "a@b@c.test", "o koval@school.test");

    private static Task<ExclusionEntryResult> EnterAsync(ErasureUnitWorld world, string? email) =>
        world.Enter.ExecuteAsync(ErasureUnitWorld.AdminId, email, "req-3", TestContext.Current.CancellationToken);

    private static ParticipantSearchResult Loaded(long id) =>
        new(id, "Олена Коваль", Email, 0, 2);

    private static void AssertNothingChanged(ErasureUnitWorld world)
    {
        Assert.Empty(world.Exclusions.Added);
        Assert.Empty(world.Audit.Added);
        Assert.DoesNotContain("save", world.Calls);
    }

    // ---------------------------------------------------------------- the exclusion entered in advance (entity)

    /// <summary>FR-003, entity model §1: the fingerprint, no Google user id, the entry instant — nothing else.</summary>
    [Fact]
    public void AnExclusionEnteredInAdvance_HoldsTheFingerprint_NoGoogleUserId_AndTheEntryDate()
    {
        var salt = ErasureSalt.Generate();
        var fingerprint = EmailFingerprint.Of(Email, salt);
        var at = InstallationTestHost.DefaultStart;

        var exclusion = ErasedParticipant.EnterInAdvance(fingerprint, at);

        Assert.Null(exclusion.GoogleUserId);
        Assert.Equal(fingerprint.Bytes.ToArray(), exclusion.EmailFingerprint);
        Assert.Equal(at, exclusion.ErasedAt);
    }

    /// <summary>S-05, BR-076: the entity still has no property for a name or an email.</summary>
    [Fact]
    public void TheExclusion_StillHasNoPropertyForANameOrAnEmail()
    {
        var names = typeof(ErasedParticipant).GetProperties().Select(p => p.Name).ToList();

        Assert.Equal(
            ["CreatedAt", "EmailFingerprint", "ErasedAt", "GoogleUserId", "Id", "UpdatedAt"],
            names.Order(StringComparer.Ordinal));
    }

    // ---------------------------------------------------------------- read-only mode (FR-012, FR-013)

    /// <summary>AC-009, FR-012, FR-013: every BR-025 cause refuses; one refused audit row naming no target, nothing else.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyReasons))]
    public async Task InReadOnlyMode_TheEntryIsRefused_WithOneRefusedAuditRow_AndNothingElse(LegitimacyModeReason reason)
    {
        var world = new ErasureUnitWorld(readOnly: true, reason: reason);

        var refusal = await Assert.ThrowsAsync<ReadOnlyModeException>(() => EnterAsync(world, Email));

        Assert.Equal(reason, refusal.Reason);
        var audit = Assert.Single(world.Audit.Added);
        Assert.Equal(AuditAction.ErasureExclusionEntered, audit.Action);
        Assert.Equal(AuditOutcome.Refused, audit.Outcome);
        Assert.Equal(AuditRefusalCategory.ReadOnlyMode, audit.RefusalCategory);
        Assert.Equal(ErasureUnitWorld.AdminId, audit.ActorId);
        Assert.Equal(AppRole.Admin, audit.ActorRole);
        Assert.Null(audit.TargetType);
        Assert.Null(audit.TargetId);
        Assert.Equal(["guard", "audit", "save"], world.Calls);
        Assert.Equal([PermittedServiceWrite.AuditEvent], world.Work.SavesDeclaring);
        Assert.Empty(world.Exclusions.Added);
        Assert.Null(world.Salts.Salt);
    }

    /// <summary>FR-012, api-design §2.6: the guard runs before the email is validated, under the entry's own operation name.</summary>
    [Fact]
    public async Task InReadOnlyMode_AnInvalidEmail_IsStillARefusal()
    {
        var world = new ErasureUnitWorld(readOnly: true);

        await Assert.ThrowsAsync<ReadOnlyModeException>(() => EnterAsync(world, "not an address"));

        Assert.Equal([EnterErasureExclusionUseCase.Operation], world.Guard.Operations);
        Assert.Equal(ErasureTestData.Operations.Enter, EnterErasureExclusionUseCase.Operation);
    }

    // ---------------------------------------------------------------- validation (VR-001, AC-011)

    /// <summary>AC-011, VR-001: a missing or blank email is required; nothing is read or written.</summary>
    [Theory]
    [MemberData(nameof(RequiredCases))]
    public async Task AMissingEmail_IsRequired_AndNothingIsTouched(string? email)
    {
        var world = new ErasureUnitWorld();

        var result = await EnterAsync(world, email);

        Assert.Equal(ExclusionEntryOutcome.Invalid, result.Outcome);
        Assert.Equal(LiftFieldError.EmailRequired, result.FieldError);
        Assert.Empty(result.LoadedParticipants);
        Assert.Equal(["guard"], world.Calls);
    }

    /// <summary>AC-011, VR-001: more than 254 characters after trimming is too long.</summary>
    [Fact]
    public async Task AnEmailOver254Characters_IsTooLong()
    {
        var world = new ErasureUnitWorld();
        var email = new string('a', 255 - "@school.test".Length) + "@school.test";

        var result = await EnterAsync(world, email);

        Assert.Equal(ExclusionEntryOutcome.Invalid, result.Outcome);
        Assert.Equal(LiftFieldError.EmailTooLong, result.FieldError);
        Assert.Equal(["guard"], world.Calls);
    }

    /// <summary>AC-011, VR-001: anything but local@domain with exactly one @, non-empty parts, no whitespace is invalid.</summary>
    [Theory]
    [MemberData(nameof(InvalidCases))]
    public async Task AMalformedEmail_IsInvalid(string email)
    {
        var world = new ErasureUnitWorld();

        var result = await EnterAsync(world, email);

        Assert.Equal(ExclusionEntryOutcome.Invalid, result.Outcome);
        Assert.Equal(LiftFieldError.EmailInvalid, result.FieldError);
        Assert.Equal(["guard"], world.Calls);
        Assert.Empty(world.Audit.Added);
    }

    /// <summary>
    /// VR-001 "the two use cases share one validation": every case gives the entry the same field error the lifting gives.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("o.koval")]
    [InlineData("a@b@c.test")]
    [InlineData("o koval@school.test")]
    public async Task TheEntryAndTheLifting_GiveTheSameFieldError(string email)
    {
        var entry = await EnterAsync(new ErasureUnitWorld(), email);
        var lifting = await new ErasureUnitWorld().Lift.ExecuteAsync(
            ErasureUnitWorld.AdminId, email, null, TestContext.Current.CancellationToken);

        Assert.NotNull(entry.FieldError);
        Assert.Equal(lifting.FieldError, entry.FieldError);
    }

    /// <summary>VR-001: exactly 254 characters with surrounding spaces is accepted (trimmed first).</summary>
    [Fact]
    public async Task An254CharacterEmail_WithSurroundingSpaces_IsEntered()
    {
        var world = new ErasureUnitWorld();
        var email = new string('a', 254 - "@school.test".Length) + "@school.test";

        var result = await EnterAsync(world, "  " + email + "  ");

        Assert.Equal(ExclusionEntryOutcome.Entered, result.Outcome);
        Assert.Single(world.Exclusions.Added);
    }

    /// <summary>OD-001 a: the domain need not be the installation's.</summary>
    [Fact]
    public async Task AnEmailOfAnotherDomain_IsEntered()
    {
        var world = new ErasureUnitWorld();

        var result = await EnterAsync(world, "guest@elsewhere.example");

        Assert.Equal(ExclusionEntryOutcome.Entered, result.Outcome);
        Assert.Single(world.Exclusions.Added);
    }

    // ---------------------------------------------------------------- entered (FR-002, FR-003, FR-013; AC-001, AC-002)

    /// <summary>
    /// AC-001, AC-002, FR-002 step 3.2, FR-003: with no salt yet the entry creates it and stores one exclusion holding the
    /// fingerprint keyed with that salt, no Google user id and the entry instant.
    /// </summary>
    [Fact]
    public async Task WithNoSaltYet_TheEntryCreatesIt_AndStoresTheExclusion_WithTheFingerprint_AndNoGoogleUserId()
    {
        var world = new ErasureUnitWorld();

        var result = await EnterAsync(world, Email);

        Assert.Equal(ExclusionEntryOutcome.Entered, result.Outcome);
        Assert.Null(result.FieldError);
        Assert.Empty(result.LoadedParticipants);
        Assert.False(result.LoadedTruncated);
        Assert.Equal(1, world.Salts.Creations);
        var exclusion = Assert.Single(world.Exclusions.Added);
        Assert.Null(exclusion.GoogleUserId);
        Assert.Equal(EmailFingerprint.Of(Email, world.Salts.Salt!).Bytes.ToArray(), exclusion.EmailFingerprint);
        Assert.Equal(world.Time.GetUtcNow(), exclusion.ErasedAt);
    }

    /// <summary>US-038 FR-010: an existing salt is reused, not replaced.</summary>
    [Fact]
    public async Task AnExistingSalt_IsReused()
    {
        var world = new ErasureUnitWorld();
        var salt = ErasureSalt.Generate();
        world.Salts.Salt = salt;

        await EnterAsync(world, Email);

        Assert.Equal(0, world.Salts.Creations);
        Assert.Same(salt, world.Salts.Salt);
        Assert.Equal(EmailFingerprint.Of(Email, salt).Bytes.ToArray(), Assert.Single(world.Exclusions.Added).EmailFingerprint);
    }

    /// <summary>AC-001, FR-013, S-06: one succeeded audit row — the Admin, the new exclusion's internal id, nothing else.</summary>
    [Fact]
    public async Task TheEntry_WritesOneAuditRow_NamingTheNewExclusion()
    {
        var world = new ErasureUnitWorld();

        await EnterAsync(world, Email);

        var exclusion = Assert.Single(world.Exclusions.Added);
        var audit = Assert.Single(world.Audit.Added);
        Assert.Equal(AuditAction.ErasureExclusionEntered, audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Null(audit.RefusalCategory);
        Assert.Equal(ErasureUnitWorld.AdminId, audit.ActorId);
        Assert.Equal(AppRole.Admin, audit.ActorRole);
        Assert.Equal(AuditTargetType.ErasedParticipant, audit.TargetType);
        Assert.Equal(exclusion.Id, audit.TargetId);
        Assert.Equal("req-3", audit.RequestId);
    }

    /// <summary>
    /// FR-002, FR-011, I-6: one transaction; the exclusive fence first inside it; the loaded check after the fence and
    /// before the salt; the existing-exclusion check after the salt; the insert and the audit before the commit.
    /// </summary>
    [Fact]
    public async Task TheEntry_RunsInOneTransaction_FenceFirst_ThenLoaded_ThenSalt_ThenExcluded_ThenTheInsert()
    {
        var world = new ErasureUnitWorld();

        await EnterAsync(world, Email);

        var calls = world.Calls;
        Assert.Equal("guard", calls[0]);
        Assert.Equal(1, world.Work.Transactions);
        Assert.True(world.Work.Committed);
        Assert.Equal([true], world.Fence.EnteredInsideTransaction);
        Assert.Equal(1, calls.Count(c => c == "fence:exclusive"));
        Assert.DoesNotContain("fence:writer", calls);
        Assert.Equal(calls.IndexOf("begin") + 1, calls.IndexOf("fence:exclusive"));
        Assert.True(calls.IndexOf("fence:exclusive") < calls.IndexOf("loaded"));
        Assert.True(calls.IndexOf("loaded") < calls.IndexOf("salt:create"));
        Assert.True(calls.IndexOf("salt:create") < calls.IndexOf("exclusion:any"));
        Assert.True(calls.IndexOf("exclusion:any") < calls.IndexOf("exclusion:add"));
        Assert.True(calls.IndexOf("exclusion:add") < calls.IndexOf("audit"));
        Assert.True(calls.LastIndexOf("save") < calls.IndexOf("commit"));
        Assert.True(calls.IndexOf("audit") < calls.IndexOf("commit"));
    }

    /// <summary>A failure inside the transaction propagates and commits nothing (spec §8).</summary>
    [Fact]
    public async Task AFailureInsideTheTransaction_Propagates_AndNothingIsCommitted()
    {
        var world = new ErasureUnitWorld();
        world.Work.FailSaveInsideTransaction = new InvalidOperationException("synthetic failure");

        await Assert.ThrowsAsync<InvalidOperationException>(() => EnterAsync(world, Email));

        Assert.True(world.Work.RolledBack);
        Assert.False(world.Work.Committed);
    }

    // ---------------------------------------------------------------- already loaded (FR-004; AC-006, AC-014)

    /// <summary>
    /// AC-006, FR-004, I-5: a loaded participant with that email — "already loaded" with them listed; the salt is not
    /// touched, nothing is stored, no audit row.
    /// </summary>
    [Fact]
    public async Task ALoadedParticipantWithTheEmail_IsAnsweredAlreadyLoaded_AndNothingIsStored()
    {
        var world = new ErasureUnitWorld();
        world.Reads.LoadedRows.Add(Loaded(41));
        world.Reads.LoadedRows.Add(Loaded(42));

        var result = await EnterAsync(world, Email);

        Assert.Equal(ExclusionEntryOutcome.AlreadyLoaded, result.Outcome);
        Assert.Null(result.FieldError);
        Assert.Equal([41L, 42L], result.LoadedParticipants.Select(p => p.ParticipantId));
        Assert.Equal(Loaded(41), result.LoadedParticipants[0]);
        Assert.False(result.LoadedTruncated);
        AssertNothingChanged(world);
        Assert.DoesNotContain("salt:create", world.Calls);
        Assert.Null(world.Salts.Salt);
        Assert.True(world.Calls.IndexOf("fence:exclusive") < world.Calls.IndexOf("loaded"));
    }

    /// <summary>I-5: when the person is loaded and the fingerprint is also excluded, "already loaded" wins.</summary>
    [Fact]
    public async Task LoadedAndExcludedAtOnce_IsAnsweredAlreadyLoaded()
    {
        var world = new ErasureUnitWorld();
        var salt = ErasureSalt.Generate();
        world.Salts.Salt = salt;
        world.Exclusions.Seed(ErasedParticipant.Record("100000000000000501", EmailFingerprint.Of(Email, salt), world.Time.GetUtcNow()));
        world.Reads.LoadedRows.Add(Loaded(41));

        var result = await EnterAsync(world, Email);

        Assert.Equal(ExclusionEntryOutcome.AlreadyLoaded, result.Outcome);
        AssertNothingChanged(world);
    }

    /// <summary>AC-014, VR-002: the lookup gets the email trimmed and lower-cased, whatever the Admin typed.</summary>
    [Fact]
    public async Task TheLoadedLookup_ReceivesTheNormalizedEmail()
    {
        var world = new ErasureUnitWorld();

        await EnterAsync(world, "  O.KOVAL@School-One.Example.Test ");

        Assert.Equal([Email], world.Reads.EmailLookups);
    }

    /// <summary>FR-004, api-design §2.5: more than 50 matches show the first 50 and say so.</summary>
    [Fact]
    public async Task MoreThan50LoadedParticipants_Show50_AndAreTruncated()
    {
        var world = new ErasureUnitWorld();
        for (var i = 1; i <= 51; i++)
        {
            world.Reads.LoadedRows.Add(Loaded(i));
        }

        var result = await EnterAsync(world, Email);

        Assert.Equal(ExclusionEntryOutcome.AlreadyLoaded, result.Outcome);
        Assert.Equal(50, result.LoadedParticipants.Count);
        Assert.Equal(Enumerable.Range(1, 50).Select(i => (long)i), result.LoadedParticipants.Select(p => p.ParticipantId));
        Assert.True(result.LoadedTruncated);
        AssertNothingChanged(world);
    }

    // ---------------------------------------------------------------- already excluded (FR-005; AC-007, AC-014)

    /// <summary>AC-007, FR-005: an exclusion left by an erasure with that fingerprint — "already excluded", nothing new.</summary>
    [Fact]
    public async Task AnExclusionFromAnErasure_IsAnsweredAlreadyExcluded()
    {
        var world = new ErasureUnitWorld();
        var salt = ErasureSalt.Generate();
        world.Salts.Salt = salt;
        world.Exclusions.Seed(ErasedParticipant.Record("100000000000000501", EmailFingerprint.Of(Email, salt), world.Time.GetUtcNow()));

        var result = await EnterAsync(world, Email);

        Assert.Equal(ExclusionEntryOutcome.AlreadyExcluded, result.Outcome);
        Assert.Null(result.FieldError);
        Assert.Empty(result.LoadedParticipants);
        AssertNothingChanged(world);
        Assert.Single(world.Exclusions.Stored);
    }

    /// <summary>AC-007, AC-014, FR-005: an exclusion entered in advance, typed again in another case — "already excluded".</summary>
    [Fact]
    public async Task AnExclusionEnteredInAdvance_TypedAgainInAnotherCase_IsAnsweredAlreadyExcluded()
    {
        var world = new ErasureUnitWorld();
        var first = await EnterAsync(world, Email);
        Assert.Equal(ExclusionEntryOutcome.Entered, first.Outcome);
        world.Calls.Clear();
        world.Audit.Added.Clear();
        world.Exclusions.Added.Clear();

        var second = await EnterAsync(world, " O.Koval@SCHOOL-ONE.example.test");

        Assert.Equal(ExclusionEntryOutcome.AlreadyExcluded, second.Outcome);
        AssertNothingChanged(world);
        Assert.Single(world.Exclusions.Stored);
        Assert.Equal(EmailFingerprint.Of(Email, world.Salts.Salt!), Assert.Single(world.Exclusions.FingerprintLookups.Skip(1)));
    }

    /// <summary>FR-005: an exclusion of another address does not count; the entry stores its own.</summary>
    [Fact]
    public async Task AnExclusionOfAnotherAddress_DoesNotCount()
    {
        var world = new ErasureUnitWorld();
        var salt = ErasureSalt.Generate();
        world.Salts.Salt = salt;
        world.Exclusions.Seed(ErasedParticipant.Record("100000000000000502", EmailFingerprint.Of("other@school-one.example.test", salt), world.Time.GetUtcNow()));

        var result = await EnterAsync(world, Email);

        Assert.Equal(ExclusionEntryOutcome.Entered, result.Outcome);
        Assert.Equal(2, world.Exclusions.Stored.Count);
    }

    /// <summary>FR-005, US-038 OD-001: an exclusion without a fingerprint never matches an email.</summary>
    [Fact]
    public async Task AnExclusionWithoutFingerprint_DoesNotCount()
    {
        var world = new ErasureUnitWorld();
        world.Salts.Salt = ErasureSalt.Generate();
        world.Exclusions.Seed(ErasedParticipant.Record("100000000000000503", null, world.Time.GetUtcNow()));

        var result = await EnterAsync(world, Email);

        Assert.Equal(ExclusionEntryOutcome.Entered, result.Outcome);
    }

    // ---------------------------------------------------------------- lifting after an entry (FR-010; AC-008, AC-021)

    /// <summary>
    /// AC-008, AC-021, FR-010: the exclusion entered on an installation with no salt is found and removed by a lifting of
    /// the same email in another case — the salt created by the entry is the one the lifting uses.
    /// </summary>
    [Fact]
    public async Task AfterTheFirstEntry_ALiftingOfTheSameEmail_RemovesIt()
    {
        var world = new ErasureUnitWorld();
        await EnterAsync(world, Email);
        var entered = Assert.Single(world.Exclusions.Added);

        var lifting = await world.Lift.ExecuteAsync(
            ErasureUnitWorld.AdminId, Email.ToUpperInvariant(), null, TestContext.Current.CancellationToken);

        Assert.Equal(new ExclusionLiftingResult(ExclusionLiftingOutcome.Lifted, null, 1), lifting);
        Assert.Equal([entered], world.Exclusions.Removed);
        Assert.Empty(world.Exclusions.Stored);
    }
}
