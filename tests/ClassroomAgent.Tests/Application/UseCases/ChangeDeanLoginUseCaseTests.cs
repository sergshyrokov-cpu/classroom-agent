using System.Reflection;
using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-067: an Admin changes a Dean's login (spec FR-002 … FR-008). The evaluation order is load-bearing — read-only
/// first, then the account, the address, the school's domain, "the same address", uniqueness — and every refusal is an
/// outcome, not an exception (AD-9). The ports are in memory (TC-1, <see cref="DeanAccountWorld"/>).
/// </summary>
public sealed class ChangeDeanLoginUseCaseTests
{
    private const string NewEmail = "dean.renamed@school-one.example.test";

    private const string NewEmailMixedCase = "  Dean.Renamed@School-One.Example.Test ";

    private const string SubdomainEmail = "dean.renamed@sub.school-one.example.test";

    private static Task<DeanAccountActionOutcome> ChangeAsync(
        DeanAccountWorld world,
        long adminId,
        long deanId,
        string newEmail,
        CancellationToken ct) =>
        world.ChangeLogin.ExecuteAsync(adminId, deanId, newEmail, "r-67", ct);

    /// <summary>Spec §6 row 2: the inputs the creation form also refuses as "not an email".</summary>
    public static TheoryData<string> MalformedAddresses =>
    [
        "",
        "   ",
        "dean.renamed",
        "@school-one.example.test",
        "dean@renamed@school-one.example.test",
        "dean renamed@school-one.example.test",
    ];

    /// <summary>Every refused entry with the refusal creation gives it (spec FR-002 steps 3–4, "same code as creation").</summary>
    public static TheoryData<string, DeanAccountRefusal> RefusedLikeCreation => new()
    {
        { "", DeanAccountRefusal.NotAnEmailAddress },
        { "   ", DeanAccountRefusal.NotAnEmailAddress },
        { "dean.renamed", DeanAccountRefusal.NotAnEmailAddress },
        { "@school-one.example.test", DeanAccountRefusal.NotAnEmailAddress },
        { "dean@renamed@school-one.example.test", DeanAccountRefusal.NotAnEmailAddress },
        { DeanAccountTestData.OutsideDomainEmail, DeanAccountRefusal.OutsideSchoolDomain },
        { SubdomainEmail, DeanAccountRefusal.OutsideSchoolDomain },
    };

    // ---- success (FR-003, FR-007) -------------------------------------------------------------------------------

    /// <summary>AC-001, FR-003: the same account carries the new login, stored lower-cased and trimmed (BR-079).</summary>
    [Fact]
    public async Task AValidChange_GivesTheSameAccountTheNewLogin_LowerCased()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();
        var id = dean.Id;

        var outcome = await ChangeAsync(world, admin.Id, dean.Id, NewEmailMixedCase, ct);

