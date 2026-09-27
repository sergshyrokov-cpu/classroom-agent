using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The US-013 run use case with every port in memory (TC-1): the <c>sync_state</c> row, the unit of work, the
/// read-only guard, the stored connection and the legitimacy state. It exists so the Application-layer tests
/// prove behaviour — evaluation order, refusals, what is written and what is not — without an HTTP host.
/// </summary>
public sealed class SyncWorld
{
    public SyncWorld(
        bool readOnly = false,
        SeededConnection connection = SeededConnection.Usable,
        DateTimeOffset? now = null)
    {
        Time = new ManualTimeProvider(now ?? InstallationTestHost.DefaultStart);
        ReadOnly = new Guard(readOnly);
        States = new StateRepository();
        Connections = new ConnectionRepository(connection);
        // The school's domain is known because a check succeeded — which is independent of whether a connection
        // has been saved. Without this, no connection would read as DomainUnknown instead of NotConfigured.
        Legitimacy = new LegitimacyRepository(domainKnown: true);
        Work = new UnitOfWork(States);
    }

    public ManualTimeProvider Time { get; }

    public Guard ReadOnly { get; }

    public StateRepository States { get; }

    public ConnectionRepository Connections { get; }

    public LegitimacyRepository Legitimacy { get; }

    public UnitOfWork Work { get; }

    public GetWorkspaceConnectionQuery ConnectionQuery => new(Connections, Legitimacy);

    public RunSynchronizationUseCase Run => new(States, ConnectionQuery, ReadOnly, Work, Time);

    /// <summary>A run identifier the assertions can recognise.</summary>
    public static Guid RunId(int ordinal) => new($"00000000-0000-0000-0000-{ordinal:D12}");

    /// <summary>The read-only guard, with the cause the tests need and a record of what it was asked.</summary>
    public sealed class Guard(bool readOnly) : IReadOnlyModeGuard
    {
        private readonly List<string> _operations = [];

        public bool IsReadOnly { get; set; } = readOnly;

        /// <summary>Every operation name the guard was asked about, in order (spec FR-005: it runs first).</summary>
        public IReadOnlyList<string> Operations => _operations;

        public Task EnsureAllowedAsync(string operation, CancellationToken cancellationToken)
        {
            _operations.Add(operation);
            return IsReadOnly
                ? throw new ReadOnlyModeException(LegitimacyModeReason.SuspendedByOwner, null, operation)
                : Task.CompletedTask;
        }
    }

    /// <summary>The single <c>sync_state</c> row, in memory, counting what it was asked (db-design §3).</summary>
    public sealed class StateRepository : ISyncStateRepository
    {
        private SyncState? _state;

        /// <summary>The stored row, or null while the installation has never synchronized (spec I-2).</summary>
        public SyncState? Stored => _state;

        /// <summary>Rows staged for insert, in order — exactly one over the life of an installation.</summary>
        public List<SyncState> Added { get; } = [];

        /// <summary>How many times the repository was touched at all (spec FR-005: the guard runs first).</summary>
        public int Reads { get; private set; }

        public Task<SyncState?> GetAsync(CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(_state);
        }

        public Task<SyncState?> GetForReadAsync(CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(_state);
        }

        public void Add(SyncState state)
        {
            SetId(state, 1);
            _state = state;
            Added.Add(state);
        }

        /// <summary>
        /// The identity the database would generate. The entity keeps <c>Id</c> private, as it should, so the
        /// test double fills it the way EF Core does — by reflection, not by widening the domain.
        /// </summary>
        private static void SetId(SyncState state, long id) =>
            typeof(SyncState).GetProperty(nameof(SyncState.Id))!.SetValue(state, id);
    }

    /// <summary>The stored connection, seeded into one of the states of US-009 spec FR-002.</summary>
    public sealed class ConnectionRepository : IWorkspaceConnectionRepository
    {
        private readonly WorkspaceConnection? _connection;

        public ConnectionRepository(SeededConnection seeded)
        {
            _connection = seeded switch
            {
                SeededConnection.None => null,
                SeededConnection.Usable => WorkspaceConnection.Create(
                    InstallationTestData.Domain,
                    AccessCheckTestData.TechnicalAccount),
                SeededConnection.ForAnotherDomain => WorkspaceConnection.Create(
                    InstallationTestData.OtherDomain,
                    "classroom-agent@" + InstallationTestData.OtherDomain),
                _ => throw new ArgumentOutOfRangeException(nameof(seeded), seeded, null),
            };
        }

        public int Reads { get; private set; }

        public Task<WorkspaceConnection?> GetForReadAsync(CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(_connection);
        }

        public Task<WorkspaceConnection?> GetForUpdateAsync(CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(_connection);
        }

        public void Add(WorkspaceConnection connection) =>
            throw new InvalidOperationException("US-013 writes no connection.");
    }

    /// <summary>The legitimacy state, for the school's own domain (US-009 spec FR-003).</summary>
    public sealed class LegitimacyRepository(bool domainKnown) : ILegitimacyStateRepository
    {
        private readonly LegitimacyState? _state = domainKnown
            ? LegitimacyState.FromSuccess(
                InstallationTestHost.DefaultStart - TimeSpan.FromHours(1),
                InstallationStatus.Active,
                CompatibilityState.Supported,
                InstallationTestData.Domain,
                "100000000000000000001")
            : null;

        public Task<LegitimacyState?> GetAsync(CancellationToken cancellationToken) => Task.FromResult(_state);

        public Task<LegitimacyState?> GetForReadAsync(CancellationToken cancellationToken) => Task.FromResult(_state);

        public void Add(LegitimacyState state) =>
            throw new InvalidOperationException("US-013 writes no legitimacy state.");
    }

    /// <summary>Commits, counted, with what was staged at each one (carried US-009 F-2).</summary>
    public sealed class UnitOfWork(StateRepository states) : IUnitOfWork
    {
        public int Commits { get; private set; }

        public int Transactions { get; private set; }

        /// <summary>The status of the row at each commit, in order — the two writes of spec FR-006.</summary>
        public List<SyncRunStatus?> CommittedStatuses { get; } = [];

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Commits++;
            CommittedStatuses.Add(states.Stored?.Status);
            return Task.CompletedTask;
        }

        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
        {
            Transactions++;
            await work(cancellationToken);
        }
    }
}
