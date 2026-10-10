using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence.Repositories;

/// <summary>
/// The links of meeting codes (US-032 db-design §2, §7). Stages changes; never saves (AD-7). The row is loaded tracked,
/// so its concurrency stamp guards the later save.
/// </summary>
public sealed class MeetingCodeLinkRepository(ClassroomAgentDbContext db) : IMeetingCodeLinkRepository
{
    /// <summary>The row of exactly this code (ordinal, as the unique index compares), tracked, or null.</summary>
    public Task<MeetingCodeLink?> GetByCodeAsync(string meetingCode, CancellationToken cancellationToken) =>
        db.MeetingCodeLinks.SingleOrDefaultAsync(l => l.MeetingCode == meetingCode, cancellationToken);

    /// <summary>Whether any stored meeting carries exactly this code.</summary>
    public Task<bool> CodeHasMeetingsAsync(string meetingCode, CancellationToken cancellationToken) =>
        db.MeetSessions.AsNoTracking().AnyAsync(s => s.MeetingCode == meetingCode, cancellationToken);

    public Task<bool> CourseExistsAsync(long courseId, CancellationToken cancellationToken) =>
        db.Courses.AsNoTracking().AnyAsync(c => c.Id == courseId, cancellationToken);

    public void Add(MeetingCodeLink link) => db.MeetingCodeLinks.Add(link);
}
