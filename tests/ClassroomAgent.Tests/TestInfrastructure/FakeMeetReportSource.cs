using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// An in-memory <see cref="IMeetReportSource"/> for the US-033 query unit tests (TC-1). It applies the filters of
/// US-033 db-design §3 — a meeting belongs to a course only through a link of its code to that course id (a "not a
/// course" mark is a link with no course), half-open period on the start — and the §4 rules for people: an email is a
/// personal account when any participant with it has any membership, and the displayed name is that of the lowest
/// participant id with a full name. Courses are registered in <see cref="Courses"/> too, so the drop-down and the
/// reports agree. Every call is counted so the tests can prove bounded reads and short-circuits.
/// </summary>
public sealed class FakeMeetReportSource : IMeetReportSource
{
    private readonly Dictionary<long, string> _courseNames = [];
    private readonly Dictionary<string, long?> _links = new(StringComparer.Ordinal);
    private readonly List<MeetReportMeetingRecord> _meetings = [];
    private readonly List<(long Id, string? Email, string? FullName)> _participants = [];
    private readonly List<(long CourseId, MeetReportMemberRecord Member)> _memberships = [];
    private long _nextId = 5000;

    /// <summary>The journal's course source, fed by <see cref="AddCourse"/>; the query's drop-down reads it.</summary>
    public FakeJournalSource Courses { get; } = new();

    public int Calls { get; private set; }

    /// <summary>The lower-cased emails of every <see cref="GetPeopleAsync"/> call.</summary>
    public List<IReadOnlyCollection<string>> PeopleRequests { get; } = [];

    /// <summary>The bounds of every <see cref="GetMeetingsAsync"/> call.</summary>
    public List<(DateTimeOffset Start, DateTimeOffset EndExclusive)> Bounds { get; } = [];

    public long AddCourse(string name)
    {
        var id = Courses.AddCourse(name);
        _courseNames[id] = name;
        return id;
    }

    /// <summary>Links <paramref name="meetingCode"/> to a course, automatic or confirmed alike (spec FR-004, OD-009).</summary>
    public void Link(string meetingCode, long courseId) => _links[meetingCode] = courseId;

    /// <summary>Marks <paramref name="meetingCode"/> "not a course" (BR-083): a link row with no course.</summary>
    public void MarkNotACourse(string meetingCode) => _links[meetingCode] = null;

    /// <summary>Adds a participant; returns its id. Ids grow, so the first added wins the name tie rule.</summary>
    public long AddParticipant(string? email, string? fullName = null)
    {
        var id = _nextId++;
        _participants.Add((id, email, fullName));
        return id;
    }

    public void AddMembership(
        long courseId,
        long participantId,
        ClassroomRole role,
        DateTimeOffset firstSeenAt,
        DateTimeOffset lastSeenAt,
        bool onRoster = true)
    {
        var participant = _participants.Single(p => p.Id == participantId);
        _memberships.Add((courseId, new MeetReportMemberRecord(
            participantId, role, participant.Email, participant.FullName, firstSeenAt, lastSeenAt, onRoster)));
    }

    /// <summary>Adds a meeting; <paramref name="connections"/> are (email or null, seconds).</summary>
    public long AddMeeting(
        string meetingCode,
        string organizerEmail,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        params (string? Email, int Seconds)[] connections)
    {
        var id = _nextId++;
        _meetings.Add(new MeetReportMeetingRecord(
            id,
            meetingCode,
            organizerEmail,
            startedAt,
            endedAt,
            connections.Select(c => new MeetReportConnectionRecord(c.Email, c.Seconds)).ToList()));
        return id;
    }

    public Task<string?> GetCourseNameAsync(long courseId, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(_courseNames.TryGetValue(courseId, out var name) ? name : null);
    }

    public Task<IReadOnlyList<MeetReportMeetingRecord>> GetMeetingsAsync(
        long courseId,
        DateTimeOffset start,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken)
    {
        Calls++;
        Bounds.Add((start, endExclusive));
        IReadOnlyList<MeetReportMeetingRecord> result = _meetings
            .Where(m => IsOfCourse(m, courseId) && m.StartedAt >= start && m.StartedAt < endExclusive)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<MeetReportMeetingRecord?> GetMeetingOfCourseAsync(
        long courseId,
        long meetingId,
        CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(_meetings.SingleOrDefault(m => m.Id == meetingId && IsOfCourse(m, courseId)));
    }

    public Task<IReadOnlyList<MeetReportMemberRecord>> GetCourseMembersAsync(long courseId, CancellationToken cancellationToken)
    {
        Calls++;
        IReadOnlyList<MeetReportMemberRecord> result = _memberships
            .Where(m => m.CourseId == courseId)
            .Select(m => m.Member)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<MeetReportPersonRecord>> GetPeopleAsync(
        IReadOnlyCollection<string> lowerCaseEmails,
        CancellationToken cancellationToken)
    {
        Calls++;
        PeopleRequests.Add(lowerCaseEmails.ToList());
        var result = new List<MeetReportPersonRecord>();
        foreach (var email in lowerCaseEmails.Distinct(StringComparer.Ordinal))
        {
            var matching = _participants
                .Where(p => p.Email is not null && string.Equals(p.Email.ToLowerInvariant(), email, StringComparison.Ordinal))
                .OrderBy(p => p.Id)
                .ToList();
            if (matching.Count == 0)
            {
                continue;
            }

            var ids = matching.Select(p => p.Id).ToHashSet();
            result.Add(new MeetReportPersonRecord(
                email,
                matching.FirstOrDefault(p => p.FullName is not null).FullName,
                _memberships.Any(m => ids.Contains(m.Member.ParticipantId))));
        }

        return Task.FromResult<IReadOnlyList<MeetReportPersonRecord>>(result);
    }

    private bool IsOfCourse(MeetReportMeetingRecord meeting, long courseId) =>
        _links.TryGetValue(meeting.MeetingCode, out var linked) && linked == courseId;
}
