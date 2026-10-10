using ClassroomAgent.Application.MeetLinking;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// Bounded reads for scoring meeting codes (US-032 db-design §5.2): the unassigned codes in code order, and for a set
/// of codes their meetings with participant emails plus every membership whose participant email equals,
/// case-insensitively, an organizer or participant email of those meetings.
/// </summary>
public interface IMeetCodeScoringSource
{
    /// <summary>Codes with at least one meeting and no link row, ordinal order, strictly after <paramref name="afterCode"/>.</summary>
    Task<IReadOnlyList<string>> GetUnassignedCodesAsync(string? afterCode, int batchSize, CancellationToken cancellationToken);

    /// <summary>One input per requested code that has meetings; participant emails exclude connections without one.</summary>
    Task<IReadOnlyList<MeetCodeScoringInput>> GetScoringInputsAsync(
        IReadOnlyCollection<string> meetingCodes,
        CancellationToken cancellationToken);
}
