using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Infrastructure.Persistence;

/// <summary>ICourseListSource over the database (US-020 db-design §3, entity model §2): untracked reads, no write.</summary>
/// <remarks>Compile-only skeleton created at TEST_WRITING (US-020 OD-012 a); IMPLEMENTATION completes it.</remarks>
public sealed class CourseListSource(ClassroomAgentDbContext db) : ICourseListSource
{
    public Task<IReadOnlyList<CourseListPersonRecord>> GetTeacherChoiceAsync(CancellationToken cancellationToken)
    {
        _ = (db, cancellationToken);
        throw new NotImplementedException();
    }

    public Task<int> CountCoursesAsync(CourseListFilter filter, CancellationToken cancellationToken)
    {
        _ = (filter, cancellationToken);
        throw new NotImplementedException();
    }

    public Task<bool> AnyCourseStoredAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<CourseListCourseRecord>> GetCoursePageAsync(
        CourseListFilter filter, int offset, int limit, CancellationToken cancellationToken)
    {
        _ = (filter, offset, limit, cancellationToken);
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<CourseListTeacherRecord>> GetCurrentTeachersAsync(
        IReadOnlyCollection<long> courseIds, CancellationToken cancellationToken)
    {
        _ = (courseIds, cancellationToken);
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<CourseListStudentCountRecord>> GetStudentCountsAsync(
        IReadOnlyCollection<long> courseIds, CancellationToken cancellationToken)
    {
        _ = (courseIds, cancellationToken);
        throw new NotImplementedException();
    }
}
