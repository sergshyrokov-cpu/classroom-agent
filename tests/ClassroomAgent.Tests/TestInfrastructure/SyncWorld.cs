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

    /// <summary>
    /// The Classroom port, in memory (US-014, TC-4). Empty by default, which keeps every US-013 test true: a run
    /// with no course to import still completes with the counter at zero (US-013 spec I-1).
    /// </summary>
    public ClassroomReader Classroom { get; } = new();

    public CourseRepository Courses { get; } = new();

    public ParticipantRepository Participants { get; } = new();

    public MembershipRepository Memberships { get; } = new();

    public RunSynchronizationUseCase Run =>
        new(States, ConnectionQuery, ReadOnly, Work, Time, Classroom, Courses, Participants, Memberships);

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

    /// <summary>
    /// The Classroom port, answering from what a test seeded (US-014 entity-model §6, TC-4). Every course and
    /// person here is invented; TC-4 forbids a real roster in a fixture.
    /// </summary>
    public sealed class ClassroomReader : IClassroomReader
    {
        private readonly List<CourseSnapshot> _courses = [];
        private readonly Dictionary<string, CourseRoster> _rosters = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Exception> _rosterFailures = new(StringComparer.Ordinal);

        /// <summary>Set to make <see cref="ReadCoursesAsync"/> fail part-way; the courses before it are yielded.</summary>
        public Exception? FailAfterCourses { get; set; }

        /// <summary>The Google course ids whose roster was asked for, in order.</summary>
        public List<string> RostersRead { get; } = [];

        /// <summary>How many times the course list was enumerated at all (the read-only tests assert zero).</summary>
        public int CourseReads { get; private set; }

        /// <summary>Seeds one course and its roster. A roster left null means Classroom returns an empty one.</summary>
        public ClassroomReader WithCourse(
            string googleId,
            string name,
            string state = CourseTestData.States.Active,
            IEnumerable<RosterEntry>? teachers = null,
            IEnumerable<RosterEntry>? students = null,
            string? ownerGoogleId = null,
            DateTimeOffset? creationTime = null,
            DateTimeOffset? updateTime = null,
            string? section = null)
        {
            _courses.Add(new CourseSnapshot(
                googleId,
                state,
                new CourseDetails(
                    name,
                    section,
                    null,
                    null,
                    null,
                    ownerGoogleId,
                    creationTime,
                    updateTime,
                    null,
                    null,
                    null,
                    null)));
            _rosters[googleId] = new CourseRoster(
                (teachers ?? []).ToList(),
                (students ?? []).ToList());
            return this;
        }

        /// <summary>
        /// Makes one course's roster read fail. Spec I-6: a failed read leaves the roster <b>unknown</b>, so no
        /// membership of that course may be marked off the roster.
        /// </summary>
        public ClassroomReader WithRosterFailure(string googleId, Exception failure)
        {
            _rosterFailures[googleId] = failure;
            return this;
        }

        /// <summary>The impersonation address each call was made with (BR-015): the assertions check it is the one the connection holds.</summary>
        public List<string> ImpersonatedAs { get; } = [];

        public async IAsyncEnumerable<CourseSnapshot> ReadCoursesAsync(
            string impersonationUser,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            CourseReads++;
            ImpersonatedAs.Add(impersonationUser);
            foreach (var course in _courses)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return course;
                await Task.Yield();
            }

            if (FailAfterCourses is not null)
            {
                throw FailAfterCourses;
            }
        }

        public Task<CourseRoster> ReadRosterAsync(
            string impersonationUser,
            string courseGoogleId,
            CancellationToken cancellationToken)
        {
            RostersRead.Add(courseGoogleId);
            ImpersonatedAs.Add(impersonationUser);
            return _rosterFailures.TryGetValue(courseGoogleId, out var failure)
                ? Task.FromException<CourseRoster>(failure)
                : Task.FromResult(_rosters.TryGetValue(courseGoogleId, out var roster)
                    ? roster
                    : new CourseRoster([], []));
        }
    }

    /// <summary>The courses, in memory, keyed on the Google id the upsert matches (spec FR-008).</summary>
    public sealed class CourseRepository : ICourseRepository
    {
        private long _nextId = 1;

        /// <summary>Every course staged for insert, in order.</summary>
        public List<Course> Added { get; } = [];

        /// <summary>The stored courses by Google id, as the database would hold them.</summary>
        public Dictionary<string, Course> Stored { get; } = new(StringComparer.Ordinal);

        public Task<Course?> GetByGoogleIdAsync(string googleId, CancellationToken cancellationToken) =>
            Task.FromResult(Stored.TryGetValue(googleId, out var course) ? course : null);

        public void Add(Course course)
        {
            Identity.Assign(course, _nextId++);
            Stored[course.GoogleId] = course;
            Added.Add(course);
        }
    }

    /// <summary>The participants, in memory, keyed on the Google user id (spec FR-006).</summary>
    public sealed class ParticipantRepository : IClassroomParticipantRepository
    {
        private long _nextId = 1;

        public List<ClassroomParticipant> Added { get; } = [];

        public Dictionary<string, ClassroomParticipant> Stored { get; } = new(StringComparer.Ordinal);

        public Task<IReadOnlyList<ClassroomParticipant>> GetByGoogleUserIdsAsync(
            IReadOnlyCollection<string> googleUserIds,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<ClassroomParticipant> found = googleUserIds
                .Where(Stored.ContainsKey)
                .Select(id => Stored[id])
                .ToList();
            return Task.FromResult(found);
        }

        public void Add(ClassroomParticipant participant)
        {
            Identity.Assign(participant, _nextId++);
            Stored[participant.GoogleUserId] = participant;
            Added.Add(participant);
        }
    }

    /// <summary>The memberships, in memory, unique on (course, person) as PC-8 and spec v2 FR-007 fix it.</summary>
    public sealed class MembershipRepository : ICourseMembershipRepository
    {
        private long _nextId = 1;

        public List<CourseMembership> Added { get; } = [];

        /// <summary>Every stored membership, in insertion order.</summary>
        public List<CourseMembership> Stored { get; } = [];

        public Task<IReadOnlyList<CourseMembership>> GetByCourseAsync(long courseId, CancellationToken cancellationToken)
        {
            IReadOnlyList<CourseMembership> found = Stored.Where(m => m.CourseId == courseId).ToList();
            return Task.FromResult(found);
        }

        public void Add(CourseMembership membership)
        {
            Identity.Assign(membership, _nextId++);
            Stored.Add(membership);
            Added.Add(membership);
        }

        /// <summary>The memberships of one course, as the assertions read them.</summary>
        public IReadOnlyList<CourseMembership> OfCourse(long courseId) =>
            Stored.Where(m => m.CourseId == courseId).ToList();
    }

    /// <summary>
    /// Fills the identity the database would generate. The entities keep <c>Id</c> private, as they should, so the
    /// test doubles do it the way EF Core does — by reflection, not by widening the domain (the
    /// <see cref="StateRepository"/> precedent).
    /// </summary>
    private static class Identity
    {
        public static void Assign<T>(T entity, long id)
            where T : notnull =>
            typeof(T).GetProperty("Id")!.SetValue(entity, id);
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
