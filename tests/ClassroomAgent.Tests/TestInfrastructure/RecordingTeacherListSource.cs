using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>US-070: the real teacher list source, with every call written to a <see cref="ListSearchReadLog"/>.</summary>
public sealed class RecordingTeacherListSource(ITeacherListSource inner, ListSearchReadLog log) : ITeacherListSource
{
    public Task<int> CountTeachersAsync(CourseState? state, string? search, CancellationToken cancellationToken)
    {
        log.Add(nameof(CountTeachersAsync), search);
        return inner.CountTeachersAsync(state, search, cancellationToken);
    }

    public Task<IReadOnlyList<TeacherListRecord>> GetTeacherPageAsync(
        CourseState? state, string? search, int offset, int limit, CancellationToken cancellationToken)
    {
        log.Add(nameof(GetTeacherPageAsync), search);
        return inner.GetTeacherPageAsync(state, search, offset, limit, cancellationToken);
    }
}
