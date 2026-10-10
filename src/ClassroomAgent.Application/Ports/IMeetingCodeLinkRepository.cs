using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// The links of meeting codes (US-032 entity model §5). <see cref="CourseExistsAsync"/> lives here rather than on the
/// read source so the three write use cases depend on one port (test-generation report §2).
/// </summary>
public interface IMeetingCodeLinkRepository
{
    Task<MeetingCodeLink?> GetByCodeAsync(string meetingCode, CancellationToken cancellationToken);

    Task<bool> CodeHasMeetingsAsync(string meetingCode, CancellationToken cancellationToken);

    Task<bool> CourseExistsAsync(long courseId, CancellationToken cancellationToken);

    void Add(MeetingCodeLink link);
}
