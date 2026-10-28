using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// US-062 entity model §2.1, db-design §5: the stored facts the data-health banner and page are computed from. Reads
/// only, untracked; never calls Google (spec FR-001.1).
/// </summary>
public interface IDataHealthSource
{
    /// <summary>Db-design §5.1: one row per course with a mark, an above-maximum grade, an item gone or a skip record.</summary>
    Task<IReadOnlyList<CourseHealthFact>> GetCourseFactsAsync(CancellationToken cancellationToken);

    /// <summary>Db-design §5.3: every grade above its item's maximum in the given courses, by item title then student.</summary>
    Task<IReadOnlyList<AboveMaximumGradeRow>> GetAboveMaximumGradesAsync(
        IReadOnlyCollection<long> courseIds, CancellationToken cancellationToken);

    /// <summary>Db-design §5.3: every item marked "not in Classroom" in the given courses.</summary>
    Task<IReadOnlyList<ItemNotInClassroomRow>> GetItemsNotInClassroomAsync(
        IReadOnlyCollection<long> courseIds, CancellationToken cancellationToken);
}
