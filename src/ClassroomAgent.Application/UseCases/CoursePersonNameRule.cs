using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The person's name on the course pages (US-020 spec FR-007, US-021 spec FR-005; US-021 entity model §4): one rule for
/// the course list and the course card.
/// </summary>
internal static class CoursePersonNameRule
{
    /// <summary>Profile name, else the full email, else no name — as stored.</summary>
    public static CourseListPersonName NameOf(string? surname, string? givenName, string? email)
    {
        var parts = new[] { surname, givenName }.Where(p => !string.IsNullOrEmpty(p)).ToList();
        if (parts.Count > 0)
        {
            return new CourseListPersonName(CourseListPersonNameKind.ProfileName, string.Join(' ', parts));
        }

        return string.IsNullOrEmpty(email)
            ? new CourseListPersonName(CourseListPersonNameKind.NoName, null)
            : new CourseListPersonName(CourseListPersonNameKind.Email, email);
    }
}
