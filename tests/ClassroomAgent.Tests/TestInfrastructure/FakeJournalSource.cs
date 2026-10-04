using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// An in-memory <see cref="IJournalSource"/> for the <c>GetJournalQuery</c> unit tests (TC-1). It applies the same
/// filters as US-025 db-design §2 — course, half-open period, student role — so the use case sees exactly what the
/// real port would return, and it records every call so the tests can prove bounded reads and the short-circuits.
/// </summary>
public sealed class FakeJournalSource : IJournalSource
{
    private readonly List<JournalCourseRecord> _courses = [];
    private readonly List<(long CourseId, JournalItemRecord Item)> _items = [];
    private readonly List<(long CourseId, ClassroomRole Role, JournalMemberRecord Member)> _members = [];
    private readonly List<JournalSubmissionRecord> _submissions = [];
    private long _nextId = 1000;

    public int CourseCalls { get; private set; }

    public int ItemCalls { get; private set; }

    public int MemberCalls { get; private set; }

    public int SubmissionCalls { get; private set; }

    public int TotalCalls => CourseCalls + ItemCalls + MemberCalls + SubmissionCalls;

    /// <summary>The bounds of every <see cref="GetItemsAsync"/> and <see cref="GetSubmissionsAsync"/> call.</summary>
    public List<(DateTimeOffset Start, DateTimeOffset EndExclusive)> Bounds { get; } = [];

    public long AddCourse(string name, string? section = null)
    {
        var id = _nextId++;
        _courses.Add(new JournalCourseRecord(id, name, section));
        return id;
    }

    /// <summary>Adds an item; graded when <paramref name="maxPoints"/> is set, a material when <paramref name="material"/>.</summary>
    public long AddItem(
        long courseId,
        DateTimeOffset itemDate,
        string? title = null,
        decimal? maxPoints = null,
        DateTimeOffset? dueAt = null,
        bool material = false)
    {
        var id = _nextId++;
        var kind = material
            ? CourseWorkKind.Material
            : maxPoints is null ? CourseWorkKind.UngradedWork : CourseWorkKind.GradedWork;
        _items.Add((courseId, new JournalItemRecord(id, title ?? CourseWorkTestData.Title((int)id), itemDate, dueAt, maxPoints, kind)));
        return id;
    }

    /// <summary>Adds a participant with a membership of the course; returns the participant id.</summary>
    public long AddMember(
        long courseId,
        string? fullName,
        string? email = null,
        DateTimeOffset? firstSeenAt = null,
        DateTimeOffset? lastSeenAt = null,
        bool onRoster = true,
        ClassroomRole role = ClassroomRole.Student,
        long? participantId = null)
    {
        var id = participantId ?? _nextId++;
        var first = firstSeenAt ?? JournalTestData.Period.StartUtc.AddDays(-30);
        var last = lastSeenAt ?? InstallationTestHost.DefaultStart;
        _members.Add((courseId, role, new JournalMemberRecord(id, first, last, onRoster, fullName, email)));
        return id;
    }

    public long AddSubmission(
        long itemId,
        long participantId,
        SubmissionState state = SubmissionState.TurnedIn,
        string? rawState = null,
        decimal? assignedGrade = null,
        decimal? draftGrade = null,
        DateTimeOffset? turnedInAt = null,
        bool late = false,
        DateTimeOffset? updateTime = null,
        long? id = null)
    {
        var submissionId = id ?? _nextId++;
        _submissions.Add(new JournalSubmissionRecord(
            submissionId, itemId, participantId, updateTime, state, rawState, assignedGrade, draftGrade, turnedInAt, late));
        return submissionId;
    }

    public Task<IReadOnlyList<JournalCourseRecord>> GetCoursesAsync(CancellationToken cancellationToken)
    {
        CourseCalls++;
        return Task.FromResult<IReadOnlyList<JournalCourseRecord>>(_courses.ToList());
    }

    public Task<IReadOnlyList<JournalItemRecord>> GetItemsAsync(
        long courseId,
        DateTimeOffset start,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken)
    {
        ItemCalls++;
        Bounds.Add((start, endExclusive));
        return Task.FromResult<IReadOnlyList<JournalItemRecord>>(ItemsOf(courseId, start, endExclusive).ToList());
    }

    public Task<IReadOnlyList<JournalMemberRecord>> GetStudentMembersAsync(long courseId, CancellationToken cancellationToken)
    {
        MemberCalls++;
        return Task.FromResult<IReadOnlyList<JournalMemberRecord>>(
            _members.Where(m => m.CourseId == courseId && m.Role == ClassroomRole.Student).Select(m => m.Member).ToList());
    }

    public Task<IReadOnlyList<JournalSubmissionRecord>> GetSubmissionsAsync(
        long courseId,
        DateTimeOffset start,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken)
    {
        SubmissionCalls++;
        Bounds.Add((start, endExclusive));
        var itemIds = ItemsOf(courseId, start, endExclusive).Select(i => i.Id).ToHashSet();
        return Task.FromResult<IReadOnlyList<JournalSubmissionRecord>>(
            _submissions.Where(s => itemIds.Contains(s.ItemId)).ToList());
    }

    private IEnumerable<JournalItemRecord> ItemsOf(long courseId, DateTimeOffset start, DateTimeOffset endExclusive) =>
        _items.Where(i => i.CourseId == courseId && i.Item.ItemDate >= start && i.Item.ItemDate < endExclusive)
            .Select(i => i.Item);
}
