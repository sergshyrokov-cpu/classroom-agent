using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// An in-memory <see cref="ICourseListSource"/> for the US-020 query unit tests (TC-1). It applies the rules of US-020
/// db-design §3 — the state, current-teacher and literal case-insensitive search filters combined with AND; the course
/// order by name, section (none first), id; the teacher label key (surname and given name, else email, else last), then
/// id. Every call and its arguments are recorded, so the tests can prove bounded reads and that nothing is read for the
/// list once the query is refused.
/// </summary>
public sealed class FakeCourseListSource : ICourseListSource
{
    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");

    private static readonly StringComparer Alphabet = StringComparer.Create(Uk, ignoreCase: true);

    private readonly List<CourseListCourseRecord> _courses = [];
    private readonly List<CourseListPersonRecord> _people = [];
    private readonly List<(long CourseId, long ParticipantId, ClassroomRole Role, bool OnRoster)> _memberships = [];
    private long _nextCourseId = 100;
    private long _nextParticipantId = 9000;

    public int TeacherChoiceCalls { get; private set; }

    public int AnyCourseStoredCalls { get; private set; }

    /// <summary>The filter of every <see cref="CountCoursesAsync"/> call.</summary>
    public List<CourseListFilter> CountFilters { get; } = [];

    /// <summary>The arguments of every <see cref="GetCoursePageAsync"/> call.</summary>
    public List<(CourseListFilter Filter, int Offset, int Limit)> PageRequests { get; } = [];

    /// <summary>The course ids of every <see cref="GetCurrentTeachersAsync"/> call.</summary>
    public List<IReadOnlyCollection<long>> TeacherRequests { get; } = [];

    /// <summary>The course ids of every <see cref="GetStudentCountsAsync"/> call.</summary>
    public List<IReadOnlyCollection<long>> StudentCountRequests { get; } = [];

    /// <summary>Every call of any member, in total — one database round trip each (db-design §3).</summary>
    public int TotalCalls =>
        TeacherChoiceCalls + AnyCourseStoredCalls + CountFilters.Count + PageRequests.Count + TeacherRequests.Count
        + StudentCountRequests.Count;

    /// <summary>True when no course has been read for the list (spec FR-012, S-07): no count, page, teachers or counts.</summary>
    public bool NoCourseRead =>
        CountFilters.Count == 0 && PageRequests.Count == 0 && TeacherRequests.Count == 0
        && StudentCountRequests.Count == 0 && AnyCourseStoredCalls == 0;

    public long AddCourse(
        string name,
        string? section = null,
        CourseState state = CourseState.Active,
        DateTimeOffset? updateTime = null)
    {
        var id = _nextCourseId++;
        _courses.Add(new CourseListCourseRecord(id, name, section, state, updateTime));
        return id;
    }

    public long AddPerson(string? surname = null, string? givenName = null, string? email = null)
    {
        var id = _nextParticipantId++;
        _people.Add(new CourseListPersonRecord(id, surname, givenName, email));
        return id;
    }

    public void AddTeacher(long courseId, long participantId, bool onRoster = true) =>
        _memberships.Add((courseId, participantId, ClassroomRole.Teacher, onRoster));

    public void AddStudent(long courseId, long participantId, bool onRoster = true) =>
        _memberships.Add((courseId, participantId, ClassroomRole.Student, onRoster));

    /// <summary>Adds <paramref name="count"/> current students to the course.</summary>
    public void AddStudents(long courseId, int count)
    {
        for (var i = 0; i < count; i++)
        {
            AddStudent(courseId, AddPerson(email: $"student.{courseId}.{i}@school.example"));
        }
    }

    public Task<IReadOnlyList<CourseListPersonRecord>> GetTeacherChoiceAsync(CancellationToken cancellationToken)
    {
        TeacherChoiceCalls++;
        var ids = _memberships.Where(IsCurrentTeacher).Select(m => m.ParticipantId).ToHashSet();
        IReadOnlyList<CourseListPersonRecord> result = OrderByLabel(_people.Where(p => ids.Contains(p.ParticipantId))).ToList();
        return Task.FromResult(result);
    }

