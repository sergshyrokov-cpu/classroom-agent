using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-048 AC-005…AC-009, FR-003…FR-007: a Dean sign-in whose save loses a race with another write to the same
/// account is run again on a fresh read, at most three times, and leaves exactly one audit row. Proved against real
/// PostgreSQL and the real concurrency token (TC-2, spec §9). The race is not left to timing: the attempt under test
/// reads the account through <see cref="InterleavingAppUserRepository"/>, and the competing write lands, complete,
/// in its own scope between that read and the attempt's save.
/// </summary>
public sealed class DeanSignInConcurrencyTests(PostgreSqlFixture database)
{
    private const string Dean = DeanAccountTestData.DeanEmail;

    private const string Wrong = DeanAccountTestData.WrongPassword;

    private const string Correct = DeanAccountTestData.TemporaryPassword;

    /// <summary>
    /// AC-005: two wrong passwords at the same moment both get the common refusal (the wrong-password outcome the
    /// page shows as the common message), never an exception; each writes its own "wrong password" row; the count is 2.
    /// </summary>
    [Fact]
    public async Task TwoWrongPasswordsAtOnce_BothGetTheCommonRefusal_AndTheCountIsTwo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ReadOnlyModeHost.Cause.NotReadOnly, ct);
        await host.InsertDeanAsync(ct);
        DeanSignInOutcome? competing = null;

        var outcome = await SignInInterleavedAsync(
            host,
            Wrong,
            async read =>
            {
                if (read == 1)
                {
                    competing = await SignInAsync(host, Wrong, "r-competing", ct);
                }
            },
            ct);

        Assert.Equal(DeanSignInResult.WrongPassword, outcome.Result);
        Assert.Equal(DeanSignInResult.WrongPassword, competing?.Result);
        Assert.Equal(2, outcome.Repository.ReadsByEmail);
        var rows = await SignInRowsAsync(host, ct);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal("wrong_password", row.RefusalCategory));
        Assert.Equal(["r-competing", "r-under-test"], rows.Select(r => r.RequestId).ToArray());
        var dean = await DeanRowAsync(host, ct);
        Assert.Equal(2, dean.AccessFailedCount);
        Assert.Null(dean.LockoutEnd);
    }

    /// <summary>
    /// AC-006 (I-3): with four failures already, two wrong passwords at once — the one that saves first is the fifth
    /// failure and starts the lockout; the re-run of the other meets that lockout at step 2 and is refused "locked out"
    /// without counting. The count is zero and sign-in is locked.
    /// </summary>
    [Fact]
    public async Task AConflictAtTheFifthFailure_EndsInTheLockoutRefusal()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ReadOnlyModeHost.Cause.NotReadOnly, ct);
        await host.InsertDeanAsync(ct);
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            await SignInAsync(host, Wrong, "r-before-" + attempt, ct);
        }

        DeanSignInOutcome? competing = null;

        var outcome = await SignInInterleavedAsync(
            host,
            Wrong,
            async read =>
            {
                if (read == 1)
                {
                    competing = await SignInAsync(host, Wrong, "r-competing", ct);
                }
            },
            ct);

        Assert.Equal(DeanSignInResult.WrongPassword, competing?.Result);
        Assert.Equal(DeanSignInResult.LockedOut, outcome.Result);
        var rows = await SignInRowsAsync(host, ct);
        Assert.Equal(6, rows.Count);
        Assert.Equal("wrong_password", Assert.Single(rows, r => r.RequestId == "r-competing").RefusalCategory);
        Assert.Equal("locked_out", Assert.Single(rows, r => r.RequestId == "r-under-test").RefusalCategory);
        var dean = await DeanRowAsync(host, ct);
        Assert.Equal(0, dean.AccessFailedCount);
        Assert.Equal(host.Time.GetUtcNow() + SignInDeanUseCase.LockoutDuration, dean.LockoutEnd);
    }

    /// <summary>
    /// AC-007 (I-2): the attempt read the account before an Admin disabled it; its save conflicts with the disable, the
    /// re-run sees the disabled account, and with the correct password the outcome is "account disabled" — one sign-in
    /// row, no success recorded, the count untouched.
    /// </summary>
    [Fact]
    public async Task TheReRun_SeesWhatTheOtherWriterSaved()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ReadOnlyModeHost.Cause.NotReadOnly, ct);
        await host.InsertAppUserAsync(ct);
        await host.InsertDeanAsync(ct);
        var users = await host.AppUsersAsync(ct);
        var adminId = Assert.Single(users, u => u.Role == "admin").Id;
        var deanId = Assert.Single(users, u => u.Role == "dean").Id;

        var outcome = await SignInInterleavedAsync(
            host,
            Correct,
            async read =>
            {
                if (read == 1)
                {
                    using var scope = host.CreateScope();
                    var disable = scope.ServiceProvider.GetRequiredService<SetDeanAccountStateUseCase>();
                    await disable.ExecuteAsync(adminId, deanId, disable: true, "r-admin", ct);
                }
            },
            ct);

        Assert.Equal(DeanSignInResult.AccountDisabled, outcome.Result);
        var row = Assert.Single(await SignInRowsAsync(host, ct));
        Assert.Equal("r-under-test", row.RequestId);
        Assert.Equal("refused", row.Outcome);
        Assert.Equal("account_disabled", row.RefusalCategory);
        var dean = await DeanRowAsync(host, ct);
        Assert.True(dean.IsDisabled);
        Assert.Null(dean.LastSuccessfulSignInAt);
        Assert.Equal(0, dean.AccessFailedCount);
    }

    /// <summary>
    /// AC-008 (I-4): when every save conflicts, the sequence runs three times and the third conflict leaves the use
    /// case as <see cref="AppUserConflictException"/> — never a provider exception — with no sign-in row and no change
    /// of the account saved.
    /// </summary>
    [Fact]
    public async Task ThreeConflictsInARow_AreNotHidden()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ReadOnlyModeHost.Cause.NotReadOnly, ct);
        await host.InsertDeanAsync(ct);
        InterleavingAppUserRepository? repository = null;

        await Assert.ThrowsAsync<AppUserConflictException>(() => SignInInterleavedAsync(
            host,
            Wrong,
            _ => TouchDeanRowAsync(host, ct),
            ct,
            created => repository = created));

        Assert.Equal(3, repository?.ReadsByEmail);
        Assert.Empty(await SignInRowsAsync(host, ct));
        var dean = await DeanRowAsync(host, ct);
        Assert.Equal(0, dean.AccessFailedCount);
        Assert.Null(dean.LockoutEnd);
    }

    /// <summary>
    /// FR-004: two conflicts are absorbed — the third run saves, and the request still writes exactly one row.
    /// </summary>
    [Fact]
    public async Task TwoConflicts_TheThirdRunSaves()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ReadOnlyModeHost.Cause.NotReadOnly, ct);
        await host.InsertDeanAsync(ct);

        var outcome = await SignInInterleavedAsync(
            host,
            Wrong,
            read => read <= 2 ? TouchDeanRowAsync(host, ct) : Task.CompletedTask,
            ct);

        Assert.Equal(DeanSignInResult.WrongPassword, outcome.Result);
        Assert.Equal(3, outcome.Repository.ReadsByEmail);
        var row = Assert.Single(await SignInRowsAsync(host, ct));
        Assert.Equal("wrong_password", row.RefusalCategory);
        Assert.Equal(1, (await DeanRowAsync(host, ct)).AccessFailedCount);
    }

    /// <summary>
    /// AC-009 (I-5): the re-run of a wrong password on an unchanged hash does not verify the password again — one
    /// verification for the request, as an unknown login costs.
    /// </summary>
    [Fact]
    public async Task AReRunOnAnUnchangedHash_DoesNotVerifyAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ReadOnlyModeHost.Cause.NotReadOnly, ct);
        await host.InsertDeanAsync(ct);

        var outcome = await SignInInterleavedAsync(
            host,
            Wrong,
            async read =>
            {
                if (read == 1)
                {
                    await SignInAsync(host, Wrong, "r-competing", ct);
                }
            },
            ct);

        Assert.Equal(DeanSignInResult.WrongPassword, outcome.Result);
        Assert.Equal(2, outcome.Repository.ReadsByEmail);
        Assert.Equal(1, outcome.Hasher.Verifications);
    }

    /// <summary>
    /// AC-009 (I-5): when an Admin reset changed the hash between the runs, the re-run verifies against the new hash.
    /// </summary>
    [Fact]
    public async Task AReRunOnAChangedHash_VerifiesAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ReadOnlyModeHost.Cause.NotReadOnly, ct);
        await host.InsertAppUserAsync(ct);
        await host.InsertDeanAsync(ct);
        var users = await host.AppUsersAsync(ct);
        var adminId = Assert.Single(users, u => u.Role == "admin").Id;
        var deanId = Assert.Single(users, u => u.Role == "dean").Id;

        var outcome = await SignInInterleavedAsync(
            host,
            Wrong,
            async read =>
            {
                if (read == 1)
                {
                    using var scope = host.CreateScope();
                    var reset = scope.ServiceProvider.GetRequiredService<ResetDeanPasswordUseCase>();
                    await reset.ExecuteAsync(adminId, deanId, DeanAccountTestData.NewPassword, "r-admin", ct);
                }
            },
            ct);

        Assert.Equal(DeanSignInResult.WrongPassword, outcome.Result);
        Assert.Equal(2, outcome.Repository.ReadsByEmail);
        Assert.Equal(2, outcome.Hasher.Verifications);
        var row = Assert.Single(await SignInRowsAsync(host, ct));
        Assert.Equal("wrong_password", row.RefusalCategory);
        Assert.Equal(1, (await DeanRowAsync(host, ct)).AccessFailedCount);
    }

    /// <summary>
    /// BR-026, spec §7: the re-run is the same sign-in bookkeeping, permitted in read-only mode — two wrong passwords
    /// at once still both get the common refusal while the installation is suspended.
    /// </summary>
    [Fact]
    public async Task InReadOnlyMode_TheReRunStillSaves()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ReadOnlyModeHost.Cause.Suspended, ct);
        await host.InsertDeanAsync(ct);

        var outcome = await SignInInterleavedAsync(
            host,
            Wrong,
            async read =>
            {
                if (read == 1)
                {
                    await SignInAsync(host, Wrong, "r-competing", ct);
                }
            },
            ct);

        Assert.Equal(DeanSignInResult.WrongPassword, outcome.Result);
        Assert.Equal(2, (await SignInRowsAsync(host, ct)).Count);
        Assert.Equal(2, (await DeanRowAsync(host, ct)).AccessFailedCount);
    }

    /// <summary>
    /// FR-007: a request cancelled while its first run is in flight is not run again, and the conflicting run leaves
    /// no row behind.
    /// </summary>
    [Fact]
    public async Task ACancelledRequest_IsNotRunAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ReadOnlyModeHost.Cause.NotReadOnly, ct);
        await host.InsertDeanAsync(ct);
        using var request = CancellationTokenSource.CreateLinkedTokenSource(ct);
        InterleavingAppUserRepository? repository = null;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SignInInterleavedAsync(
            host,
            Wrong,
            async read =>
            {
                await TouchDeanRowAsync(host, ct);
                await request.CancelAsync();
            },
            request.Token,
            created => repository = created));

        Assert.Equal(1, repository?.ReadsByEmail);
        Assert.Empty(await SignInRowsAsync(host, ct));
    }

    private Task<InstallationTestHost> StartAsync(ReadOnlyModeHost.Cause cause, CancellationToken ct) =>
        ReadOnlyModeHost.StartAsync(database, cause, ct);

    /// <summary>A complete sign-in in its own scope, as a separate request would make it.</summary>
    private static async Task<DeanSignInOutcome> SignInAsync(
        InstallationTestHost host,
        string password,
        string requestId,
        CancellationToken ct)
    {
        using var scope = host.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SignInDeanUseCase>()
            .ExecuteAsync(Dean, password, requestId, ct);
    }

    /// <summary>
    /// The attempt under test: the production use case built from one request scope's own ports, with the account
    /// repository wrapped for the interleaving and the hasher wrapped for counting.
    /// </summary>
    private static async Task<InterleavedOutcome> SignInInterleavedAsync(
        InstallationTestHost host,
        string password,
        Func<int, Task> afterRead,
        CancellationToken ct,
        Action<InterleavingAppUserRepository>? onCreated = null)
    {
        using var scope = host.CreateScope();
        var services = scope.ServiceProvider;
        var repository = new InterleavingAppUserRepository(services.GetRequiredService<IAppUserRepository>(), afterRead);
        var hasher = new CountingPasswordHasher(services.GetRequiredService<IPasswordHasher>());
        onCreated?.Invoke(repository);
        var useCase = new SignInDeanUseCase(
            repository,
            services.GetRequiredService<IAuditEventRepository>(),
            hasher,
            services.GetRequiredService<IUnitOfWork>(),
            services.GetRequiredService<ServiceWriteScope>(),
            services.GetRequiredService<TimeProvider>());
        var outcome = await useCase.ExecuteAsync(Dean, password, "r-under-test", ct);
        return new InterleavedOutcome(outcome.Result, repository, hasher);
    }

    /// <summary>Any other writer of the account: a new concurrency stamp, nothing else changed.</summary>
    private static Task TouchDeanRowAsync(InstallationTestHost host, CancellationToken ct) =>
        host.ExecuteAsync(
            "UPDATE app_user SET concurrency_stamp = @stamp WHERE role = 'dean'",
            ct,
            ("stamp", Guid.NewGuid().ToString("N")));

    private static async Task<IReadOnlyList<InstallationAuditRow>> SignInRowsAsync(InstallationTestHost host, CancellationToken ct) =>
        (await host.AuditRowsAsync(ct)).Where(r => r.Action == "dean_sign_in").ToList();

    private static async Task<AppUserRow> DeanRowAsync(InstallationTestHost host, CancellationToken ct) =>
        Assert.Single(await host.AppUsersAsync(ct), u => u.Role == "dean");

    private sealed record InterleavedOutcome(
        DeanSignInResult Result,
        InterleavingAppUserRepository Repository,
        CountingPasswordHasher Hasher);
}
