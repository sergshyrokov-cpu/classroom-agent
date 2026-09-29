using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The Classroom port of a <b>host</b> test (US-014, TC-4): no test may reach a live Google API, and once the
/// import step exists a run in a real host would try. Empty unless a test seeds it, which keeps every US-013
/// expectation true — a run with no course to import completes with the counter at zero.
/// </summary>
/// <remarks>
/// Every course and person a test seeds here is invented: TC-4 forbids a real roster, name, address or school
/// domain in a fixture. The Application-layer double lives in <see cref="SyncWorld.ClassroomReader"/>; this one
/// exists because the host resolves the port from its own composition root.
/// </remarks>
public sealed class FakeClassroomReader : IClassroomReader
{
    private readonly List<CourseSnapshot> _courses = [];
    private readonly Dictionary<string, CourseRoster> _rosters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<CourseWorkSnapshot>> _courseWork = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<SubmissionSnapshot>> _submissions = new(StringComparer.Ordinal);

    /// <summary>The impersonation address every call was made with (BR-015).</summary>
    public List<string> ImpersonatedAs { get; } = [];

    /// <summary>Seeds one course and its roster, as <see cref="SyncWorld.ClassroomReader"/> does.</summary>
    public FakeClassroomReader WithCourse(
        string googleId,
        string name,
        string state = CourseTestData.States.Active,
        IEnumerable<RosterEntry>? teachers = null,
        IEnumerable<RosterEntry>? students = null,
        string? description = null)
    {
        _courses.Add(new CourseSnapshot(
            googleId,
            state,
            new CourseDetails(name, null, null, description, null, null, null, null, null, null, null, null)));
        _rosters[googleId] = new CourseRoster((teachers ?? []).ToList(), (students ?? []).ToList());
        return this;
    }

    /// <summary>Seeds one coursework or material item, as <see cref="SyncWorld.ClassroomReader.WithCourseWork"/> does.</summary>
    public FakeClassroomReader WithCourseWork(
        string courseGoogleId,
        string itemGoogleId,
        CourseWorkResource resource,
        string title,
        DateTimeOffset itemDate,
        DateTimeOffset? dueAt = null,
        decimal? maxPoints = null,
        DateTimeOffset? creationTime = null,
        DateTimeOffset? updateTime = null)
    {
        if (!_courseWork.TryGetValue(courseGoogleId, out var items))
        {
            items = [];
            _courseWork[courseGoogleId] = items;
        }

        items.Add(new CourseWorkSnapshot(
            itemGoogleId,
            resource,
            new CourseWorkDetails(title, itemDate, dueAt, maxPoints, creationTime, updateTime)));
        return this;
    }

    /// <summary>Seeds one submission, as <see cref="SyncWorld.ClassroomReader.WithSubmission"/> does.</summary>
    public FakeClassroomReader WithSubmission(
        string courseGoogleId,
        string courseWorkGoogleId,
        string googleUserId,
        string googleId,
        string state,
        decimal? assignedGrade = null,
        decimal? draftGrade = null,
        DateTimeOffset? turnedInAt = null,
        bool late = false,
        DateTimeOffset? updateTime = null)
    {
        if (!_submissions.TryGetValue(courseGoogleId, out var items))
        {
            items = [];
            _submissions[courseGoogleId] = items;
        }

        items.Add(new SubmissionSnapshot(
            googleId,
            courseWorkGoogleId,
            googleUserId,
            state,
            assignedGrade,
            draftGrade,
            turnedInAt,
            late,
            updateTime));
        return this;
    }

    public async IAsyncEnumerable<CourseSnapshot> ReadCoursesAsync(
        string impersonationUser,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ImpersonatedAs.Add(impersonationUser);
        foreach (var course in _courses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return course;
            await Task.Yield();
        }
    }

    public Task<CourseRoster> ReadRosterAsync(
        string impersonationUser,
        string courseGoogleId,
        CancellationToken cancellationToken)
    {
        ImpersonatedAs.Add(impersonationUser);
        return Task.FromResult(_rosters.TryGetValue(courseGoogleId, out var roster)
            ? roster
            : new CourseRoster([], []));
    }

    public Task<CourseWorkPage> ReadCourseWorkAsync(
        string impersonationUser,
        string courseGoogleId,
        CancellationToken cancellationToken)
    {
        ImpersonatedAs.Add(impersonationUser);
        return Task.FromResult(new CourseWorkPage(
            _courseWork.TryGetValue(courseGoogleId, out var items) ? items : []));
    }

    public Task<IReadOnlyList<SubmissionSnapshot>> ReadSubmissionsAsync(
        string impersonationUser,
        string courseGoogleId,
        CancellationToken cancellationToken)
    {
        ImpersonatedAs.Add(impersonationUser);
        return Task.FromResult<IReadOnlyList<SubmissionSnapshot>>(
            _submissions.TryGetValue(courseGoogleId, out var items) ? items : []);
    }
}
