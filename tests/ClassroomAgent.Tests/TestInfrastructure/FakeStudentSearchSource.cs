using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>US-071: an in-memory <see cref="IStudentSearchSource"/> for the query unit tests (TC-1). Every call is recorded.</summary>
public sealed class FakeStudentSearchSource : IStudentSearchSource
{
    public List<StudentSearchRecord> Students { get; } = [];

    public List<StudentSearchCourseRecord> Courses { get; } = [];

    /// <summary>The total to answer; the number of stored students when null.</summary>
    public int? Total { get; set; }

    public List<string> Calls { get; } = [];

    /// <summary>The text and limit of every rows read.</summary>
    public List<(string Search, int Limit)> RowReads { get; } = [];

    public List<IReadOnlyCollection<long>> CourseReads { get; } = [];

    public Task<int> CountStudentsAsync(string search, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(CountStudentsAsync));
        return Task.FromResult(Total ?? Students.Count);
    }

    public Task<IReadOnlyList<StudentSearchRecord>> GetStudentsAsync(string search, int limit, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(GetStudentsAsync));
        RowReads.Add((search, limit));
        return Task.FromResult<IReadOnlyList<StudentSearchRecord>>(Students.Take(limit).ToList());
    }

    public Task<IReadOnlyList<StudentSearchCourseRecord>> GetStudentCoursesAsync(
        IReadOnlyCollection<long> participantIds, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(GetStudentCoursesAsync));
        CourseReads.Add(participantIds);
        return Task.FromResult<IReadOnlyList<StudentSearchCourseRecord>>(
            Courses.Where(c => participantIds.Contains(c.ParticipantId)).ToList());
    }
}
