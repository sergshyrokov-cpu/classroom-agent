using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// US-042 spec FR-003: the name a person is shown with, from the surname, the given name, the email and the name
/// source — one rule for students and teachers, independent of any report (I-10). Names are returned as stored: never
/// translated, never corrected.
/// </summary>
internal static class ReportPersonNameRule
{
    /// <summary>(null, <see cref="ReportNameKind.Unnamed"/>) when nothing applies; the caller picks the label.</summary>
    public static (string? Name, ReportNameKind Kind) Label(
        string? surname, string? givenName, string? email, ReportNameSource source)
    {
        if (source == ReportNameSource.Profile)
        {
            var hasSurname = !string.IsNullOrEmpty(surname);
            var hasGivenName = !string.IsNullOrEmpty(givenName);
            if (hasSurname || hasGivenName)
            {
                // I-1: one part alone is shown alone; both are joined by one space.
                var name = hasSurname && hasGivenName ? surname + " " + givenName : hasSurname ? surname : givenName;
                return (name, ReportNameKind.Profile);
            }
        }

        return EmailLocalPart(email) is { } localPart
            ? (localPart, ReportNameKind.EmailLocalPart)
            : (null, ReportNameKind.Unnamed);
    }

    /// <summary>The stored address up to its first <c>@</c>; none without <c>@</c> or with nothing before it (I-5).</summary>
    private static string? EmailLocalPart(string? email)
    {
        var at = email?.IndexOf('@', StringComparison.Ordinal) ?? -1;
        return at > 0 ? email![..at] : null;
    }
}
