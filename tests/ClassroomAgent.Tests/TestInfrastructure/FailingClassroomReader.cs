using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// An <see cref="IClassroomReader"/> that answers from a <see cref="FakeClassroomReader"/> except where a test scripts
/// a failure (US-017, TC-4): the course listing, or the roster of one course. Nothing reaches Google.
/// </summary>
public sealed class FailingClassroomReader(FakeClassroomReader inner) : IClassroomReader
{
    private readonly Dictionary<string, Exception> _rosterFailures = new(StringComparer.Ordinal);

    /// <summary>Thrown by the course listing when set.</summary>
    public Exception? CourseListingFailure { get; set; }

    /// <summary>Makes the roster read of that course throw.</summary>
    public FailingClassroomReader WithRosterFailure(string courseGoogleId, Exception failure)
    {
        _rosterFailures[courseGoogleId] = failure;
        return this;
    }

    public async IAsyncEnumerable<CourseSnapshot> ReadCoursesAsync(
        string impersonationUser,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (CourseListingFailure is { } failure)
        {
            throw failure;
        }

        await foreach (var course in inner.ReadCoursesAsync(impersonationUser, cancellationToken))
        {
            yield return course;
        }
    }

    public Task<CourseRoster> ReadRosterAsync(
        string impersonationUser,
        string courseGoogleId,
        CancellationToken cancellationToken) =>
        _rosterFailures.TryGetValue(courseGoogleId, out var failure)
            ? Task.FromException<CourseRoster>(failure)
            : inner.ReadRosterAsync(impersonationUser, courseGoogleId, cancellationToken);

    public Task<CourseWorkPage> ReadCourseWorkAsync(
        string impersonationUser,
        string courseGoogleId,
        CancellationToken cancellationToken) =>
        inner.ReadCourseWorkAsync(impersonationUser, courseGoogleId, cancellationToken);

    public Task<IReadOnlyList<SubmissionSnapshot>> ReadSubmissionsAsync(
        string impersonationUser,
        string courseGoogleId,
        CancellationToken cancellationToken) =>
        inner.ReadSubmissionsAsync(impersonationUser, courseGoogleId, cancellationToken);
}
