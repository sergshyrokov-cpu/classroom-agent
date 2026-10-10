namespace ClassroomAgent.Application.MeetLinking;

/// <summary>A candidate course and its exact share (US-032 spec FR-003, I-4): compared as a fraction, shown rounded down.</summary>
public sealed record CandidateShare(long CourseId, int Numerator, int Denominator)
{
    /// <summary>The share as a whole percent, rounded down; 0 when the denominator is 0.</summary>
    public int PercentRoundedDown => Denominator == 0 ? 0 : (int)(100L * Numerator / Denominator);
}
