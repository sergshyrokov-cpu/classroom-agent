using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-059 AC-011, spec FR-002, VR-002, VR-004: saving the connection with a different technical account records that
/// save's instant as the account's last change; the same account in other letter case or with spaces records nothing,
/// and a first save records nothing. The save's single audit row is unchanged (FR-002.4). The use case is the unit,
/// its ports in memory (TC-1; the <c>SaveWorkspaceConnectionBranchTests</c> precedent).
/// </summary>
public sealed class SaveWorkspaceConnectionAccountChangeTests
{
    private const string Domain = InstallationTestData.Domain;

    private const string AccountA = WorkspaceConnectionTestData.TechnicalAccount;

    private const string AccountB = WorkspaceConnectionTestData.OtherTechnicalAccount;

    private static readonly TimeSpan Later = TimeSpan.FromHours(1);

    /// <summary>
    /// AC-011, FR-002.1, FR-002.2: a first save of A records no change; A again in other letter case and with spaces
    /// records none; B later records that save's instant.
    /// </summary>
    [Fact]
    public async Task OnlyASaveWithAnotherAccount_RecordsItsInstantAsTheChange()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World();

        Assert.True((await world.SaveAsync(AccountA, ct)).IsSaved);
        Assert.Null(world.Stored.TechnicalAccountChangedAt);

        Assert.True((await world.SaveAsync(WorkspaceConnectionTestData.TechnicalAccountMixedCase, ct)).IsSaved);
        Assert.Null(world.Stored.TechnicalAccountChangedAt);

        var changedAt = world.Time.GetUtcNow();
        Assert.True((await world.SaveAsync(AccountB, ct)).IsSaved);
        Assert.Equal(changedAt, world.Stored.TechnicalAccountChangedAt);
        Assert.Equal(AccountB, world.Stored.ImpersonationUserEmail);
    }

    /// <summary>FR-002.2: saving the current account again later keeps the previous change instant.</summary>
    [Fact]
    public async Task SavingTheSameAccountAgain_KeepsThePreviousChangeInstant()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World();
        await world.SaveAsync(AccountA, ct);
        var changedAt = world.Time.GetUtcNow();
        await world.SaveAsync(AccountB, ct);

        await world.SaveAsync(" " + AccountB.ToUpperInvariant() + " ", ct);

        Assert.Equal(changedAt, world.Stored.TechnicalAccountChangedAt);
    }

    /// <summary>FR-002.4, SC-11: every save still writes exactly one <c>WorkspaceConnectionSaved</c> row — the change adds no event.</summary>
    [Fact]
    public async Task AnAccountChange_WritesNoAuditEventOfItsOwn()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World();
        await world.SaveAsync(AccountA, ct);
        await world.SaveAsync(AccountB, ct);

        Assert.Equal(2, world.Audit.Written.Count);
        Assert.All(world.Audit.Written, row =>
        {
            Assert.Equal(AuditAction.WorkspaceConnectionSaved, row.Action);
            Assert.Equal(AuditOutcome.Succeeded, row.Outcome);
        });
    }

    /// <summary>The use case with its ports in memory, the guard permitting the write and a clock that moves between saves.</summary>
    private sealed class World
    {
        private int _requests;

        public World()
        {
            var states = new StateRepository(LegitimacyState.FromSuccess(
                InstallationTestHost.DefaultStart,
                InstallationStatus.Active,
                CompatibilityState.Supported,
                Domain,
                InstallationTestData.ClientId));
            UseCase = new SaveWorkspaceConnectionUseCase(
                Connections,
                Audit,
                new GetWorkspaceConnectionQuery(Connections, states),
                new PermittingGuard(),
                new NoOpUnitOfWork(),
                new ServiceWriteScope(),
                Time);
        }

        public ManualTimeProvider Time { get; } = new(InstallationTestHost.DefaultStart);

        public ConnectionRepository Connections { get; } = new();

        public AuditRepository Audit { get; } = new();

        public SaveWorkspaceConnectionUseCase UseCase { get; }

        public WorkspaceConnection Stored => Assert.Single(Connections.Stored);

        /// <summary>One save by the Admin, then the clock moves on.</summary>
        public async Task<SaveWorkspaceConnectionOutcome> SaveAsync(string email, CancellationToken ct)
        {
            var outcome = await UseCase.ExecuteAsync(7, email, $"r-{++_requests}", ct);
            Time.Advance(Later);
            return outcome;
        }
    }

    private sealed class PermittingGuard : IReadOnlyModeGuard
    {
        public Task EnsureAllowedAsync(string operation, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        /// <summary>US-053: nothing is staged in this double, so there is nothing to discard.</summary>
        public void DiscardUncommittedChanges()
        {
        }

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

        public void Add(LegitimacyState newState) => throw new NotSupportedException("The save writes no legitimacy state.");
    }
}
