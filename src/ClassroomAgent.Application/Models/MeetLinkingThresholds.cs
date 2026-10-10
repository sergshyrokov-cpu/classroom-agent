namespace ClassroomAgent.Application.Models;

/// <summary>
/// The automatic-linking thresholds from installation configuration (US-032 spec FR-004, FR-005; OD-006):
/// <c>MeetLinking:MinSharePercent</c> and <c>MeetLinking:MinGapPoints</c>, whole numbers 1 … 100.
/// </summary>
public sealed record MeetLinkingThresholds(int MinSharePercent, int MinGapPoints)
{
    /// <summary>The values used when neither setting is present (OD-006 a).</summary>
    public static MeetLinkingThresholds Default { get; } = new(60, 30);
}
