using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Infrastructure.Persistence;

/// <summary>
/// ITeacherListSource over the database (US-022 db-design §3, entity model §2): untracked reads, one command each, no
/// transaction, no write, nothing logged (SC-10).
/// </summary>
public sealed class TeacherListSource(ClassroomAgentDbContext db) : ITeacherListSource
{
    private readonly ClassroomAgentDbContext _db = db;

    public Task<int> CountTeachersAsync(CourseState? state, CancellationToken cancellationToken) =>
        throw new NotImplementedException("US-022 skeleton (OD-010): completed at IMPLEMENTATION.");

    public Task<IReadOnlyList<TeacherListRecord>> GetTeacherPageAsync(
        CourseState? state, int offset, int limit, CancellationToken cancellationToken) =>
        throw new NotImplementedException("US-022 skeleton (OD-010): completed at IMPLEMENTATION.");
}
