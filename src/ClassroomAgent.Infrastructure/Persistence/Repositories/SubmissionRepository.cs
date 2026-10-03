using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence.Repositories;

/// <summary>
/// The installation's <c>submission</c> table (US-015 entity model §7). Stages changes; never saves (AD-7).
/// </summary>
public sealed class SubmissionRepository(ClassroomAgentDbContext db) : ISubmissionRepository
{
    /// <summary>Tracked, so the upsert's update half changes the row it finds (spec FR-010). One query per course.</summary>
    public async Task<IReadOnlyList<Submission>> GetByCourseWorkIdsAsync(
        IReadOnlyCollection<long> courseWorkIds,
        CancellationToken cancellationToken) =>
        await db.Submissions.Where(s => courseWorkIds.Contains(s.CourseWorkId)).ToListAsync(cancellationToken);

    public void Add(Submission submission) => db.Submissions.Add(submission);
}