        Assert.Null(outcome.Refusal);
        Assert.Equal(id, outcome.AccountId);
        Assert.Equal(id, dean.Id);
        Assert.Equal(NewEmail, dean.Email);
        Assert.Equal(NewEmail, dean.NormalizedEmail);
        Assert.Empty(world.Users.Added);
    }

    /// <summary>AC-003, FR-003: the security stamp rotates, so an open session ends on its next request (SC-2 v104).</summary>
    [Fact]
    public async Task AValidChange_RotatesTheSecurityStamp()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();
        var stamp = dean.SecurityStamp;

        await ChangeAsync(world, admin.Id, dean.Id, NewEmail, ct);

        Assert.NotEqual(stamp, dean.SecurityStamp);
    }

    /// <summary>
    /// AC-006, FR-003, OD-001 a: a temporary password, a lockout in force and the disabled flag all survive the change,
    /// and so do the hash, the counter, the language and the retention clock.
    /// </summary>
    [Fact]
    public async Task AValidChange_LeavesEverythingElseOfTheAccountUntouched()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();
        var at = world.Time.GetUtcNow();
        dean.RecordFailedSignIn(at, maxAttempts: 1, lockoutFor: TimeSpan.FromHours(1));
        dean.Disable(at);
        var hash = dean.PasswordHash;
        var failed = dean.AccessFailedCount;
        var lockoutEnd = dean.LockoutEnd;
        var language = dean.UiLanguage;
        var lastSignIn = dean.LastSuccessfulSignInAt;
        var createdAt = dean.CreatedAt;
        Assert.True(dean.IsLockedOut(at));

        var outcome = await ChangeAsync(world, admin.Id, dean.Id, NewEmail, ct);

        Assert.Null(outcome.Refusal);
        Assert.Equal(NewEmail, dean.Email);
        Assert.True(dean.PasswordIsTemporary);
        Assert.True(dean.IsDisabled);
        Assert.True(dean.IsLockedOut(at));
        Assert.Equal(hash, dean.PasswordHash);
        Assert.Equal(failed, dean.AccessFailedCount);
        Assert.Equal(lockoutEnd, dean.LockoutEnd);
        Assert.Equal(language, dean.UiLanguage);
        Assert.Equal(lastSignIn, dean.LastSuccessfulSignInAt);
        Assert.Equal(createdAt, dean.CreatedAt);
        Assert.Equal(AppRole.Dean, dean.Role);
        Assert.Equal(SignInMethod.Password, dean.SignInMethod);
    }

    /// <summary>AC-007, FR-007: one audit row — the Admin as actor, the account as object — committed with the change.</summary>
    [Fact]
    public async Task AValidChange_WritesOneAuditRow_InOneTransactionWithTheChange()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();

        await ChangeAsync(world, admin.Id, dean.Id, NewEmail, ct);

        var row = Assert.Single(world.Audit.Written);
        Assert.Equal(AuditAction.DeanLoginChanged, row.Action);
        Assert.Equal(AuditActorType.AppUser, row.ActorType);
        Assert.Equal(admin.Id, row.ActorId);
        Assert.Equal(AppRole.Admin, row.ActorRole);
        Assert.Equal(AuditTargetType.AppUser, row.TargetType);
        Assert.Equal(dean.Id, row.TargetId);
        Assert.Equal(AuditOutcome.Succeeded, row.Outcome);
        Assert.Null(row.RefusalCategory);
        Assert.Equal("r-67", row.RequestId);
        Assert.Equal(1, world.Work.Transactions);
        Assert.Equal(1, world.Work.Commits);
        Assert.Equal(1, world.Audit.WrittenAtLastCommit);
    }

    /// <summary>AC-007, SC-11 v104: no property of the audit row holds the old or the new address.</summary>
    [Fact]
    public async Task TheAuditRow_HoldsNeitherAddress()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();

        await ChangeAsync(world, admin.Id, dean.Id, NewEmail, ct);

        var row = Assert.Single(world.Audit.Written);
        var values = typeof(AuditEvent)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => Convert.ToString(p.GetValue(row), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)
            .ToList();
        Assert.DoesNotContain(values, v => v.Contains(NewEmail, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(values, v => v.Contains(DeanAccountTestData.DeanEmail, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(values, v => v.Contains("dean.renamed", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>AC-001, OD-001 a: a disabled account may have its login changed — the change is not refused.</summary>
    [Fact]
    public async Task ADisabledAccount_CanHaveItsLoginChanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();
        dean.Disable(world.Time.GetUtcNow());

        var outcome = await ChangeAsync(world, admin.Id, dean.Id, NewEmail, ct);

        Assert.Null(outcome.Refusal);
        Assert.Equal(NewEmail, dean.Email);
        Assert.True(dean.IsDisabled);
    }

    // ---- read-only mode (FR-008) ----------------------------------------------------------------------------------

    /// <summary>AC-008, FR-002 step 1: the guard refuses before any repository call and nothing changes (AD-6).</summary>
    [Fact]
    public async Task InReadOnlyMode_TheGuardRefusesBeforeAnyRepositoryCall()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld(readOnly: true);
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();
        var stamp = dean.SecurityStamp;

        await Assert.ThrowsAsync<ReadOnlyModeException>(() => ChangeAsync(world, admin.Id, dean.Id, NewEmail, ct));

        Assert.Equal(0, world.Users.Reads);
        Assert.Single(world.ReadOnly.Operations);
        Assert.Equal(DeanAccountTestData.DeanEmail, dean.Email);
        Assert.Equal(stamp, dean.SecurityStamp);
        Assert.Equal(0, world.Work.Transactions);
    }

    /// <summary>
    /// AC-008, FR-008 (SPEC_REVIEW m-1): the refusal writes exactly one refused audit row with the new action, the
    /// account's id and the read-only category — and nothing else is committed.
    /// </summary>
    [Fact]
    public async Task InReadOnlyMode_TheRefusalWritesOneRefusedAuditRow_WithTheLoginChangeAction()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld(readOnly: true);
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();

        await Assert.ThrowsAsync<ReadOnlyModeException>(() => ChangeAsync(world, admin.Id, dean.Id, NewEmail, ct));

        var row = Assert.Single(world.Audit.Written);
        Assert.Equal(AuditAction.DeanLoginChanged, row.Action);
        Assert.Equal(AuditOutcome.Refused, row.Outcome);
        Assert.Equal(AuditRefusalCategory.ReadOnlyMode, row.RefusalCategory);
        Assert.Equal(admin.Id, row.ActorId);
        Assert.Equal(AppRole.Admin, row.ActorRole);
        Assert.Equal(AuditTargetType.AppUser, row.TargetType);
        Assert.Equal(dean.Id, row.TargetId);
        Assert.Equal(1, world.Audit.WrittenAtLastCommit);
    }

    // ---- the account (FR-002 step 2) ------------------------------------------------------------------------------

    /// <summary>FR-002 step 2: an id that matches nothing is "no such Dean account", and nothing is written.</summary>
    [Fact]
    public async Task AnUnknownId_IsNoSuchDeanAccount()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();

        var outcome = await ChangeAsync(world, admin.Id, 9999, NewEmail, ct);

        Assert.Equal(DeanAccountRefusal.NoSuchDeanAccount, outcome.Refusal);
        Assert.Empty(world.Audit.Written);
        Assert.Equal(0, world.Work.Commits);
    }

    /// <summary>
    /// FR-002 step 2, spec §7, BR-010: an Admin's id is answered as "no such Dean" — an Admin's login is their Google
    /// account and never changes here.
    /// </summary>
    [Fact]
    public async Task AnAdminsId_IsNoSuchDeanAccount_AndTheAdminsLoginStays()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var email = admin.Email;

        var outcome = await ChangeAsync(world, admin.Id, admin.Id, NewEmail, ct);

        Assert.Equal(DeanAccountRefusal.NoSuchDeanAccount, outcome.Refusal);
        Assert.Equal(email, admin.Email);
        Assert.Empty(world.Audit.Written);
    }

    /// <summary>
    /// Entity model §2: the entity's own invariant — an Admin account refuses a login change even if a caller skipped
    /// the use case's check (BR-010).
    /// </summary>
    [Fact]
    public void TheEntity_RefusesToChangeAnAdminsLogin()
    {
        var admin = AppUser.CreateAdmin("admin@school-one.example.test", UiLanguage.Uk, InstallationTestHost.DefaultStart);

        Assert.Throws<InvalidOperationException>(() => admin.ChangeLogin(NewEmail, InstallationTestHost.DefaultStart));
        Assert.Equal("admin@school-one.example.test", admin.Email);
    }

    /// <summary>Entity model §2: a blank login is a programming error at the entity, not a value it stores.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TheEntity_RefusesABlankLogin(string email)
    {
        var dean = AppUser.CreateDean(DeanAccountTestData.DeanEmail, "hashed:x", UiLanguage.Uk, InstallationTestHost.DefaultStart);

        Assert.ThrowsAny<ArgumentException>(() => dean.ChangeLogin(email, InstallationTestHost.DefaultStart));
        Assert.Equal(DeanAccountTestData.DeanEmail, dean.Email);
    }

    // ---- the address (FR-002 steps 3–4) ---------------------------------------------------------------------------

    /// <summary>AC-004, FR-002 step 3: a malformed or empty address is refused and nothing changes.</summary>
    [Theory]
    [MemberData(nameof(MalformedAddresses))]
    public async Task AMalformedAddress_IsRefused_AndNothingChanges(string newEmail)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();
        var stamp = dean.SecurityStamp;

        var outcome = await ChangeAsync(world, admin.Id, dean.Id, newEmail, ct);

        Assert.Equal(DeanAccountRefusal.NotAnEmailAddress, outcome.Refusal);
        AssertNothingChanged(world, dean, stamp);
    }

    /// <summary>AC-004, FR-002 step 4: an address outside the school's domain, or in a subdomain of it, is refused.</summary>
    [Theory]
    [InlineData(DeanAccountTestData.OutsideDomainEmail)]
    [InlineData(SubdomainEmail)]
    [InlineData("dean.renamed@school-one.example.test.evil.example")]
    public async Task AnAddressOutsideTheSchoolDomain_IsRefused_AndNothingChanges(string newEmail)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();
        var stamp = dean.SecurityStamp;

        var outcome = await ChangeAsync(world, admin.Id, dean.Id, newEmail, ct);

        Assert.Equal(DeanAccountRefusal.OutsideSchoolDomain, outcome.Refusal);
        AssertNothingChanged(world, dean, stamp);
    }

    /// <summary>FR-002 step 4: with no school domain known, every address is refused — nothing falls back to "any domain".</summary>
    [Fact]
    public async Task WithNoSchoolDomainKnown_TheAddressIsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld(schoolDomain: null);
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();
        var stamp = dean.SecurityStamp;

        var outcome = await ChangeAsync(world, admin.Id, dean.Id, NewEmail, ct);

        Assert.Equal(DeanAccountRefusal.OutsideSchoolDomain, outcome.Refusal);
        AssertNothingChanged(world, dean, stamp);
    }

    /// <summary>
    /// AC-004, FR-002 ("the same code as creation"): every entry gets exactly the refusal creation gives it, so the two
    /// forms cannot drift apart.
    /// </summary>
    [Theory]
    [MemberData(nameof(RefusedLikeCreation))]
    public async Task EveryRefusedAddress_IsRefusedExactlyAsCreationRefusesIt(string newEmail, DeanAccountRefusal expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();

        var created = await world.Create.ExecuteAsync(admin.Id, newEmail, DeanAccountTestData.TemporaryPassword, "r-1", ct);
        var changed = await ChangeAsync(world, admin.Id, dean.Id, newEmail, ct);

        Assert.Equal(expected, created.Refusal);
        Assert.Equal(created.Refusal, changed.Refusal);
    }

    // ---- the same address (FR-006, OD-002 a) ----------------------------------------------------------------------

    /// <summary>
    /// FR-006, OD-002 a: the account's own address — in any letter case, with spaces around it — is "login unchanged",
    /// and nothing is written: no change, no stamp rotation, no audit row, no commit.
    /// </summary>
    [Theory]
    [InlineData(DeanAccountTestData.DeanEmail)]
    [InlineData(DeanAccountTestData.DeanEmailMixedCase)]
    [InlineData("  dean@school-one.example.test  ")]
    public async Task TheAccountsOwnAddress_IsLoginUnchanged_AndNothingIsWritten(string newEmail)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();
        var stamp = dean.SecurityStamp;

        var outcome = await ChangeAsync(world, admin.Id, dean.Id, newEmail, ct);

        Assert.Equal(DeanAccountRefusal.LoginUnchanged, outcome.Refusal);
        AssertNothingChanged(world, dean, stamp);
    }

    // ---- uniqueness (FR-002 step 6, FR-004) -----------------------------------------------------------------------

    /// <summary>AC-005, FR-002 step 6: another Dean's address, in any letter case, is already in use.</summary>
    [Theory]
    [InlineData(DeanAccountTestData.SecondDeanEmail)]
    [InlineData("Second.Dean@School-One.Example.Test")]
    public async Task AnotherDeansAddress_IsAlreadyUsed_AndNothingChanges(string newEmail)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();
        var other = world.SeedDean(DeanAccountTestData.SecondDeanEmail);
        var stamp = dean.SecurityStamp;

        var outcome = await ChangeAsync(world, admin.Id, dean.Id, newEmail, ct);

        Assert.Equal(DeanAccountRefusal.EmailAlreadyUsed, outcome.Refusal);
        AssertNothingChanged(world, dean, stamp);
        Assert.Equal(DeanAccountTestData.SecondDeanEmail, other.Email);
    }

    /// <summary>AC-005, FR-002 step 6: an Admin's address is in use the same way — uniqueness spans every role.</summary>
    [Fact]
    public async Task AnAdminsAddress_IsAlreadyUsed_AndNothingChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();
        var stamp = dean.SecurityStamp;

        var outcome = await ChangeAsync(world, admin.Id, dean.Id, "ADMIN@school-one.example.test", ct);

        Assert.Equal(DeanAccountRefusal.EmailAlreadyUsed, outcome.Refusal);
        AssertNothingChanged(world, dean, stamp);
    }

    /// <summary>
    /// AC-005, FR-004: another account takes the address after the check — the unique index refuses the save — and the
    /// answer is "already in use", not a failure; nothing is committed.
    /// </summary>
    [Fact]
    public async Task AnAddressTakenBetweenTheCheckAndTheSave_IsAlreadyUsed_AndNothingIsCommitted()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();
        world.Work.NextCommitFailure = new UniqueEmailViolationException(new InvalidOperationException("unique index"));

        var outcome = await ChangeAsync(world, admin.Id, dean.Id, NewEmail, ct);

        Assert.Equal(DeanAccountRefusal.EmailAlreadyUsed, outcome.Refusal);
        Assert.Null(world.Work.NextCommitFailure);
        Assert.Equal(0, world.Work.Commits);
        Assert.Equal(0, world.Audit.WrittenAtLastCommit);
    }

    // ---- order of evaluation (FR-002, I-1) ------------------------------------------------------------------------

    /// <summary>
    /// FR-002, I-1: an unknown account is answered before the address is looked at — a malformed address on an unknown
    /// id is "no such Dean", not "not an email".
    /// </summary>
    [Fact]
    public async Task TheAccountIsCheckedBeforeTheAddress()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();

        var outcome = await ChangeAsync(world, admin.Id, 9999, "not-an-email", ct);

        Assert.Equal(DeanAccountRefusal.NoSuchDeanAccount, outcome.Refusal);
    }

    /// <summary>
    /// FR-002, I-1: the format and the domain are checked before "the same address" — with no school domain known, even
    /// the account's own address is reported as outside the domain, not as unchanged.
    /// </summary>
    [Fact]
    public async Task TheDomainIsCheckedBeforeTheSameAddress()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld(schoolDomain: null);
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();

        var outcome = await ChangeAsync(world, admin.Id, dean.Id, DeanAccountTestData.DeanEmail, ct);

        Assert.Equal(DeanAccountRefusal.OutsideSchoolDomain, outcome.Refusal);
    }

    /// <summary>
    /// FR-002 steps 5–6: the account's own address is "unchanged", never "already in use" — the account does not
    /// collide with itself.
    /// </summary>
    [Fact]
    public async Task TheSameAddressIsCheckedBeforeUniqueness()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();

        var outcome = await ChangeAsync(world, admin.Id, dean.Id, DeanAccountTestData.DeanEmailMixedCase, ct);

        Assert.NotEqual(DeanAccountRefusal.EmailAlreadyUsed, outcome.Refusal);
        Assert.Equal(DeanAccountRefusal.LoginUnchanged, outcome.Refusal);
    }

    // ---- the audit factory (FR-007) -------------------------------------------------------------------------------

    /// <summary>Db-design §3: the success factory accepts the fifth action.</summary>
    [Fact]
    public void TheManagementAuditFactory_AcceptsTheLoginChange()
    {
        var row = AuditEvent.DeanAccountManaged(1, AuditAction.DeanLoginChanged, 2, InstallationTestHost.DefaultStart, "r-1");

        Assert.Equal(AuditAction.DeanLoginChanged, row.Action);
        Assert.Equal(2, row.TargetId);
    }

    private static void AssertNothingChanged(DeanAccountWorld world, AppUser dean, string stamp)
    {
        Assert.Equal(DeanAccountTestData.DeanEmail, dean.Email);
        Assert.Equal(DeanAccountTestData.DeanEmail, dean.NormalizedEmail);
        Assert.Equal(stamp, dean.SecurityStamp);
        Assert.Empty(world.Audit.Written);
        Assert.Equal(0, world.Work.Commits);
        Assert.Empty(world.Users.Added);
    }
}
