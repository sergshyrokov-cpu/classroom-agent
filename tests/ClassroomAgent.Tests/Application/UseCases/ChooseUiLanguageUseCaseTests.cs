using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-039 AC-002, AC-005 in the Application layer (TC-1): the use case validates the code itself (VR-001), changes
/// only the language of the account it is given, and is not a credential change — the security stamp stays
/// (spec FR-004, I-3; db-design §2.1; entity-model §1).
/// </summary>
public sealed class ChooseUiLanguageUseCaseTests
{
    private static ChooseUiLanguageUseCase UseCaseOf(DeanAccountWorld world) =>
        new(world.Users, world.Work, world.WriteScope);

    /// <summary>AC-002: the choice lands on the account, and nothing but the language and its concurrency stamp moves.</summary>
    [Theory]
    [InlineData("en", UiLanguage.En)]
    [InlineData("uk", UiLanguage.Uk)]
    public async Task AValidCode_IsStoredOnThatAccount(string code, UiLanguage expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();
        if (expected == UiLanguage.Uk)
        {
            dean.ChooseUiLanguage(UiLanguage.En);
        }

        var securityStamp = dean.SecurityStamp;
        var passwordHash = dean.PasswordHash;

        var chosen = await UseCaseOf(world).ExecuteAsync(dean.Id, code, ct);

        Assert.Equal(expected, chosen);
        Assert.Equal(expected, dean.UiLanguage);
        Assert.Equal(securityStamp, dean.SecurityStamp);
        Assert.Equal(passwordHash, dean.PasswordHash);
        Assert.Equal(1, world.Work.Commits);
        Assert.Empty(world.Audit.Written);
    }

    /// <summary>AC-002: another account is untouched.</summary>
    [Fact]
    public async Task AnotherAccount_KeepsItsLanguage()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();

        var chosen = await UseCaseOf(world).ExecuteAsync(dean.Id, "en", ct);

