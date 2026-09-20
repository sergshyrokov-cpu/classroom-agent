using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-009 AC-010, at the level TEST_WRITING could not reach: the <c>DomainNotConfirmed</c> branch of the save,
/// proven **independently of read-only mode**. Over HTTP the two cannot be separated — an installation that
/// never confirmed its legitimacy is always read-only and the guard runs first — so the Specification's
/// "neither hides the other" is satisfied here, with the guard substituted and the ports in memory (TC-1).
/// </summary>
public sealed class SaveWorkspaceConnectionBranchTests
{
    private const string Domain = "school-one.example.test";

    private const string Account = "classroom-agent@school-one.example.test";

    /// <summary>AC-010: no successful check, and the installation is **not** refusing writes.</summary>
    [Fact]
    public async Task WithNoConfirmedDomain_TheSaveIsRefusedForThatReasonAlone()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(legitimacy: null);

        var outcome = await world.UseCase.ExecuteAsync(7, Account, "r-1", ct);

        Assert.Equal(SaveWorkspaceConnectionRefusal.DomainNotConfirmed, outcome.Refusal);
        Assert.Empty(world.Connections.Stored);
    }

    /// <summary>AC-008, AC-010: the refusal writes its own audit row, with its own category.</summary>
    [Fact]
    public async Task TheRefusal_IsAuditedAsDomainNotConfirmed()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(legitimacy: null);

        await world.UseCase.ExecuteAsync(7, Account, "r-1", ct);

        var row = Assert.Single(world.Audit.Written);
        Assert.Equal(AuditAction.WorkspaceConnectionSaved, row.Action);
        Assert.Equal(AuditOutcome.Refused, row.Outcome);
        Assert.Equal(AuditRefusalCategory.DomainNotConfirmed, row.RefusalCategory);
        Assert.Equal(7, row.ActorId);
        Assert.Null(row.TargetId);
    }

    /// <summary>
    /// AC-010: a domain recorded without a successful check — the <c>upgrade_required</c> shape — licenses
    /// nothing, and the refusal is still the domain one rather than a mismatch.
    /// </summary>
    [Fact]
    public async Task ADomainWithoutASuccessfulCheck_IsNotAConfirmedDomain()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(LegitimacyState.FromUpgradeRequired(InstallationStatus.Active, Domain, "100000000000000000001"));

        var outcome = await world.UseCase.ExecuteAsync(7, Account, "r-1", ct);

        Assert.Equal(SaveWorkspaceConnectionRefusal.DomainNotConfirmed, outcome.Refusal);
    }

    /// <summary>AC-003: with a confirmed domain the same call saves, so the branch above is the only difference.</summary>
    [Fact]
    public async Task WithAConfirmedDomain_TheSameCallSaves()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(Confirmed());

        var outcome = await world.UseCase.ExecuteAsync(7, Account, "r-1", ct);

        Assert.True(outcome.IsSaved);
        var stored = Assert.Single(world.Connections.Stored);
        Assert.Equal(Domain, stored.Domain);
        Assert.Equal(Account, stored.ImpersonationUserEmail);
    }

    /// <summary>AC-005: the impersonation refusal is a different branch with a different category.</summary>
    [Fact]
    public async Task AnAddressOutsideTheDomain_IsADifferentRefusal()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(Confirmed());

        var outcome = await world.UseCase.ExecuteAsync(7, "agent@school-two.example.test", "r-1", ct);

        Assert.Equal(SaveWorkspaceConnectionRefusal.ImpersonationDomainMismatch, outcome.Refusal);
        var row = Assert.Single(world.Audit.Written);
        Assert.Equal(AuditRefusalCategory.ImpersonationDomainMismatch, row.RefusalCategory);
    }

    private static LegitimacyState Confirmed() =>
        LegitimacyState.FromSuccess(
            InstallationTestHost.DefaultStart,
            InstallationStatus.Active,
            CompatibilityState.Supported,
            Domain,
            "100000000000000000001");

    /// <summary>The use case with its ports in memory and the guard permitting the write (TC-1).</summary>
    private sealed class World
    {
        public World(LegitimacyState? legitimacy)
        {
            States = new StateRepository(legitimacy);
            var query = new GetWorkspaceConnectionQuery(Connections, States);
            UseCase = new SaveWorkspaceConnectionUseCase(
                Connections,
                Audit,
                query,
                new PermittingGuard(),
                new NoOpUnitOfWork(),
                new ServiceWriteScope(),
                new ManualTimeProvider(InstallationTestHost.DefaultStart));
        }

        public ConnectionRepository Connections { get; } = new();

        public AuditRepository Audit { get; } = new();

        public StateRepository States { get; }

        public SaveWorkspaceConnectionUseCase UseCase { get; }
    }

    private sealed class PermittingGuard : IReadOnlyModeGuard
    {
        public Task EnsureAllowedAsync(string operation, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken) =>
            work(cancellationToken);
    }

    private sealed class ConnectionRepository : IWorkspaceConnectionRepository
    {
        public List<WorkspaceConnection> Stored { get; } = [];

        public Task<WorkspaceConnection?> GetForReadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Stored.FirstOrDefault());

        public Task<WorkspaceConnection?> GetForUpdateAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Stored.FirstOrDefault());

        public void Add(WorkspaceConnection connection) => Stored.Add(connection);
    }

    private sealed class AuditRepository : IAuditEventRepository
    {
        public List<AuditEvent> Written { get; } = [];

        public void Add(AuditEvent auditEvent) => Written.Add(auditEvent);
    }

    private sealed class StateRepository(LegitimacyState? state) : ILegitimacyStateRepository
    {
        public Task<LegitimacyState?> GetAsync(CancellationToken cancellationToken) => Task.FromResult(state);

        public Task<LegitimacyState?> GetForReadAsync(CancellationToken cancellationToken) => Task.FromResult(state);

        public void Add(LegitimacyState newState) => throw new NotSupportedException("The branch tests write no state.");
    }
}
