using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-012 AC-002, AC-003, AC-009: an Admin creates a Dean account (spec FR-003). The evaluation order is
/// load-bearing — the read-only guard runs before any repository call — and every refusal is an outcome, not an
/// exception (AD-9).
/// </summary>
public sealed class CreateDeanAccountUseCaseTests
{
    private static Task<DeanAccountActionOutcome> CreateAsync(
        DeanAccountWorld world,
        long adminId,
        CancellationToken ct,
        string email = DeanAccountTestData.DeanEmail,
        string password = DeanAccountTestData.TemporaryPassword) =>
        world.Create.ExecuteAsync(adminId, email, password, "r-1", ct);

    /// <summary>AC-002: the account exists, as a Dean, with a hash and a temporary password.</summary>
    [Fact]
    public async Task AValidSubmission_CreatesADeanAccount()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();

        var outcome = await CreateAsync(world, admin.Id, ct);

        Assert.Null(outcome.Refusal);
        var created = Assert.Single(world.Users.Added);
        Assert.Equal(AppRole.Dean, created.Role);
        Assert.Equal(SignInMethod.Password, created.SignInMethod);
        Assert.Equal(DeanAccountTestData.DeanEmail, created.Email);
        Assert.False(created.IsDisabled);
        Assert.True(created.PasswordIsTemporary);
        Assert.Null(created.LastSuccessfulSignInAt);
    }

    /// <summary>AC-002, S-10: the password is stored only as a hash, produced through the port (spec FR-018).</summary>
    [Fact]
    public async Task ThePassword_IsStoredOnlyAsAHash()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();

        await CreateAsync(world, admin.Id, ct);

        var created = Assert.Single(world.Users.Added);
        Assert.Contains(DeanAccountTestData.TemporaryPassword, world.Hasher.Hashed);
        Assert.Equal(DeanAccountWorld.Hashes.Marker + DeanAccountTestData.TemporaryPassword, created.PasswordHash);
        Assert.NotEqual(DeanAccountTestData.TemporaryPassword, created.PasswordHash);
    }

    /// <summary>AC-002: the address is stored lower-cased, so the login is case-insensitive (spec FR-004).</summary>
    [Fact]
    public async Task AMixedCaseAddress_IsStoredLowerCased()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();

        await CreateAsync(world, admin.Id, ct, email: DeanAccountTestData.DeanEmailMixedCase);

        var created = Assert.Single(world.Users.Added);
        Assert.Equal(DeanAccountTestData.DeanEmail, created.Email);
        Assert.Equal(DeanAccountTestData.DeanEmail, created.NormalizedEmail);
    }

    /// <summary>AC-002, AC-016: one audit row, naming the Admin and the new account (spec FR-017).</summary>
    [Fact]
    public async Task TheCreation_IsAudited()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();

        await CreateAsync(world, admin.Id, ct);

        var created = Assert.Single(world.Users.Added);
        var row = Assert.Single(world.Audit.Written);
        Assert.Equal(AuditAction.DeanAccountCreated, row.Action);
        Assert.Equal(AuditOutcome.Succeeded, row.Outcome);
        Assert.Equal(admin.Id, row.ActorId);
        Assert.Equal(AppRole.Admin, row.ActorRole);
        Assert.Equal(AuditTargetType.AppUser, row.TargetType);
        Assert.Equal(created.Id, row.TargetId);
        Assert.Null(row.RefusalCategory);
    }

    /// <summary>AC-002: the account and its audit row land in one transaction (db-design §4.4).</summary>
    [Fact]
    public async Task TheAccountAndItsAuditRow_CommitTogether()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();

        await CreateAsync(world, admin.Id, ct);

        // One transaction, and by the end of it both the account and its audit row are staged. The row names
        // the account, so the identity has to exist first (US-008 db-design §4.4) — the number of commits
        // inside the transaction is an EF Core mechanism, not a requirement.
        Assert.Equal(1, world.Work.Transactions);
        Assert.Equal((1, 1), world.Work.Staged[^1]);
    }

    /// <summary>AC-003: an address outside the school's domain is refused (spec FR-004, VR-001).</summary>
    [Fact]
    public async Task AnAddressOutsideTheSchoolDomain_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();

        var outcome = await CreateAsync(world, admin.Id, ct, email: DeanAccountTestData.OutsideDomainEmail);

        Assert.Equal(DeanAccountRefusal.OutsideSchoolDomain, outcome.Refusal);
        Assert.Empty(world.Users.Added);
    }

    /// <summary>AC-003: a subdomain of the school's domain is not the school's domain (spec FR-004).</summary>
    [Fact]
    public async Task ASubdomainOfTheSchoolDomain_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();

        var outcome = await CreateAsync(world, admin.Id, ct, email: "dean@mail.school-one.example.test");

        Assert.Equal(DeanAccountRefusal.OutsideSchoolDomain, outcome.Refusal);
        Assert.Empty(world.Users.Added);
    }

    /// <summary>AC-003: a free-form string and an empty local part are refused (spec VR-001).</summary>
    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@school-one.example.test")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnAddressThatIsNotAnEmail_IsRefused(string email)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();

        var outcome = await CreateAsync(world, admin.Id, ct, email: email);

        Assert.Equal(DeanAccountRefusal.NotAnEmailAddress, outcome.Refusal);
        Assert.Empty(world.Users.Added);
    }

    /// <summary>
    /// AC-003: an address already in use is refused — whether it belongs to an active Dean, a disabled Dean or
    /// an **Admin**. The three answer identically, so the screen reveals nothing about the other account
    /// (spec VR-001, S-03).
    /// </summary>
    [Fact]
    public async Task AnAddressAlreadyUsedByADean_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        world.SeedDean();

        var outcome = await CreateAsync(world, admin.Id, ct);

        Assert.Equal(DeanAccountRefusal.EmailAlreadyUsed, outcome.Refusal);
        Assert.Empty(world.Users.Added);
    }

    /// <summary>AC-003: the same answer when the existing account is an Admin (spec I-8, S-03).</summary>
    [Fact]
    public async Task AnAddressAlreadyUsedByAnAdmin_IsRefusedTheSameWay()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();

        var outcome = await CreateAsync(world, admin.Id, ct, email: admin.Email);

        Assert.Equal(DeanAccountRefusal.EmailAlreadyUsed, outcome.Refusal);
        Assert.Empty(world.Users.Added);
    }

    /// <summary>AC-003: uniqueness is decided on the normalized form (spec FR-004).</summary>
    [Fact]
    public async Task AnAddressDifferingOnlyInCase_IsAlreadyUsed()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        world.SeedDean();

        var outcome = await CreateAsync(world, admin.Id, ct, email: DeanAccountTestData.DeanEmailMixedCase);

        Assert.Equal(DeanAccountRefusal.EmailAlreadyUsed, outcome.Refusal);
        Assert.Empty(world.Users.Added);
    }

    /// <summary>AC-004: a password that breaks the policy is refused, and the violation is named (spec VR-002).</summary>
    [Fact]
    public async Task APasswordThatBreaksThePolicy_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();

        var outcome = await CreateAsync(world, admin.Id, ct, password: DeanAccountTestData.TooShortPassword);

        Assert.Equal(DeanAccountRefusal.PasswordPolicy, outcome.Refusal);
        Assert.Equal(PasswordPolicyViolation.TooShort, outcome.Violation);
        Assert.Empty(world.Users.Added);
        Assert.Empty(world.Hasher.Hashed);
    }

    /// <summary>
    /// AC-009: in read-only mode the guard refuses before anything is read or written (spec FR-003 step 1,
    /// FR-015; AD-6). The repository is not touched at all.
    /// </summary>
    [Fact]
    public async Task InReadOnlyMode_TheGuardRefusesBeforeAnyRepositoryCall()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld(readOnly: true);
        var admin = world.SeedAdmin();
        var readsBefore = world.Users.Reads;

        await Assert.ThrowsAsync<ReadOnlyModeException>(() => CreateAsync(world, admin.Id, ct));

        Assert.Equal(CreateDeanAccountUseCase.Operation, Assert.Single(world.ReadOnly.Operations));
        Assert.Equal(readsBefore, world.Users.Reads);
        Assert.Empty(world.Users.Added);
        Assert.Empty(world.Hasher.Hashed);
    }

    /// <summary>
    /// AC-009, AC-016: the read-only refusal still writes its own audit row — one of the service writes BR-026
    /// permits — and **exactly one**, with nothing else staged (carried US-009 F-2).
    /// </summary>
    [Fact]
    public async Task InReadOnlyMode_TheRefusalWritesExactlyOneAuditRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld(readOnly: true);
        var admin = world.SeedAdmin();

        await Assert.ThrowsAsync<ReadOnlyModeException>(() => CreateAsync(world, admin.Id, ct));

        var row = Assert.Single(world.Audit.Written);
        Assert.Equal(AuditAction.DeanAccountCreated, row.Action);
        Assert.Equal(AuditOutcome.Refused, row.Outcome);
        Assert.Equal(AuditRefusalCategory.ReadOnlyMode, row.RefusalCategory);
        Assert.Null(row.TargetId);
        Assert.Equal((0, 1), Assert.Single(world.Work.Staged));
    }

    /// <summary>
    /// AC-003: with no confirmed domain the installation is read-only, so the guard answers first and no path
    /// falls back to accepting any domain (spec FR-004, S-04).
    /// </summary>
    [Fact]
    public async Task WithNoConfirmedDomain_NothingFallsBackToAcceptingAnyDomain()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld(readOnly: true, schoolDomain: null);
        var admin = world.SeedAdmin();

        await Assert.ThrowsAsync<ReadOnlyModeException>(() => CreateAsync(world, admin.Id, ct));

        Assert.Empty(world.Users.Added);
    }
}
