using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The US-012 use cases with every port in memory (TC-1): the accounts, the audit trail, the unit of work, the
/// password hasher, the read-only guard and the legitimacy state. It exists so the Application-layer tests prove
/// behaviour — evaluation order, refusals, audit rows, what is written and what is not — without an HTTP host.
/// </summary>
public sealed class DeanAccountWorld
{
    public DeanAccountWorld(
        bool readOnly = false,
        string? schoolDomain = DeanAccountTestData.Domain,
        DateTimeOffset? now = null)
    {
        Time = new ManualTimeProvider(now ?? InstallationTestHost.DefaultStart);
        ReadOnly = new Guard(readOnly);
        Users = new AccountRepository();
        Audit = new AuditRepository();
        Work = new UnitOfWork(Audit, Users);
        Hasher = new Hashes();
        WriteScope = new ServiceWriteScope();
        Legitimacy = new LegitimacyRepository(schoolDomain);
    }

    public ManualTimeProvider Time { get; }

    public Guard ReadOnly { get; }

    public AccountRepository Users { get; }

    public AuditRepository Audit { get; }

    public UnitOfWork Work { get; }

    public Hashes Hasher { get; }

    /// <summary>
    /// The BR-026 declaration a use case opens around a write permitted in read-only mode (US-007 FR-005).
    /// The tests read it through what the use cases commit, not directly.
    /// </summary>
    public ServiceWriteScope WriteScope { get; }

    public LegitimacyRepository Legitimacy { get; }

    public CreateDeanAccountUseCase Create =>
        new(ReadOnly, Users, Legitimacy, Audit, Hasher, Work, WriteScope, Time);

    public SetDeanAccountStateUseCase SetState =>
        new(ReadOnly, Users, Audit, Work, WriteScope, Time);

    public ResetDeanPasswordUseCase Reset =>
        new(ReadOnly, Users, Audit, Hasher, Work, WriteScope, Time);

    public SignInDeanUseCase SignIn =>
        new(Users, Audit, Hasher, Work, WriteScope, Time);

    public CompleteTemporaryPasswordChangeUseCase ForcedChange =>
        new(Users, Audit, Hasher, Work, WriteScope, Time);

    public ChangeOwnPasswordUseCase ChangeOwn =>
        new(Users, Audit, Hasher, Work, WriteScope, Time);

    public ListDeanAccountsQuery List => new(Users);

    /// <summary>An Admin already in the table — the actor of every management action.</summary>
    public AppUser SeedAdmin(string email = "admin@school-one.example.test")
    {
        var admin = AppUser.CreateAdmin(email, UiLanguage.Uk, Time.GetUtcNow());
        Users.Seed(admin);
        return admin;
    }

    /// <summary>
    /// A Dean already in the table. It goes through <see cref="AppUser.CreateDean"/> on purpose: seeding an
    /// account any other way would prove a shape the production code does not produce.
    /// </summary>
    public AppUser SeedDean(
        string email = DeanAccountTestData.DeanEmail,
        string password = DeanAccountTestData.TemporaryPassword)
    {
        var dean = AppUser.CreateDean(email, Hasher.Hash(password), UiLanguage.Uk, Time.GetUtcNow());
        Users.Seed(dean);
        return dean;
    }

    /// <summary>The read-only guard, with the cause the tests need and a record of what it was asked.</summary>
    public sealed class Guard(bool readOnly) : IReadOnlyModeGuard
    {
        private readonly List<string> _operations = [];

        public bool IsReadOnly { get; set; } = readOnly;

        /// <summary>Every operation name the guard was asked about, in order.</summary>
        public IReadOnlyList<string> Operations => _operations;

        public Task EnsureAllowedAsync(string operation, CancellationToken cancellationToken)
        {
            _operations.Add(operation);
            return IsReadOnly
                ? throw new ReadOnlyModeException(LegitimacyModeReason.SuspendedByOwner, null, operation)
                : Task.CompletedTask;
        }
    }

    /// <summary>The accounts, in memory, counting the reads so evaluation order can be asserted.</summary>
    public sealed class AccountRepository : IAppUserRepository
    {
        private readonly List<AppUser> _accounts = [];

        private long _nextId = 1;

        public IReadOnlyList<AppUser> Accounts => _accounts;

        public List<AppUser> Added { get; } = [];

        /// <summary>How many times the repository was touched at all (spec FR-003: the guard runs first).</summary>
        public int Reads { get; private set; }

        public void Seed(AppUser user)
        {
            SetId(user, _nextId++);
            _accounts.Add(user);
        }

