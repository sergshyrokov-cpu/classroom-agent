using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// US-071 db-design §3: the students of the header search. <c>search</c> is the validated text (VR-001), at least 2
/// characters (VR-003), whole and not split: the words are the Infrastructure helper's.
/// </summary>
public interface IStudentSearchSource
{
    Task<int> CountStudentsAsync(string search, CancellationToken cancellationToken);

    Task<IReadOnlyList<StudentSearchRecord>> GetStudentsAsync(string search, int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<StudentSearchCourseRecord>> GetStudentCoursesAsync(
        IReadOnlyCollection<long> participantIds, CancellationToken cancellationToken);
}
