using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// The reads of the Meet meetings page (US-032 db-design §5). Unassigned: codes with candidates first (the SQL test of
/// db-design §5.2 in <paramref name="zone"/>), then latest last meeting, then code (ordinal). Linked: latest last
/// meeting first, codes with no meeting last, then code. Marked: latest mark first, then code.
/// </summary>
public interface IMeetCodesReadSource
{
    Task<MeetCodeListCounts> GetCountsAsync(CancellationToken cancellationToken);

    Task<PagedRows<UnassignedCodeRow>> GetUnassignedPageAsync(
        int page,
        int size,
        TimeZoneInfo zone,
        CancellationToken cancellationToken);

    Task<PagedRows<LinkedCodeRow>> GetLinkedPageAsync(int page, int size, CancellationToken cancellationToken);

    Task<PagedRows<MarkedCodeRow>> GetMarkedPageAsync(int page, int size, CancellationToken cancellationToken);

    /// <summary>Every stored course (id, name, section), any order.</summary>
    Task<IReadOnlyList<Models.Dtos.CourseOption>> GetAllCoursesAsync(CancellationToken cancellationToken);
}
