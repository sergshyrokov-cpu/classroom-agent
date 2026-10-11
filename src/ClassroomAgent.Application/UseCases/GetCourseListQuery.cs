using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The course list (US-020 spec FR-001 … FR-018). Reads only: no read-only guard, no unit of work, no audit, no Google
/// port (spec FR-015).
/// </summary>
/// <remarks>Compile-only skeleton created at TEST_WRITING (US-020 OD-012 a); IMPLEMENTATION completes it.</remarks>
public sealed class GetCourseListQuery(ICourseListSource source, SchoolTimeZone schoolTimeZone)
{
    /// <summary>Spec FR-008, OD-006.</summary>
    public const int PageSize = 50;

    public Task<CourseListPageResult> ExecuteAsync(CourseListRequest request, CancellationToken cancellationToken)
    {
        _ = (source, schoolTimeZone, request, cancellationToken);
        throw new NotImplementedException();
    }
}
