namespace ClassroomAgent.Application.MeetLinking;

/// <summary>
/// "On the roster on date D" (US-032 spec FR-002, I-1): first seen on or before D and either still on the roster or
/// last seen on or after D, both taken as whole dates in the school's time zone.
/// </summary>
public static class RosterOnDate
{
    public static bool Covers(RosterMembership membership, DateOnly date, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(membership);
        if (LocalDate(membership.FirstSeenAt, zone) > date)
        {
            return false;
        }

        return membership.OnRoster || LocalDate(membership.LastSeenAt, zone) >= date;
    }

    /// <summary>The calendar date of an instant in the school's zone (NFR-074).</summary>
    public static DateOnly LocalDate(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
}