        public Task<AppUser?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(_accounts.SingleOrDefault(u =>
                string.Equals(u.NormalizedEmail, normalizedEmail, StringComparison.Ordinal)));
        }

        public Task<AppUser?> FindByIdAsync(long id, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(_accounts.SingleOrDefault(u => u.Id == id));
        }

        public Task<AppUser?> FindDeanByIdAsync(long id, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(_accounts.SingleOrDefault(u => u.Id == id && u.Role == AppRole.Dean));
        }

        public Task<IReadOnlyList<AppUser>> ListDeansAsync(CancellationToken cancellationToken)
        {
            Reads++;
            IReadOnlyList<AppUser> deans = _accounts
                .Where(u => u.Role == AppRole.Dean)
                .OrderBy(u => u.NormalizedEmail, StringComparer.Ordinal)
                .ToList();
            return Task.FromResult(deans);
        }

        public Task<AccountSessionState?> GetSessionStateAsync(long id, CancellationToken cancellationToken)
        {
            Reads++;
            var account = _accounts.SingleOrDefault(u => u.Id == id);
            return Task.FromResult(account is null
                ? null
                : new AccountSessionState(account.SecurityStamp, account.IsDisabled));
        }

        public void Add(AppUser user)
        {
            SetId(user, _nextId++);
            _accounts.Add(user);
            Added.Add(user);
        }

        public void Forget(AppUser user) => _accounts.Remove(user);

        /// <summary>
        /// The identity the database would generate. The entity keeps <c>Id</c> private, as it should, so the
        /// test double fills it the way EF Core does — by reflection, not by widening the domain.
        /// </summary>
        private static void SetId(AppUser user, long id) =>
            typeof(AppUser)
                .GetProperty(nameof(AppUser.Id))!
                .SetValue(user, id);
    }

    /// <summary>The audit trail, in memory. Rows are only ever added (SC-11).</summary>
    public sealed class AuditRepository : IAuditEventRepository
    {
        private readonly List<AuditEvent> _written = [];

        /// <summary>Rows staged so far, in order.</summary>
        public IReadOnlyList<AuditEvent> Written => _written;

        /// <summary>Rows that were staged when the last commit happened (carried US-009 F-2).</summary>
        public int WrittenAtLastCommit { get; set; }

        public void Add(AuditEvent auditEvent) => _written.Add(auditEvent);
    }

    /// <summary>Commits, counted, with the transaction boundary honoured.</summary>
    public sealed class UnitOfWork(AuditRepository audit, AccountRepository users) : IUnitOfWork
    {
        public int Commits { get; private set; }

        public int Transactions { get; private set; }

        /// <summary>How many accounts and audit rows were staged at each commit, in order.</summary>
        public List<(int Accounts, int AuditRows)> Staged { get; } = [];

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Commits++;
            Staged.Add((users.Added.Count, audit.Written.Count));
            audit.WrittenAtLastCommit = audit.Written.Count;
            return Task.CompletedTask;
        }

        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
        {
            Transactions++;
            await work(cancellationToken);
        }
    }

    /// <summary>
    /// A hasher that is obviously not a real one, so a test can read it: the "hash" is the password with a
    /// marker. It proves the use cases store a hash and verify through the port, never the password itself.
    /// </summary>
    public sealed class Hashes : IPasswordHasher
    {
        public const string Marker = "hashed:";

        public List<string> Hashed { get; } = [];

        public List<string> Verified { get; } = [];

        public string Hash(string password)
        {
            Hashed.Add(password);
            return Marker + password;
        }

        public bool Verify(string hash, string password)
        {
            Verified.Add(password);
            return string.Equals(hash, Marker + password, StringComparison.Ordinal);
        }
    }

    /// <summary>The legitimacy state, for the school's domain alone (spec FR-004).</summary>
    public sealed class LegitimacyRepository(string? domain) : ILegitimacyStateRepository
    {
        private LegitimacyState? _state = domain is null
            ? null
            : LegitimacyState.FromSuccess(
                InstallationTestHost.DefaultStart,
                InstallationStatus.Active,
                CompatibilityState.Supported,
                domain,
                "100000000000000000001");

        public Task<LegitimacyState?> GetAsync(CancellationToken cancellationToken) => Task.FromResult(_state);

        public Task<LegitimacyState?> GetForReadAsync(CancellationToken cancellationToken) => Task.FromResult(_state);

        public void Add(LegitimacyState state) => _state = state;
    }
}
