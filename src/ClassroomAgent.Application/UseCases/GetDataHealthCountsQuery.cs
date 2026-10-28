using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// US-062 entity model §2.4, api-design §2.3: the banner's two counts (spec FR-001.5); null when both are zero. The
/// facts are counted in the database and the problems in <see cref="DataHealthRules"/>, the page's own rules (spec §9).
/// </summary>
public sealed class GetDataHealthCountsQuery(
    IDataHealthSource source,
    ISyncStateRepository syncStates,
    ISyncSkippedCourseRepository skippedCourses,
    ILegitimacyStateRepository legitimacyStates,
    TimeProvider timeProvider)
{
    /// <summary>FR-002.1: both roles see the same counts; <paramref name="role"/> is the caller's, for symmetry with the page.</summary>
    public async Task<DataHealthBannerModel?> ExecuteAsync(AppRole role, CancellationToken cancellationToken)
    {
        var assessment = await DataHealthRules.AssessAsync(
            source, syncStates, skippedCourses, legitimacyStates, timeProvider, cancellationToken);
        var counts = new DataHealthBannerModel(assessment.TechnicalCount, assessment.TeachingCount);
        return counts is { TechnicalCount: 0, TeachingCount: 0 } ? null : counts;
    }
}
