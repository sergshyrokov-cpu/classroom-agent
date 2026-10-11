using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// An in-memory <see cref="ICourseCardSource"/> for the US-021 query unit tests (TC-1). Members are returned in the order
/// they were added — the port's order, which the query must keep (ordering itself is proved against PostgreSQL in
/// <c>CourseCardSourceTests</c>). Every call and its argument is recorded, so the tests can prove bounded reads and that
/// nothing is read before the id parses.
/// </summary>
public sealed class FakeCourseCardSource : ICourseCardSource
{
    private readonly Dictionary<long, CourseCardCourseRecord> _courses = [];
    private readonly Dictionary<long, List<CourseCardMemberRecord>> _members = [];
    private long _nextCourseId = 100;
    private long _nextParticipantId = 9000;

    /// <summary>The id of every <see cref="GetCourseAsync"/> call.</summary>
    public List<long> CourseRequests { get; } = [];

    /// <summary>The id of every <see cref="GetMembersAsync"/> call.</summary>
    public List<long> MemberRequests { get; } = [];

    /// <summary>Every call of any member — one database round trip each (db-design §2).</summary>
    public int TotalCalls => CourseRequests.Count + MemberRequests.Count;

    public long AddCourse(
        string name = "Фізика",
        string? section = null,
        CourseState state = CourseState.Active,
        string? room = null,
        string? descriptionHeading = null,
        string? description = null,
        DateTimeOffset? creationTime = null,
        DateTimeOffset? updateTime = null)
    {
        var id = _nextCourseId++;
        _courses[id] = new CourseCardCourseRecord(
            id, name, section, state, room, descriptionHeading, description, creationTime, updateTime);
        _members[id] = [];
        return id;
    }

    /// <summary>Adds a membership with its participant; returns the participant id.</summary>
    public long AddMember(
        long courseId,
        string? surname,
        string? givenName,
        string? email,
        ClassroomRole role = ClassroomRole.Student,
        bool onRoster = true,
        DateTimeOffset? lastSeenAt = null,
        bool isOwner = false)
    {
        var id = _nextParticipantId++;
        _members[courseId].Add(new CourseCardMemberRecord(
            id,
            surname,
            givenName,
            email,
            role,
            onRoster,
            lastSeenAt ?? new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero),
            isOwner));
        return id;
    }

    public Task<CourseCardCourseRecord?> GetCourseAsync(long courseId, CancellationToken cancellationToken)
    {
        CourseRequests.Add(courseId);
        return Task.FromResult(_courses.GetValueOrDefault(courseId));
    }

    public Task<IReadOnlyList<CourseCardMemberRecord>> GetMembersAsync(long courseId, CancellationToken cancellationToken)
    {
        MemberRequests.Add(courseId);
        IReadOnlyList<CourseCardMemberRecord> members = _members.TryGetValue(courseId, out var list) ? list.ToList() : [];
        return Task.FromResult(members);
    }
}
