using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// <see cref="IJournalFieldSource"/> in memory (TC-1, TC-4): the journal fields a report reads, with every call
/// recorded. Lessons are filtered by lesson date exactly as the port's contract states, so a use-case test proves the
/// use case's own rules and a separate PostgreSQL test proves the port (db-design §5.1).
/// </summary>
public sealed class FakeJournalFieldSource : IJournalFieldSource
{
    private long _nextId = 1000;

    public List<JournalCourseRecord> Courses { get; } = [];

    public List<(long CourseId, JournalLessonRecord Lesson)> Lessons { get; } = [];

    public List<(long CourseId, JournalCourseMemberRecord Member)> Members { get; } = [];

    public List<JournalSubmissionRecord> Submissions { get; } = [];

    /// <summary>Every call by method name, in order.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>The (start, end) of every lessons and submissions call — the UTC interval of spec FR-005.</summary>
    public List<(DateTimeOffset Start, DateTimeOffset End)> Intervals { get; } = [];

    public long AddCourse(string name = "Test Course One", string? section = "Test Section A")
    {
        var id = _nextId++;
        Courses.Add(new JournalCourseRecord(id, name, section));
        return id;
    }

    public long AddLesson(
        long courseId,
        DateTimeOffset lessonDate,
        string title = "Test Lesson",
        decimal? maxPoints = 10m,
        DateTimeOffset? dueAt = null,
        CourseWorkResource resource = CourseWorkResource.CourseWork)
    {
        var id = _nextId++;
        Lessons.Add((courseId, new JournalLessonRecord(id, resource, title, maxPoints, dueAt, lessonDate)));
        return id;
    }

    /// <summary>
    /// A member with the US-042 name parts (entity model §3.2). The report reads no full name any more (spec I-6), so
    /// none is supplied; <c>FullName: null</c> goes when IMPLEMENTATION removes the field from the record.
    /// </summary>
    public long AddMember(
        long courseId,
        ClassroomRole role = ClassroomRole.Student,
        string? surname = "Student",
        string? givenName = "Test",
        string? email = null,
        DateTimeOffset? firstSeenAt = null,
        DateTimeOffset? lastSeenAt = null,
        bool onRoster = true)
    {
        var id = _nextId++;
        var first = firstSeenAt ?? JournalTestData.Period.StartUtc.AddDays(-30);
        Members.Add((courseId, new JournalCourseMemberRecord(
            id, role, first, lastSeenAt ?? first, onRoster, Email: email, Surname: surname, GivenName: givenName)));
        return id;
    }

    public long AddSubmission(
        long lessonId,
        long participantId,
        SubmissionState state = SubmissionState.TurnedIn,
        decimal? assignedGrade = null,
        decimal? draftGrade = null,
        bool late = false,
        DateTimeOffset? turnedInAt = null,
        string? rawState = null,
        DateTimeOffset? updateTime = null)
    {
        var id = _nextId++;
        Submissions.Add(new JournalSubmissionRecord(
            id, lessonId, participantId, updateTime, state, rawState, assignedGrade, draftGrade, turnedInAt, late));
        return id;
    }

    public Task<IReadOnlyList<JournalCourseRecord>> GetCoursesAsync(CancellationToken cancellationToken)
    {
        Calls.Add(nameof(GetCoursesAsync));
        return Task.FromResult<IReadOnlyList<JournalCourseRecord>>(Courses.ToList());
    }

    public Task<IReadOnlyList<JournalLessonRecord>> GetLessonsAsync(
        long courseId,
        DateTimeOffset start,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken)
    {
        Calls.Add(nameof(GetLessonsAsync));
        Intervals.Add((start, endExclusive));
        return Task.FromResult<IReadOnlyList<JournalLessonRecord>>(InPeriod(courseId, start, endExclusive).ToList());
    }

    public Task<IReadOnlyList<JournalCourseMemberRecord>> GetMembersAsync(long courseId, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(GetMembersAsync));
        return Task.FromResult<IReadOnlyList<JournalCourseMemberRecord>>(
            Members.Where(m => m.CourseId == courseId).Select(m => m.Member).ToList());
    }

    public Task<IReadOnlyList<JournalSubmissionRecord>> GetLessonSubmissionsAsync(
        long courseId,
        DateTimeOffset start,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken)
    {
        Calls.Add(nameof(GetLessonSubmissionsAsync));
        Intervals.Add((start, endExclusive));
        var ids = InPeriod(courseId, start, endExclusive).Select(l => l.Id).ToHashSet();
        return Task.FromResult<IReadOnlyList<JournalSubmissionRecord>>(Submissions.Where(s => ids.Contains(s.ItemId)).ToList());
    }

    private IEnumerable<JournalLessonRecord> InPeriod(long courseId, DateTimeOffset start, DateTimeOffset endExclusive) =>
        Lessons.Where(l => l.CourseId == courseId && l.Lesson.LessonDate >= start && l.Lesson.LessonDate < endExclusive)
            .Select(l => l.Lesson);
}