    public Task<int> CountCoursesAsync(CourseListFilter filter, CancellationToken cancellationToken)
    {
        CountFilters.Add(filter);
        return Task.FromResult(Matching(filter).Count());
    }

    public Task<bool> AnyCourseStoredAsync(CancellationToken cancellationToken)
    {
        AnyCourseStoredCalls++;
        return Task.FromResult(_courses.Count > 0);
    }

    public Task<IReadOnlyList<CourseListCourseRecord>> GetCoursePageAsync(
        CourseListFilter filter, int offset, int limit, CancellationToken cancellationToken)
    {
        PageRequests.Add((filter, offset, limit));
        IReadOnlyList<CourseListCourseRecord> result = Matching(filter)
            .OrderBy(c => c.Name, Alphabet)
            .ThenBy(c => c.Section is null ? 0 : 1)
            .ThenBy(c => c.Section, Alphabet)
            .ThenBy(c => c.CourseId)
            .Skip(offset)
            .Take(limit)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<CourseListTeacherRecord>> GetCurrentTeachersAsync(
        IReadOnlyCollection<long> courseIds, CancellationToken cancellationToken)
    {
        TeacherRequests.Add(courseIds.ToList());
        IReadOnlyList<CourseListTeacherRecord> result = courseIds
            .Order()
            .SelectMany(courseId => OrderByLabel(
                    _memberships
                        .Where(m => m.CourseId == courseId && IsCurrentTeacher(m))
                        .Select(m => _people.Single(p => p.ParticipantId == m.ParticipantId)))
                .Select(p => new CourseListTeacherRecord(courseId, p)))
            .ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<CourseListStudentCountRecord>> GetStudentCountsAsync(
        IReadOnlyCollection<long> courseIds, CancellationToken cancellationToken)
    {
        StudentCountRequests.Add(courseIds.ToList());
        IReadOnlyList<CourseListStudentCountRecord> result = courseIds
            .Select(id => new CourseListStudentCountRecord(
                id, _memberships.Count(m => m.CourseId == id && m.Role == ClassroomRole.Student && m.OnRoster)))
            .Where(r => r.StudentCount > 0)
            .ToList();
        return Task.FromResult(result);
    }

    /// <summary>db-design §3.4: the FR-007 label key — empty parts count as absent; no label sorts last.</summary>
    public static string? LabelKey(CourseListPersonRecord person)
    {
        var parts = new[] { person.Surname, person.GivenName }.Where(p => !string.IsNullOrEmpty(p)).ToList();
        return parts.Count > 0 ? string.Join(' ', parts) : string.IsNullOrEmpty(person.Email) ? null : person.Email;
    }

    private static IEnumerable<CourseListPersonRecord> OrderByLabel(IEnumerable<CourseListPersonRecord> people) =>
        people
            .OrderBy(p => LabelKey(p) is null ? 1 : 0)
            .ThenBy(LabelKey, Alphabet)
            .ThenBy(p => p.ParticipantId);

    private static bool IsCurrentTeacher((long CourseId, long ParticipantId, ClassroomRole Role, bool OnRoster) m) =>
        m.Role == ClassroomRole.Teacher && m.OnRoster;

    private static bool Contains(string? text, string term) =>
        text is not null && text.ToLower(Uk).Contains(term.ToLower(Uk), StringComparison.Ordinal);

    private IEnumerable<CourseListCourseRecord> Matching(CourseListFilter filter) =>
        _courses.Where(c =>
            (filter.State is not { } state || c.State == state)
            && (filter.TeacherParticipantId is not { } teacher
                || _memberships.Any(m => m.CourseId == c.CourseId && m.ParticipantId == teacher && IsCurrentTeacher(m)))
            && (filter.SearchText is not { } term || Contains(c.Name, term) || Contains(c.Section, term)));
}
