using System.Net;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-051 AC-007, FR-005: a first Google sign-in finds no account, and before its Admin insert lands another account
/// with the same address is created. The unique index rejects the insert; the account read again goes through the
/// same role and disabled checks as any existing account. Proved against real PostgreSQL and the real unique index
/// (TC-2). The race is not left to timing: the sign-in reads the account through
/// <see cref="InterleavingAppUserRepository"/>, and the competing account is inserted, committed, between that first
/// read and the Admin insert (the US-048 pattern).
/// </summary>
public sealed class GoogleFirstSignInRaceTests(PostgreSqlFixture database)
{
    private const string Email = SignInTestData.AdminEmail;

    /// <summary>
    /// AC-007: a Dean account created in the meantime is refused as "not an Admin account" and left untouched; no
    /// Admin account exists; exactly one audit row of the AC-002 shape is committed (spec review M-3).
    /// </summary>
    [Fact]
    public async Task ADeanCreatedInTheMeantime_IsRefusedAsNotAnAdminAccount()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        IReadOnlyList<string>? deanAfterCreation = null;

        var outcome = await SignInInterleavedAsync(
            host,
            async read =>
            {
                if (read == 1)
                {
                    await host.InsertDeanAsync(ct, email: Email);
                    deanAfterCreation = await AccountsAsJsonAsync(host, ct);
                }
            },
            ct);

        Assert.Equal(SignInRefusal.NotAnAdminAccount, outcome.Outcome.Refusal);
        Assert.Null(outcome.Outcome.User);
        Assert.Equal(2, outcome.Repository.ReadsByEmail);
        Assert.Equal(deanAfterCreation, await AccountsAsJsonAsync(host, ct));
        var account = Assert.Single(await host.AppUsersAsync(ct));
        Assert.Equal("dean", account.Role);
        var audit = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal("admin_sign_in", audit.Action);
        Assert.Equal("refused", audit.Outcome);
        Assert.Equal("app_user", audit.ActorType);
        Assert.Equal(account.Id, audit.ActorId);
        Assert.Equal("dean", audit.ActorRole);
        Assert.Equal(SignInTestData.RefusalCategories.NotAnAdminAccount, audit.RefusalCategory);
        Assert.Equal("r-under-test", audit.RequestId);
    }

    /// <summary>FR-005: a disabled account created in the meantime is refused as "account disabled", not stamped.</summary>
    [Fact]
    public async Task ADisabledAdminCreatedInTheMeantime_IsRefusedAsDisabled()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        IReadOnlyList<string>? adminAfterCreation = null;

        var outcome = await SignInInterleavedAsync(
            host,
            async read =>
            {
                if (read == 1)
                {
                    await host.InsertAppUserAsync(ct, email: Email, isDisabled: true);
                    adminAfterCreation = await AccountsAsJsonAsync(host, ct);
                }
            },
            ct);

        Assert.Equal(SignInRefusal.AccountDisabled, outcome.Outcome.Refusal);
        Assert.Equal(adminAfterCreation, await AccountsAsJsonAsync(host, ct));
        var audit = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal("refused", audit.Outcome);
        Assert.Equal("admin", audit.ActorRole);
        Assert.Equal(SignInTestData.RefusalCategories.AccountDisabled, audit.RefusalCategory);
    }

    /// <summary>FR-005, FR-009: an enabled Admin created in the meantime is still stamped and signed in, as before.</summary>
    [Fact]
    public async Task AnAdminCreatedInTheMeantime_IsStillSignedIn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);

        var outcome = await SignInInterleavedAsync(
            host,
            async read =>
            {
                if (read == 1)
                {
                    await host.InsertAppUserAsync(ct, email: Email);
                }
            },
            ct);

        Assert.True(outcome.Outcome.Succeeded);
        var account = Assert.Single(await host.AppUsersAsync(ct));
        Assert.Equal("admin", account.Role);
        Assert.Equal(account.Id, outcome.Outcome.User?.Id);
        Assert.Equal(InstallationTestHost.DefaultStart, account.LastSuccessfulSignInAt);
        var audit = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal("succeeded", audit.Outcome);
    }

    private Task<InstallationTestHost> StartAsync(CancellationToken ct) =>
        InstallationTestHost.StartWithControlPlaneHttpAsync(
            database,
            ScriptedHttpHandler.AdminLoginCheckJson(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(true)),
            ct);

    /// <summary>
    /// The sign-in under test: the production use case built from one request scope's own ports, with the account
    /// repository wrapped for the interleaving.
    /// </summary>
    private static async Task<InterleavedOutcome> SignInInterleavedAsync(
        InstallationTestHost host,
        Func<int, Task> afterRead,
        CancellationToken ct)
    {
        using var scope = host.CreateScope();
        var services = scope.ServiceProvider;
        var repository = new InterleavingAppUserRepository(services.GetRequiredService<IAppUserRepository>(), afterRead);
        var useCase = new CompleteGoogleSignInUseCase(
            services.GetRequiredService<IControlPlaneClient>(),
            repository,
            services.GetRequiredService<IAuditEventRepository>(),
            services.GetRequiredService<IUnitOfWork>(),
            services.GetRequiredService<ServiceWriteScope>(),
            services.GetRequiredService<InstallationIdentity>(),
            services.GetRequiredService<SchoolDefaults>(),
            services.GetRequiredService<TimeProvider>());
        var outcome = await useCase.ExecuteAsync(Email, "r-under-test", ct);
        return new InterleavedOutcome(outcome, repository);
    }

    /// <summary>Every account row as its full JSON text, read on a connection of its own (committed data only).</summary>
    private static async Task<IReadOnlyList<string>> AccountsAsJsonAsync(InstallationTestHost host, CancellationToken ct) =>
        await host.QueryAsync("SELECT row_to_json(u)::text FROM app_user u ORDER BY u.id", r => r.GetString(0), ct);

    private sealed record InterleavedOutcome(SignInOutcome Outcome, InterleavingAppUserRepository Repository);
}
