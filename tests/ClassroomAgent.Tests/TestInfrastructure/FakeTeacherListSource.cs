using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// An in-memory <see cref="ITeacherListSource"/> for the US-022 query unit tests (TC-1). Each teacher carries a course
/// count per state; a state lists the teachers with a count above zero, "all" (null) sums the states (db-design §2,
/// spec I-7). Order is the label key — surname and given name, else email, else last — then id. Every call and its
/// arguments are recorded, so the tests can prove bounded reads and that nothing is read once the query is refused.
/// </summary>
public sealed class FakeTeacherListSource : ITeacherListSource
{
    private static readonly StringComparer Alphabet = StringComparer.Create(CultureInfo.GetCultureInfo("uk-UA"), ignoreCase: true);

    private readonly List<(long Id, string? Surname, string? GivenName, string? Email, Dictionary<CourseState, int> Counts)> _teachers = [];
    private long _nextId = 9000;

    /// <summary>The state of every <see cref="CountTeachersAsync"/> call.</summary>
    public List<CourseState?> CountRequests { get; } = [];

    /// <summary>The arguments of every <see cref="GetTeacherPageAsync"/> call.</summary>
    public List<(CourseState? State, int Offset, int Limit)> PageRequests { get; } = [];

    /// <summary>Every call of any member — one database round trip each (db-design §3).</summary>
    public int TotalCalls => CountRequests.Count + PageRequests.Count;

    /// <summary>Adds a teacher with the given course counts per state; returns the participant id.</summary>
    public long AddTeacher(
        string? surname = null,
        string? givenName = null,
        string? email = null,
        int active = 1,
        int archived = 0,
        int provisioned = 0,
        int declined = 0,
        int suspended = 0)
    {
        var id = _nextId++;
        _teachers.Add((id, surname, givenName, email, new Dictionary<CourseState, int>
        {
            [CourseState.Active] = active,
            [CourseState.Archived] = archived,
            [CourseState.Provisioned] = provisioned,
            [CourseState.Declined] = declined,
            [CourseState.Suspended] = suspended,
        }));
        return id;
    }

    /// <summary>Adds <paramref name="count"/> teachers "Вчитель 001" …, one active course each; returns their ids in label order.</summary>
    public List<long> AddNumberedTeachers(int count) =>
        Enumerable.Range(1, count).Select(i => AddTeacher("Вчитель", i.ToString("D3", CultureInfo.InvariantCulture))).ToList();

    public Task<int> CountTeachersAsync(CourseState? state, CancellationToken cancellationToken)
    {
        CountRequests.Add(state);
        return Task.FromResult(Listed(state).Count);
    }

    public Task<IReadOnlyList<TeacherListRecord>> GetTeacherPageAsync(
        CourseState? state, int offset, int limit, CancellationToken cancellationToken)
    {
        PageRequests.Add((state, offset, limit));
        IReadOnlyList<TeacherListRecord> page = Listed(state).Skip(offset).Take(limit).ToList();
        return Task.FromResult(page);
    }

    private List<TeacherListRecord> Listed(CourseState? state) =>
        _teachers
            .Select(t => new TeacherListRecord(
                t.Id, t.Surname, t.GivenName, t.Email, state is { } s ? t.Counts[s] : t.Counts.Values.Sum()))
            .Where(r => r.CourseCount > 0)
            .OrderBy(r => Label(r) is null ? 1 : 0)
            .ThenBy(Label, Alphabet)
            .ThenBy(r => r.ParticipantId)
            .ToList();

    private static string? Label(TeacherListRecord r)
    {
        var parts = new[] { r.Surname, r.GivenName }.Where(p => !string.IsNullOrEmpty(p)).ToList();
        return parts.Count > 0 ? string.Join(' ', parts) : string.IsNullOrEmpty(r.Email) ? null : r.Email;
    }
}
