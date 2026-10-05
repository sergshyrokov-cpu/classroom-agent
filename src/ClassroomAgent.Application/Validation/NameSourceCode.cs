using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Validation;

/// <summary>
/// The one value type of the template form field and the report query parameter <c>names</c> (US-042 api-design §2.1,
/// spec VR-001, VR-002): exactly <c>profile</c> or <c>email</c> — case-sensitive, not trimmed. Public so the pages render
/// the same codes they post.
/// </summary>
public static class NameSourceCode
{
    public const string Profile = "profile";

    public const string Email = "email";

    /// <summary>The form field and the query parameter.</summary>
    public const string FieldName = "names";

    public static string Of(ReportNameSource source) => source == ReportNameSource.Email ? Email : Profile;

    public static bool TryParse(string? text, out ReportNameSource source)
    {
        source = text == Email ? ReportNameSource.Email : ReportNameSource.Profile;
        return text is Profile or Email;
    }

    /// <summary>VR-001, VR-002: one occurrence of a known value; empty, repeated or unknown is malformed.</summary>
    public static bool TryParseSingle(IReadOnlyList<string?> values, out ReportNameSource source)
    {
        source = ReportNameSource.Profile;
        return values.Count == 1 && TryParse(values[0], out source);
    }
}
