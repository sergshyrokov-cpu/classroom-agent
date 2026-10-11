using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-044 over substituted ports (TC-1): the US-033 report world (<see cref="FakeMeetReportSource"/>, Kyiv, "now" in
/// October) plus the export's text port, renderer, audit repository and a unit of work that notes the BR-026 service
/// write declared at each commit. No Google port exists in the graph (spec FR-012).
/// </summary>
public sealed class MeetExportWorld
{
    public const long DeanId = 502;

    public const string CourseName = "Test Course Export";

    public const string LinkedCode = "aaa-0001-xyz";

    public const string OtherCode = "eee-0005-xyz";

    public static readonly CultureInfo En = CultureInfo.GetCultureInfo("en");

    public MeetExportWorld()
    {
        Work = new RecordingUnitOfWork(Audit, WriteScope);
    }

    public FakeMeetReportSource Source { get; } = new();

    public SyncWorld.StateRepository States { get; } = new();

    public SchoolTimeZone Zone { get; } = new(MeetReportsTestData.Kyiv);

    public ManualTimeProvider Time { get; } = new(MeetReportsTestData.Now);

    public FakeReportTexts Texts { get; } = new();

    public FakeReportRenderer Renderer { get; } = new();

    public DeanAccountWorld.AuditRepository Audit { get; } = new();

    public ServiceWriteScope WriteScope { get; } = new();

    public RecordingUnitOfWork Work { get; }

    /// <summary>
    /// Reads of the report data (course name, meetings, members, people) — zero proves "validated before anything is read
    /// for the report" (spec FR-009). The course drop-down list of the page is not report data.
    /// </summary>
    public int Reads => Source.Calls;

    public ExportMeetReportsCommand ReportsCommand =>
        new(
            new GetMeetReportsQuery(Source.Courses, Source, new GetLastSynchronizationQuery(States, Zone), Zone, Time),
            Zone,
            Time,
            Texts,
            Renderer,
            Audit,
            Work,
            WriteScope);

    public ExportMeetMeetingCommand MeetingCommand =>
        new(
            new GetMeetReportMeetingQuery(Source, Zone),
            new GetLastSynchronizationQuery(States, Zone),
            Zone,
            Time,
            Texts,
            Renderer,
            Audit,
            Work,
            WriteScope);

    /// <summary>
    /// A course with a teacher (the organizer), two students on the roster since September, and one October meeting
    /// (2026-10-05 10:00 Kyiv, 45 min) whose connections are the organizer, both students and one guest; plus a meeting
    /// of another course.
    /// </summary>
    public Seeded Seed()
    {
        var course = Source.AddCourse(CourseName);
        var other = Source.AddCourse("Test Course Other");
        var since = MeetReportsTestData.Period.Local(9, 1, 0);
        var until = MeetReportsTestData.Now;
        var teacher = Source.AddParticipant(MeetReportsTestData.Email("teacher.one"), "Test Teacher One");
        var one = Source.AddParticipant(MeetReportsTestData.Email("student.one"), "Test Student One");
        var two = Source.AddParticipant(MeetReportsTestData.Email("student.two"), "Test Student Two");
        Source.AddMembership(course, teacher, ClassroomRole.Teacher, since, until);
        Source.AddMembership(course, one, ClassroomRole.Student, since, until);
        Source.AddMembership(course, two, ClassroomRole.Student, since, until);
        Source.Link(LinkedCode, course);
        Source.Link(OtherCode, other);
        var start = MeetReportsTestData.Period.Local(10, 5, 10);
        var meeting = Source.AddMeeting(
            LinkedCode,
            MeetReportsTestData.Email("teacher.one"),
            start,
            start.AddMinutes(45),
            (MeetReportsTestData.Email("teacher.one"), 2700),
            (MeetReportsTestData.Email("student.one"), 2400),
            (MeetReportsTestData.Email("student.two"), 1200),
            (null, 300));
        var elsewhere = Source.AddMeeting(
            OtherCode,
            MeetReportsTestData.Email("teacher.two"),
            MeetReportsTestData.Period.Local(10, 8, 10),
            MeetReportsTestData.Period.Local(10, 8, 11),
            (MeetReportsTestData.Email("student.one"), 600));
        return new Seeded(course, other, meeting, elsewhere);
    }

    public sealed record Seeded(long CourseId, long OtherCourseId, long MeetingId, long OtherCourseMeetingId);

    public static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    public static MeetReportsExportRequest ReportsRequest(
        long? course,
        string? from = null,
        string? to = null,
        string? orientation = null,
        bool defaultPeriod = true) =>
        new(
            course is { } c ? Id(c) : null,
            from ?? (defaultPeriod ? MeetReportsTestData.Period.FromText : null),
            to ?? (defaultPeriod ? MeetReportsTestData.Period.ToText : null),
            orientation);

    public static MeetMeetingExportRequest MeetingRequest(long? course, string? meeting, string? orientation = null) =>
        new(course is { } c ? Id(c) : null, meeting, orientation);

    public Task<MeetExportResult> ExportReportsAsync(MeetReportsExportRequest request, AppRole role = AppRole.Dean) =>
        ReportsCommand.ExecuteAsync(request, DeanId, role, En, "test-request-1", TestContext.Current.CancellationToken);

    public Task<MeetExportResult> ExportMeetingAsync(MeetMeetingExportRequest request, AppRole role = AppRole.Dean) =>
        MeetingCommand.ExecuteAsync(request, DeanId, role, En, "test-request-1", TestContext.Current.CancellationToken);

    /// <summary>Commits, counted; notes the declared service write and how many audit rows were staged at each commit.</summary>
    public sealed class RecordingUnitOfWork(DeanAccountWorld.AuditRepository audit, ServiceWriteScope scope) : IUnitOfWork
    {
        public List<PermittedServiceWrite?> Declared { get; } = [];

        public List<int> StagedAuditRows { get; } = [];

        /// <summary>When set, the next commit throws, as a failing audit insert would.</summary>
        public bool FailNextCommit { get; set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (FailNextCommit)
            {
                FailNextCommit = false;
                throw new InvalidOperationException("Synthetic commit failure.");
            }

            Declared.Add(scope.Current);
            StagedAuditRows.Add(audit.Written.Count);
            audit.WrittenAtLastCommit = audit.Written.Count;
            return Task.CompletedTask;
        }

        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken) =>
            work(cancellationToken);
    }
}
