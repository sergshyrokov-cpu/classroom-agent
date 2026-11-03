using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>US-070: the real Meet codes read source, with every list read written to a <see cref="ListSearchReadLog"/>.</summary>
public sealed class RecordingMeetCodesReadSource(IMeetCodesReadSource inner, ListSearchReadLog log) : IMeetCodesReadSource
{
    public Task<MeetCodeListCounts> GetCountsAsync(string? search, CancellationToken cancellationToken)
    {
        log.Add(nameof(GetCountsAsync), search);
        return inner.GetCountsAsync(search, cancellationToken);
    }

    public Task<PagedRows<UnassignedCodeRow>> GetUnassignedPageAsync(
        int page, int size, TimeZoneInfo zone, string? search, CancellationToken cancellationToken)
    {
        log.Add(nameof(GetUnassignedPageAsync), search);
        return inner.GetUnassignedPageAsync(page, size, zone, search, cancellationToken);
    }

    public Task<PagedRows<LinkedCodeRow>> GetLinkedPageAsync(int page, int size, string? search, CancellationToken cancellationToken)
    {
        log.Add(nameof(GetLinkedPageAsync), search);
        return inner.GetLinkedPageAsync(page, size, search, cancellationToken);
    }

    public Task<PagedRows<MarkedCodeRow>> GetMarkedPageAsync(int page, int size, string? search, CancellationToken cancellationToken)
    {
        log.Add(nameof(GetMarkedPageAsync), search);
        return inner.GetMarkedPageAsync(page, size, search, cancellationToken);
    }

    public Task<IReadOnlyList<MeetCodeMatch>> GetFirstMatchesAsync(string search, int limit, CancellationToken cancellationToken)
    {
        log.Add(nameof(GetFirstMatchesAsync), search);
        return inner.GetFirstMatchesAsync(search, limit, cancellationToken);
    }

    public Task<IReadOnlyList<CourseOption>> GetAllCoursesAsync(CancellationToken cancellationToken) =>
        inner.GetAllCoursesAsync(cancellationToken);

    public Task<IReadOnlyList<MeetCourseAbsenceRecord>> GetCourseAbsencesAsync(CancellationToken cancellationToken) =>
        inner.GetCourseAbsencesAsync(cancellationToken);
}
