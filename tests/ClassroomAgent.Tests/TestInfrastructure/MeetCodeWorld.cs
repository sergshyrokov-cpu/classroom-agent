using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The US-032 write use cases with every port in memory (TC-1): the links, the codes that have meetings, the stored
/// courses, the audit trail, the unit of work, the read-only guard and the clock. Nothing here can reach Google.
/// </summary>
public sealed class MeetCodeWorld
{
    public const long AdminId = 501;

    public const long DeanId = 502;

    public const long CourseA = 101;

    public const long CourseB = 102;

    public const long CourseC = 103;

    public MeetCodeWorld(bool readOnly = false)
    {
        Time = new ManualTimeProvider(InstallationTestHost.DefaultStart);
        ReadOnly = new DeanAccountWorld.Guard(readOnly);
        Links = new LinkRepository();
        Audit = new DeanAccountWorld.AuditRepository();
        Work = new UnitOfWork(Audit, Links);
        WriteScope = new ServiceWriteScope();
    }

    public ManualTimeProvider Time { get; }

    public DeanAccountWorld.Guard ReadOnly { get; }

    public LinkRepository Links { get; }

    public DeanAccountWorld.AuditRepository Audit { get; }

    public UnitOfWork Work { get; }

    public ServiceWriteScope WriteScope { get; }

    public SetMeetCodeCourseUseCase SetCourse => new(ReadOnly, Links, Audit, Work, WriteScope, Time);

    public ConfirmMeetCodeLinkUseCase Confirm => new(ReadOnly, Links, Audit, Work, WriteScope, Time);

    public MarkMeetCodeNotACourseUseCase Mark => new(ReadOnly, Links, Audit, Work, WriteScope, Time);

    /// <summary>A form as the browser posts it: name/value pairs in order.</summary>
    public static MeetCodeFormInput Form(params (string Name, string? Value)[] fields) =>
        new(fields.Select(f => new KeyValuePair<string, string?>(f.Name, f.Value)).ToList());

    /// <summary>The links, in memory; reads and writes counted so the evaluation order can be asserted.</summary>
    public sealed class LinkRepository : IMeetingCodeLinkRepository
    {
        private readonly Dictionary<string, MeetingCodeLink> _links = new(StringComparer.Ordinal);

        private readonly HashSet<string> _codesWithMeetings = new(StringComparer.Ordinal);

        private readonly HashSet<long> _courses = [CourseA, CourseB, CourseC];

        private readonly List<MeetingCodeLink> _added = [];

        public int Reads { get; private set; }

        public IReadOnlyList<MeetingCodeLink> Added => _added;

        /// <summary>The committed links by code.</summary>
        public IReadOnlyDictionary<string, MeetingCodeLink> Committed => _links;

        /// <summary>Stores a link made through its factory (never by reflection), with meetings for its code.</summary>
        public MeetingCodeLink Seed(MeetingCodeLink link, bool hasMeetings = true)
        {
            _links[link.MeetingCode] = link;
            if (hasMeetings)
            {
                _codesWithMeetings.Add(link.MeetingCode);
            }

            return link;
        }

        /// <summary>A code with stored meetings and no link row — unassigned.</summary>
        public void SeedUnassigned(string code) => _codesWithMeetings.Add(code);

        public void RemoveCourse(long courseId) => _courses.Remove(courseId);

        public Task<MeetingCodeLink?> GetByCodeAsync(string meetingCode, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(_links.GetValueOrDefault(meetingCode));
        }

        public Task<bool> CodeHasMeetingsAsync(string meetingCode, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(_codesWithMeetings.Contains(meetingCode));
        }

        public Task<bool> CourseExistsAsync(long courseId, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(_courses.Contains(courseId));
        }

        public void Add(MeetingCodeLink link) => _added.Add(link);

        public void Commit()
        {
            foreach (var link in _added)
            {
                _links[link.MeetingCode] = link;
            }

            _added.Clear();
        }

        public void Discard() => _added.Clear();
    }

    /// <summary>Commits, counted, with the transaction boundary honoured; can simulate a concurrent change.</summary>
    public sealed class UnitOfWork(DeanAccountWorld.AuditRepository audit, LinkRepository links) : IUnitOfWork
    {
        public int Commits { get; private set; }

        public int Transactions { get; private set; }

        /// <summary>Audit rows staged at each commit, in order.</summary>
        public List<int> AuditRowsAtCommit { get; } = [];

        /// <summary>When set, the next commit throws <see cref="MeetingCodeLinkConflictException"/> (db-design §7).</summary>
        public bool FailNextWithConflict { get; set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (FailNextWithConflict)
            {
                FailNextWithConflict = false;
                links.Discard();
                throw new MeetingCodeLinkConflictException(new InvalidOperationException("synthetic concurrency failure"));
            }

            Commits++;
            links.Commit();
            AuditRowsAtCommit.Add(audit.Written.Count);
            audit.WrittenAtLastCommit = audit.Written.Count;
            return Task.CompletedTask;
        }

        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
        {
            Transactions++;
            await work(cancellationToken);
        }
    }
}