        Assert.Equal(UiLanguage.En, chosen);
        Assert.Equal(UiLanguage.En, dean.UiLanguage);
        Assert.Equal(UiLanguage.Uk, admin.UiLanguage);
    }

    /// <summary>AC-001: an Admin chooses as a Dean does — the use case is role-agnostic.</summary>
    [Fact]
    public async Task AnAdmin_CanChooseToo()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();

        var chosen = await UseCaseOf(world).ExecuteAsync(admin.Id, "en", ct);

        Assert.Equal(UiLanguage.En, chosen);
        Assert.Equal(UiLanguage.En, admin.UiLanguage);
    }

    /// <summary>
    /// AC-005, VR-001: anything but exactly <c>uk</c> or <c>en</c> is refused and nothing is committed. Numbers and
    /// other casings are listed on purpose — enum binding would have accepted them (api-design §2.5). Each case is
    /// paired with a valid control in the same world, so the refusal cannot pass because nothing ever commits.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("UK")]
    [InlineData("En")]
    [InlineData(" en")]
    [InlineData("en ")]
    [InlineData("de")]
    [InlineData("ru")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("Uk")]
    [InlineData("uk-UA")]
    [InlineData("english")]
    [InlineData(UiLanguageTestData.RejectedMarker)]
    public async Task AnyOtherValue_IsRefused_AndNothingIsCommitted(string? code)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();
        var concurrencyStamp = dean.ConcurrencyStamp;

        var refused = await UseCaseOf(world).ExecuteAsync(dean.Id, code, ct);

        Assert.Null(refused);
        Assert.Equal(UiLanguage.Uk, dean.UiLanguage);
        Assert.Equal(concurrencyStamp, dean.ConcurrencyStamp);
        Assert.Equal(0, world.Work.Commits);

        var control = await UseCaseOf(world).ExecuteAsync(dean.Id, "en", ct);
        Assert.Equal(UiLanguage.En, control);
        Assert.Equal(1, world.Work.Commits);
    }

    /// <summary>AC-005: an oversized value is refused like any other.</summary>
    [Fact]
    public async Task AnOversizedValue_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();

        var refused = await UseCaseOf(world).ExecuteAsync(dean.Id, new string('e', 4096), ct);
        var control = await UseCaseOf(world).ExecuteAsync(dean.Id, "en", ct);

        Assert.Null(refused);
        Assert.Equal(UiLanguage.En, control);
        Assert.Equal(1, world.Work.Commits);
    }

    /// <summary>FR-004: an account that does not exist is not created and nothing commits.</summary>
    [Fact]
    public async Task AnUnknownAccount_IsRefused_AndNothingIsCommitted()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();

        var refused = await UseCaseOf(world).ExecuteAsync(dean.Id + 1000, "en", ct);
        var control = await UseCaseOf(world).ExecuteAsync(dean.Id, "en", ct);

        Assert.Null(refused);
        Assert.Equal(UiLanguage.En, control);
        Assert.Equal(1, world.Work.Commits);
        Assert.Single(world.Users.Accounts);
    }

    /// <summary>FR-003: choosing the stored language is not an error (idempotent, db-design §2.1).</summary>
    [Fact]
    public async Task TheStoredLanguage_CanBeChosenAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();

        var chosen = await UseCaseOf(world).ExecuteAsync(dean.Id, "uk", ct);

        Assert.Equal(UiLanguage.Uk, chosen);
        Assert.Equal(UiLanguage.Uk, dean.UiLanguage);
    }

    /// <summary>
    /// FR-007: the commit runs under the BR-026 sign-in bookkeeping declaration — read while the commit happens —
    /// and the declaration is closed again afterwards.
    /// </summary>
    [Fact]
    public async Task TheCommit_IsDeclaredAsSignInBookkeeping_AndTheDeclarationCloses()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();
        var recorder = new DeclarationRecordingUnitOfWork(world.WriteScope);

        await new ChooseUiLanguageUseCase(world.Users, recorder, world.WriteScope).ExecuteAsync(dean.Id, "en", ct);

        Assert.Equal([PermittedServiceWrite.SignInBookkeeping], recorder.DeclaredAtCommit);
        Assert.Null(world.WriteScope.Current);
    }

    /// <summary>Entity-model §1: the method sets the language and renews only the concurrency stamp.</summary>
    [Fact]
    public void ChooseUiLanguage_SetsTheLanguage_AndKeepsTheSecurityStamp()
    {
        var dean = AppUser.CreateDean(
            DeanAccountTestData.DeanEmail,
            "hashed:x",
            UiLanguage.Uk,
            InstallationTestHost.DefaultStart);
        var securityStamp = dean.SecurityStamp;
        var concurrencyStamp = dean.ConcurrencyStamp;

        dean.ChooseUiLanguage(UiLanguage.En);

        Assert.Equal(UiLanguage.En, dean.UiLanguage);
        Assert.Equal(securityStamp, dean.SecurityStamp);
        Assert.NotEqual(concurrencyStamp, dean.ConcurrencyStamp);
        Assert.True(dean.PasswordIsTemporary);
        Assert.False(dean.IsDisabled);
    }

    /// <summary>Entity-model §1: an undefined enum value is a programming error, never stored.</summary>
    [Fact]
    public void ChooseUiLanguage_RejectsAnUndefinedValue()
    {
        var admin = AppUser.CreateAdmin("admin@school-one.example.test", UiLanguage.Uk, InstallationTestHost.DefaultStart);

        Assert.Throws<ArgumentOutOfRangeException>(() => admin.ChooseUiLanguage((UiLanguage)42));
        Assert.Equal(UiLanguage.Uk, admin.UiLanguage);
    }

    /// <summary>A unit of work that records which BR-026 declaration was open at each commit.</summary>
    private sealed class DeclarationRecordingUnitOfWork(ServiceWriteScope scope) : ClassroomAgent.Application.Ports.IUnitOfWork
    {
        public List<PermittedServiceWrite?> DeclaredAtCommit { get; } = [];

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            DeclaredAtCommit.Add(scope.Current);
            return Task.CompletedTask;
        }

        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken) =>
            work(cancellationToken);
    }
}
