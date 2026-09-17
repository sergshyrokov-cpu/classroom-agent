namespace ClassroomAgent.Domain.Rules;

/// <summary>
/// How long an installation keeps working without a successful legitimacy check (NFR-013, BR-025):
/// more than 7 days — strictly — since the last success ends the grace period (US-005 spec FR-008, I-4).
/// </summary>
public static class GracePeriod
{
    public static readonly TimeSpan Length = TimeSpan.FromDays(7);

    public static bool HasExpired(DateTimeOffset lastSuccessfulCheckAt, DateTimeOffset now) =>
        now - lastSuccessfulCheckAt > Length;
}
