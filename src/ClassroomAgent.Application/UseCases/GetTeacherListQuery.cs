using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The teacher list (US-022 spec FR-001 … FR-016). Reads only: no read-only guard, no unit of work, no audit, no Google
/// port (spec FR-013).
/// </summary>
public sealed class GetTeacherListQuery(ITeacherListSource source)
{
    /// <summary>Spec FR-006, OD-006.</summary>
    public const int PageSize = 50;

    private readonly ITeacherListSource _source = source;

    public Task<TeacherListPageResult> ExecuteAsync(TeacherListRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException("US-022 skeleton (OD-010): completed at IMPLEMENTATION.");
}
