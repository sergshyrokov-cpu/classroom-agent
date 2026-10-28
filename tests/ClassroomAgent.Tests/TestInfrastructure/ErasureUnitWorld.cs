using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The US-038 use cases and queries with every port in memory (TC-1). One shared <see cref="Calls"/> log records the
/// order in which the ports were touched, so the tests prove the evaluation order of spec FR-004 and FR-009 — the guard
/// first, the fence first inside the transaction — and what is written and what is not.
/// </summary>
public sealed class ErasureUnitWorld
{
    public const long AdminId = 7;

    public ErasureUnitWorld(bool readOnly = false, LegitimacyModeReason reason = LegitimacyModeReason.SuspendedByOwner)
    {
        Time = new ManualTimeProvider(InstallationTestHost.DefaultStart);
        Guard = new GuardFake(this, readOnly, reason);
        Reads = new ReadSourceFake(this);
        Store = new StoreFake(this);
        Fence = new FenceFake(this);
        Salts = new SaltStoreFake(this);
        Exclusions = new ExclusionRepositoryFake(this);
        Audit = new AuditFake(this);
        Work = new UnitOfWorkFake(this);
    }

    /// <summary>Every port touch, in order: "guard", "begin", "fence:exclusive", "target", "erase", "salt:create", …</summary>
    public List<string> Calls { get; } = [];

    public ManualTimeProvider Time { get; }

    public GuardFake Guard { get; }

    public ReadSourceFake Reads { get; }

    public StoreFake Store { get; }

    public FenceFake Fence { get; }

    public SaltStoreFake Salts { get; }

    public ExclusionRepositoryFake Exclusions { get; }

    public AuditFake Audit { get; }

    public UnitOfWorkFake Work { get; }

    public ServiceWriteScope WriteScope { get; } = new();

    public EraseParticipantUseCase Erase =>
        new(Guard, Reads, Store, Fence, Salts, Exclusions, Audit, Work, WriteScope, Time);

    public LiftErasureExclusionUseCase Lift => new(Guard, Salts, Exclusions, Audit, Work, WriteScope, Time);

    /// <summary>US-065: the entry of an exclusion in advance over the same ports.</summary>
    public EnterErasureExclusionUseCase Enter =>
        new(Guard, Reads, Fence, Salts, Exclusions, Audit, Work, WriteScope, Time);

    public SearchParticipantsForErasureQuery Search => new(Reads);

    /// <summary>The confirmation query over a legitimacy state that is read-only or not.</summary>
    public GetErasureConfirmationQuery Confirmation(bool readOnlyState = false) =>
        new(Reads, new GetLegitimacyModeQuery(new LegitimacyFake(readOnlyState), Time));

    /// <summary>The read-only guard, recording the operations it was asked about.</summary>
    public sealed class GuardFake(ErasureUnitWorld world, bool readOnly, LegitimacyModeReason reason) : IReadOnlyModeGuard
    {
        public List<string> Operations { get; } = [];

        public Task EnsureAllowedAsync(string operation, CancellationToken cancellationToken)
        {
            world.Calls.Add("guard");
            Operations.Add(operation);
            return readOnly ? throw new ReadOnlyModeException(reason, null, operation) : Task.CompletedTask;
        }
    }

    /// <summary>The read source: targets and confirmations by id, and one scripted search page.</summary>
    public sealed class ReadSourceFake(ErasureUnitWorld world) : IParticipantErasureReadSource
    {
        public Dictionary<long, ParticipantErasureTarget> Targets { get; } = [];

        public Dictionary<long, ErasureConfirmationData> Confirmations { get; } = [];

        public List<ParticipantSearchRecord> SearchRows { get; } = [];

        /// <summary>US-065: the participants the "already loaded" lookup answers, whatever the email asked.</summary>
        public List<ParticipantSearchResult> LoadedRows { get; } = [];

        /// <summary>US-065: every email the "already loaded" lookup was asked for, as passed.</summary>
        public List<string> EmailLookups { get; } = [];

        /// <summary>Every search asked for: the term as passed and the limit.</summary>
        public List<(string Term, int Limit)> Searches { get; } = [];

        public Task<ParticipantSearchPage> SearchAsync(string term, int limit, CancellationToken cancellationToken)
        {
            world.Calls.Add("search");
            Searches.Add((term, limit));
            return Task.FromResult(new ParticipantSearchPage(SearchRows.Take(limit).ToList()));
        }

        public Task<ErasureConfirmationData?> GetConfirmationAsync(long participantId, CancellationToken cancellationToken)
        {
            world.Calls.Add("confirmation");
            return Task.FromResult(Confirmations.TryGetValue(participantId, out var data) ? data : null);
        }

