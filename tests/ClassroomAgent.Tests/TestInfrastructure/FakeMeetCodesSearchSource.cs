using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-071: the two reads of <see cref="IMeetCodesReadSource"/> the header search uses, for the query unit tests (TC-1); the
/// search must call no other member, and any other member fails the test.
/// </summary>
public sealed class FakeMeetCodesSearchSource : IMeetCodesReadSource
{
    public MeetCodeListCounts Counts { get; set; } = new(0, 0, 0);

    public List<MeetCodeMatch> Matches { get; } = [];

    public List<string> Calls { get; } = [];

    /// <summary>The text and limit of every first-matches read.</summary>
    public List<(string Search, int Limit)> MatchReads { get; } = [];

    /// <summary>The text of every count read.</summary>
    public List<string?> CountReads { get; } = [];

    public Task<MeetCodeListCounts> GetCountsAsync(string? search, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(GetCountsAsync));
        CountReads.Add(search);
        return Task.FromResult(Counts);
    }

    public Task<IReadOnlyList<MeetCodeMatch>> GetFirstMatchesAsync(string search, int limit, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(GetFirstMatchesAsync));
        MatchReads.Add((search, limit));
        return Task.FromResult<IReadOnlyList<MeetCodeMatch>>(Matches.Take(limit).ToList());
    }

    public Task<PagedRows<UnassignedCodeRow>> GetUnassignedPageAsync(
        int page, int size, TimeZoneInfo zone, string? search, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The search reads no list page.");

    public Task<PagedRows<LinkedCodeRow>> GetLinkedPageAsync(int page, int size, string? search, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The search reads no list page.");

    public Task<PagedRows<MarkedCodeRow>> GetMarkedPageAsync(int page, int size, string? search, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The search reads no list page.");

    public Task<IReadOnlyList<CourseOption>> GetAllCoursesAsync(CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The search reads no course options.");

    public Task<IReadOnlyList<MeetCourseAbsenceRecord>> GetCourseAbsencesAsync(CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The search reads no course absences.");
}
