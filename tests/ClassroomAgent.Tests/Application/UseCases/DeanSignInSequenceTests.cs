using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-012 AC-010: the six steps of SC-2 in order (spec FR-012; <c>trebovaniya.md</c> §2 v66). Each step has its
/// own test for the outcome, the failed-attempt counter and the SC-11 audit category, because the order itself
/// is the security property: step 2 must not verify the password, and step 4 must not touch the counter.
/// </summary>
public sealed class DeanSignInSequenceTests
{
    /// <summary>AC-010 step 1: no account with that email; the row names no actor and no target (SC-11).</summary>
    [Fact]
    public async Task Step1_AnUnknownLogin_IsRefusedAndAuditedWithoutAnIdentifier()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();

        var outcome = await world.SignIn.ExecuteAsync("nobody@school-one.example.test", "whatever it is", "r-1", ct);

        Assert.Equal(DeanSignInResult.UnknownLogin, outcome.Result);
        Assert.Null(outcome.AccountId);
        var row = Assert.Single(world.Audit.Written);
        Assert.Equal(AuditAction.DeanSignIn, row.Action);
        Assert.Equal(AuditOutcome.Refused, row.Outcome);
        Assert.Equal(AuditRefusalCategory.UnknownLogin, row.RefusalCategory);
        Assert.Equal(AuditActorType.Anonymous, row.ActorType);
        Assert.Null(row.ActorId);
        Assert.Null(row.ActorRole);
        Assert.Null(row.TargetType);
        Assert.Null(row.TargetId);
    }

    /// <summary>
    /// AC-010 step 1, SC-2 (security review F-1): an unknown login spends the **same hashing work** as a real
    /// check, so the response time does not reveal whether the login exists. The page is reachable from the
    /// internet and the login is a guessable work email (§2 v62), and the failed-attempt counter never moves
    /// for an unknown login — so without this the account list of a school could be enumerated freely.
    /// </summary>
    [Fact]
    public async Task Step1_SpendsTheSameHashingWorkAsARealCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        world.SeedDean();

        var known = new DeanAccountWorld();
        known.SeedDean();
        await known.SignIn.ExecuteAsync(
            DeanAccountTestData.DeanEmail,
            DeanAccountTestData.WrongPassword,
            "r-1",
            ct);
        var verificationsOnAKnownLogin = known.Hasher.Verified.Count;

        var before = world.Hasher.Verified.Count;
        await world.SignIn.ExecuteAsync("nobody@school-one.example.test", "whatever it is", "r-1", ct);

        Assert.Equal(verificationsOnAKnownLogin, world.Hasher.Verified.Count - before);
    }

    /// <summary>
    /// SC-2 (security review F-3): the dummy hash is computed at most once for the process, not once per
    /// attempt. An instance field would make the unknown-login path cost a hash **plus** a verification while a
    /// real check costs one verification — the same oracle, merely inverted. Two consecutive attempts must
    /// therefore add no hashing work of their own.
    /// </summary>
    [Fact]
    public async Task Step1_DoesNotRehashOnEveryAttempt()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();

        await world.SignIn.ExecuteAsync("nobody@school-one.example.test", "whatever it is", "r-1", ct);
        var afterFirst = world.Hasher.Hashed.Count;
        await world.SignIn.ExecuteAsync("nobody.else@school-one.example.test", "whatever it is", "r-2", ct);

        Assert.Equal(afterFirst, world.Hasher.Hashed.Count);
    }

    /// <summary>
    /// AC-010 step 2, SC-2 v66: a lockout still skips the verification entirely. The equalisation of step 1
    /// must not leak into step 2 — skipping there is the rule, not an oversight.
    /// </summary>
    [Fact]
    public async Task Step2_StillVerifiesNoPassword()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        world.SeedDean();
        await LockOutAsync(world, ct);
        var before = world.Hasher.Verified.Count;

        await world.SignIn.ExecuteAsync(
            DeanAccountTestData.DeanEmail,
            DeanAccountTestData.TemporaryPassword,
            "r-9",
            ct);

        Assert.Equal(before, world.Hasher.Verified.Count);
    }

    /// <summary>
    /// AC-010 step 2, AC-011: while a lockout is in force the attempt is refused **and the password is not
    /// verified** — even the correct one. This is what stops a disabled account being used to test passwords
    /// past the lockout (SC-2 v65, v66).
    /// </summary>
    [Fact]
    public async Task Step2_ALockedOutAccount_IsRefusedWithoutVerifyingThePassword()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();
        await LockOutAsync(world, ct);
        var verifiedBefore = world.Hasher.Verified.Count;

        var outcome = await world.SignIn.ExecuteAsync(
            DeanAccountTestData.DeanEmail,
            DeanAccountTestData.TemporaryPassword,
            "r-9",
            ct);

        Assert.Equal(DeanSignInResult.LockedOut, outcome.Result);
        Assert.Equal(verifiedBefore, world.Hasher.Verified.Count);
        Assert.Equal(dean.Id, world.Audit.Written[^1].ActorId);
        Assert.Equal(AuditRefusalCategory.LockedOut, world.Audit.Written[^1].RefusalCategory);
    }

    /// <summary>AC-010 step 2: a refusal during a lockout does not move the counter further.</summary>
    [Fact]
    public async Task Step2_DoesNotMoveTheCounter()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();
        await LockOutAsync(world, ct);
        var count = dean.AccessFailedCount;

        await world.SignIn.ExecuteAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.WrongPassword, "r-9", ct);

        Assert.Equal(count, dean.AccessFailedCount);
    }

    /// <summary>AC-010 step 3: a wrong password is refused, the counter rises and the category says so.</summary>
    [Fact]
    public async Task Step3_AWrongPassword_IsRefusedAndCounted()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();

        var outcome = await world.SignIn.ExecuteAsync(
            DeanAccountTestData.DeanEmail,
            DeanAccountTestData.WrongPassword,
            "r-1",
            ct);

        Assert.Equal(DeanSignInResult.WrongPassword, outcome.Result);
        Assert.Equal(1, dean.AccessFailedCount);
        var row = Assert.Single(world.Audit.Written);
        Assert.Equal(AuditRefusalCategory.WrongPassword, row.RefusalCategory);
        Assert.Equal(dean.Id, row.ActorId);
        Assert.Equal(AppRole.Dean, row.ActorRole);
    }

    /// <summary>
    /// AC-010 step 4: a disabled account with the **correct** password gets its own outcome, and the counter is
    /// neither incremented nor reset (SC-2 v65).
    /// </summary>
    [Fact]
    public async Task Step4_ADisabledAccountWithTheCorrectPassword_IsToldSo()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();
        await world.SignIn.ExecuteAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.WrongPassword, "r-1", ct);
        await world.SetState.ExecuteAsync(admin.Id, dean.Id, disable: true, "r-2", ct);
        var count = dean.AccessFailedCount;

        var outcome = await world.SignIn.ExecuteAsync(
            DeanAccountTestData.DeanEmail,
            DeanAccountTestData.TemporaryPassword,
            "r-3",
            ct);

        Assert.Equal(DeanSignInResult.AccountDisabled, outcome.Result);
        Assert.Equal(count, dean.AccessFailedCount);
        Assert.Equal(AuditRefusalCategory.AccountDisabled, world.Audit.Written[^1].RefusalCategory);
    }

    /// <summary>
    /// AC-010: a **wrong** password on a disabled account is step 3, not step 4 — the disabled message is
    /// reachable only with the correct password (SC-2 v65, spec S-05).
    /// </summary>
    [Fact]
    public async Task ADisabledAccountWithAWrongPassword_IsAnOrdinaryWrongPassword()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();
        await world.SetState.ExecuteAsync(admin.Id, dean.Id, disable: true, "r-1", ct);

        var outcome = await world.SignIn.ExecuteAsync(
            DeanAccountTestData.DeanEmail,
            DeanAccountTestData.WrongPassword,
            "r-2",
            ct);

        Assert.Equal(DeanSignInResult.WrongPassword, outcome.Result);
        Assert.Equal(AuditRefusalCategory.WrongPassword, world.Audit.Written[^1].RefusalCategory);
    }

    /// <summary>
    /// AC-010: the lockout is checked **before** the disabled state, so a disabled account under a lockout
    /// answers "locked out" even with the correct password (SC-2 v65).
    /// </summary>
    [Fact]
    public async Task ALockedOutDisabledAccount_AnswersLockedOut()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var dean = world.SeedDean();
        await world.SetState.ExecuteAsync(admin.Id, dean.Id, disable: true, "r-1", ct);
        await LockOutAsync(world, ct);

        var outcome = await world.SignIn.ExecuteAsync(
            DeanAccountTestData.DeanEmail,
            DeanAccountTestData.TemporaryPassword,
            "r-9",
            ct);

        Assert.Equal(DeanSignInResult.LockedOut, outcome.Result);
    }

    /// <summary>
    /// AC-012: step 5 — the password is correct but temporary, so the sequence stops before a session and the
    /// forced change comes first (spec FR-006, I-3).
    /// </summary>
    [Fact]
    public async Task Step5_ATemporaryPassword_StopsBeforeASession()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();

        var outcome = await world.SignIn.ExecuteAsync(
            DeanAccountTestData.DeanEmail,
            DeanAccountTestData.TemporaryPassword,
            "r-1",
            ct);

        Assert.Equal(DeanSignInResult.TemporaryPassword, outcome.Result);
        Assert.Equal(dean.Id, outcome.AccountId);
    }

    /// <summary>AC-010 step 6: a correct, non-temporary password signs the Dean in and is audited.</summary>
    [Fact]
    public async Task Step6_ACorrectPassword_SignsTheDeanIn()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();
        await world.ForcedChange.ExecuteAsync(dean.Id, DeanAccountTestData.NewPassword, "r-0", ct);
        world.Audit.Written.ToList().Clear();

        var outcome = await world.SignIn.ExecuteAsync(
            DeanAccountTestData.DeanEmail,
            DeanAccountTestData.NewPassword,
            "r-1",
            ct);

        Assert.Equal(DeanSignInResult.SignedIn, outcome.Result);
        Assert.Equal(dean.Id, outcome.AccountId);
        Assert.Equal(world.Time.GetUtcNow(), dean.LastSuccessfulSignInAt);
        var row = world.Audit.Written[^1];
        Assert.Equal(AuditAction.DeanSignIn, row.Action);
        Assert.Equal(AuditOutcome.Succeeded, row.Outcome);
        Assert.Null(row.RefusalCategory);
    }

    /// <summary>AC-011: a success resets the counter to zero (spec FR-013).</summary>
    [Fact]
    public async Task ASuccess_ResetsTheCounter()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();
        await world.SignIn.ExecuteAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.WrongPassword, "r-1", ct);
        await world.SignIn.ExecuteAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.WrongPassword, "r-2", ct);
        Assert.Equal(2, dean.AccessFailedCount);

        await world.SignIn.ExecuteAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.TemporaryPassword, "r-3", ct);

        Assert.Equal(0, dean.AccessFailedCount);
    }

    /// <summary>AC-010: the login is matched case-insensitively (spec FR-004).</summary>
    [Fact]
    public async Task TheLogin_IsMatchedCaseInsensitively()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        world.SeedDean();

        var outcome = await world.SignIn.ExecuteAsync(
            DeanAccountTestData.DeanEmailMixedCase,
            DeanAccountTestData.TemporaryPassword,
            "r-1",
            ct);

        Assert.Equal(DeanSignInResult.TemporaryPassword, outcome.Result);
    }

    /// <summary>
    /// AC-016: every refusal commits exactly one audit row and nothing else is staged with it (carried US-009
    /// F-2). Five of the nine row shapes of db-design §4.4 are refusals, so this is asserted on the sequence.
    /// </summary>
    [Fact]
    public async Task EachRefusal_CommitsExactlyOneAuditRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        world.SeedDean();

        await world.SignIn.ExecuteAsync("nobody@school-one.example.test", DeanAccountTestData.WrongPassword, "r-1", ct);

        Assert.Single(world.Audit.Written);
        Assert.Equal(1, world.Audit.WrittenAtLastCommit);
    }

    /// <summary>
    /// AC-013: sign-in bookkeeping is one of the service writes BR-026 permits, so the whole sequence works in
    /// read-only mode. The use case consults no guard at all — this test fails if one is added.
    /// </summary>
    [Fact]
    public async Task TheSequence_NeverConsultsTheReadOnlyGuard()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld(readOnly: true);
        var dean = world.SeedDean();

        var outcome = await world.SignIn.ExecuteAsync(
            DeanAccountTestData.DeanEmail,
            DeanAccountTestData.TemporaryPassword,
            "r-1",
            ct);

        Assert.Equal(DeanSignInResult.TemporaryPassword, outcome.Result);
        Assert.Empty(world.ReadOnly.Operations);
        Assert.NotNull(dean.PasswordHash);
    }

    /// <summary>Five consecutive failures, which is what SC-2 locks on.</summary>
    private static async Task LockOutAsync(DeanAccountWorld world, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await world.SignIn.ExecuteAsync(
                DeanAccountTestData.DeanEmail,
                DeanAccountTestData.WrongPassword,
                "r-" + attempt,
                cancellationToken);
        }
    }
}
