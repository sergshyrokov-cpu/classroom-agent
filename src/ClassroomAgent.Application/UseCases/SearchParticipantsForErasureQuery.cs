using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// US-038 spec FR-002, VR-001: validates the term (trimmed, 2 … 254 characters) and searches every participant; at
/// most <see cref="MaxResults"/> results, the source asked for one more to tell whether there were more. Writes nothing.
/// </summary>
public sealed class SearchParticipantsForErasureQuery(IParticipantErasureReadSource source)
{
    public const int MaxResults = 50;

    public const int MinTermLength = 2;

    public const int MaxTermLength = 254;

    public async Task<SearchBlock> ExecuteAsync(string? term, CancellationToken cancellationToken)
    {
        var trimmed = term?.Trim() ?? string.Empty;
        SearchFieldError? error = trimmed.Length switch
        {
            0 => SearchFieldError.TermRequired,
            < MinTermLength => SearchFieldError.TermTooShort,
            > MaxTermLength => SearchFieldError.TermTooLong,
            _ => null,
        };
        if (error is not null)
        {
            return new SearchBlock(term, error, false, [], false);
        }

        var page = await source.SearchAsync(trimmed, MaxResults + 1, cancellationToken);
        var results = page.Participants
            .Take(MaxResults)
            .Select(p => new ParticipantSearchResult(p.ParticipantId, p.FullName, p.Email, p.TeacherCourses, p.StudentCourses))
            .ToList();
        return new SearchBlock(trimmed, null, true, results, page.Participants.Count > MaxResults);
    }
}