        public Task<ParticipantErasureTarget?> GetTargetAsync(long participantId, CancellationToken cancellationToken)
        {
            world.Calls.Add("target");
            return Task.FromResult(Targets.TryGetValue(participantId, out var target) ? target : null);
        }

        public Task<IReadOnlyList<ParticipantSearchResult>> FindParticipantsByEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken)
        {
            world.Calls.Add("loaded");
            EmailLookups.Add(normalizedEmail);
            return Task.FromResult<IReadOnlyList<ParticipantSearchResult>>(LoadedRows.ToList());
        }
    }

    /// <summary>The set-based erasure store: answers the scripted counts (null: the row vanished), or throws.</summary>
    public sealed class StoreFake(ErasureUnitWorld world) : IParticipantErasureStore
    {
        public ErasureCounts? Answer { get; set; }

        public Exception? Failure { get; set; }

        public List<(long ParticipantId, string GoogleUserId, string? NormalizedEmail)> Erasures { get; } = [];

        public Task<ErasureCounts?> EraseAsync(
            long participantId,
            string googleUserId,
            string? normalizedEmail,
            CancellationToken cancellationToken)
        {
            world.Calls.Add("erase");
            Erasures.Add((participantId, googleUserId, normalizedEmail));
            return Failure is { } failure ? Task.FromException<ErasureCounts?>(failure) : Task.FromResult(Answer);
        }
    }

    /// <summary>The fence: records each entry and whether a transaction was open at that moment.</summary>
    public sealed class FenceFake(ErasureUnitWorld world) : IErasureFence
    {
        public List<bool> EnteredInsideTransaction { get; } = [];

        public Task EnterAsWriterAsync(CancellationToken cancellationToken)
        {
            world.Calls.Add("fence:writer");
            EnteredInsideTransaction.Add(world.Work.InTransaction);
            return Task.CompletedTask;
        }

        public Task EnterAsErasureAsync(CancellationToken cancellationToken)
        {
            world.Calls.Add("fence:exclusive");
            EnteredInsideTransaction.Add(world.Work.InTransaction);
            return Task.CompletedTask;
        }
    }

    /// <summary>The salt store: none until a test sets one or the erasure creates it.</summary>
    public sealed class SaltStoreFake(ErasureUnitWorld world) : IErasureSaltStore
    {
        public ErasureSalt? Salt { get; set; }

        public int Creations { get; private set; }

        public Task<ErasureSalt?> FindAsync(CancellationToken cancellationToken)
        {
            world.Calls.Add("salt:find");
            return Task.FromResult(Salt);
        }

        public Task<ErasureSalt> GetOrCreateAsync(CancellationToken cancellationToken)
        {
            world.Calls.Add("salt:create");
            if (Salt is null)
            {
                Salt = ErasureSalt.Generate();
                Creations++;
            }

            return Task.FromResult(Salt);
        }
    }

    /// <summary>The exclusions, in memory; fingerprints compared by their bytes.</summary>
    public sealed class ExclusionRepositoryFake(ErasureUnitWorld world) : IErasedParticipantRepository
    {
        private long _nextId = 100;

        public List<ErasedParticipant> Stored { get; } = [];

        public List<ErasedParticipant> Added { get; } = [];

        public List<ErasedParticipant> Removed { get; } = [];

        public List<EmailFingerprint> FingerprintLookups { get; } = [];

        public void Add(ErasedParticipant exclusion)
        {
            world.Calls.Add("exclusion:add");
            typeof(ErasedParticipant).GetProperty(nameof(ErasedParticipant.Id))!.SetValue(exclusion, _nextId++);
            Added.Add(exclusion);
            Stored.Add(exclusion);
        }

        /// <summary>Seeds a stored exclusion without the use case, with an identity.</summary>
        public ErasedParticipant Seed(ErasedParticipant exclusion)
        {
            typeof(ErasedParticipant).GetProperty(nameof(ErasedParticipant.Id))!.SetValue(exclusion, _nextId++);
            Stored.Add(exclusion);
            return exclusion;
        }

        public Task<IReadOnlySet<string>> GetExcludedGoogleUserIdsAsync(
            IReadOnlyCollection<string> googleUserIds,
            CancellationToken cancellationToken)
        {
            IReadOnlySet<string> found = Stored.Select(e => e.GoogleUserId).OfType<string>().Where(googleUserIds.Contains).ToHashSet(StringComparer.Ordinal);
            return Task.FromResult(found);
        }

        public Task<IReadOnlyList<EmailFingerprint>> GetExcludedFingerprintsAsync(
            IReadOnlyCollection<EmailFingerprint> candidates,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<EmailFingerprint> found = candidates.Where(c => Stored.Any(e => Matches(e, c))).ToList();
            return Task.FromResult(found);
        }

        public Task<IReadOnlyList<ErasedParticipant>> GetByFingerprintAsync(
            EmailFingerprint fingerprint,
            CancellationToken cancellationToken)
        {
            world.Calls.Add("exclusion:find");
            FingerprintLookups.Add(fingerprint);
            IReadOnlyList<ErasedParticipant> found = Stored.Where(e => Matches(e, fingerprint)).ToList();
            return Task.FromResult(found);
        }

        public void Remove(ErasedParticipant exclusion)
        {
            world.Calls.Add("exclusion:remove");
            Removed.Add(exclusion);
            Stored.Remove(exclusion);
        }

        public Task<bool> AnyWithFingerprintAsync(EmailFingerprint fingerprint, CancellationToken cancellationToken)
        {
            world.Calls.Add("exclusion:any");
            FingerprintLookups.Add(fingerprint);
            return Task.FromResult(Stored.Any(e => Matches(e, fingerprint)));
        }

        public Task<IReadOnlyList<UnmatchedExclusion>> GetUnmatchedByFingerprintsAsync(
            IReadOnlyCollection<EmailFingerprint> candidates,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The erasure use cases never match by fingerprint (synchronization does).");

        public Task<bool> FillGoogleUserIdAsync(long exclusionId, string googleUserId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The erasure use cases never fill a Google user id (synchronization does).");

        private static bool Matches(ErasedParticipant exclusion, EmailFingerprint fingerprint) =>
            exclusion.EmailFingerprint is { } bytes && bytes.AsSpan().SequenceEqual(fingerprint.Bytes.Span);
    }

    /// <summary>The audit trail, add only.</summary>
    public sealed class AuditFake(ErasureUnitWorld world) : IAuditEventRepository
    {
        public List<AuditEvent> Added { get; } = [];

        public void Add(AuditEvent auditEvent)
        {
            world.Calls.Add("audit");
            Added.Add(auditEvent);
        }
    }

    /// <summary>
    /// The unit of work: logs "begin", "save" and "commit" or "rollback"; records the service write declared at each
    /// save; a test may make a save inside the transaction fail.
    /// </summary>
    public sealed class UnitOfWorkFake(ErasureUnitWorld world) : IUnitOfWork
    {
        /// <summary>US-053: nothing is staged in this double, so there is nothing to discard.</summary>
        public void DiscardUncommittedChanges()
        {
        }

        public bool InTransaction { get; private set; }

        public int Transactions { get; private set; }

        public bool Committed { get; private set; }

        public bool RolledBack { get; private set; }

        /// <summary>The declaration open at each save (null: none), in order.</summary>
        public List<PermittedServiceWrite?> SavesDeclaring { get; } = [];

        public Exception? FailSaveInsideTransaction { get; set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            world.Calls.Add("save");
            SavesDeclaring.Add(world.WriteScope.Current);
            return InTransaction && FailSaveInsideTransaction is { } failure ? Task.FromException(failure) : Task.CompletedTask;
        }

        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
        {
            world.Calls.Add("begin");
            Transactions++;
            InTransaction = true;
            try
            {
                await work(cancellationToken);
                world.Calls.Add("commit");
                Committed = true;
            }
            catch
            {
                world.Calls.Add("rollback");
                RolledBack = true;
                throw;
            }
            finally
            {
                InTransaction = false;
            }
        }
    }

    /// <summary>A legitimacy state: a recent success, active or suspended.</summary>
    private sealed class LegitimacyFake(bool suspended) : ILegitimacyStateRepository
    {
        private readonly LegitimacyState _state = LegitimacyState.FromSuccess(
            InstallationTestHost.DefaultStart - TimeSpan.FromHours(1),
            suspended ? InstallationStatus.Suspended : InstallationStatus.Active,
            CompatibilityState.Supported,
            InstallationTestData.Domain,
            "100000000000000000001");

        public Task<LegitimacyState?> GetAsync(CancellationToken cancellationToken) => Task.FromResult<LegitimacyState?>(_state);

        public Task<LegitimacyState?> GetForReadAsync(CancellationToken cancellationToken) => Task.FromResult<LegitimacyState?>(_state);

        public void Add(LegitimacyState state) => throw new InvalidOperationException("US-038 writes no legitimacy state.");
    }
}
