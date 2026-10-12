using System.Globalization;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Domain.Enums;
using static ClassroomAgent.Application.UseCases.JournalQueryRules;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The status and page rules shared by the course list (US-020 spec VR-001, VR-004) and the teacher list (US-022 spec
/// VR-001, VR-002, api-design §2.2, entity model §3): one rule, one set of tokens, so a status carries over unchanged.
/// </summary>
internal static class CourseListQueryRules
{
    /// <summary>US-020 spec VR-004, I-5: the largest page number accepted.</summary>
    private const int MaxPage = 100_000;

    /// <summary>US-020 spec VR-001: absent is ACTIVE; one exact token, case-insensitive, untrimmed. False when malformed.</summary>
    internal static bool TryReadStatus(IReadOnlyList<string?> values, out CourseStatusFilter status)
    {
        status = CourseStatusFilter.Active;
        switch (Single(values, out var text))
        {
            case Presence.Absent:
                return true;
            case Presence.One:
                foreach (var candidate in Enum.GetValues<CourseStatusFilter>())
                {
                    if (string.Equals(text, CourseListUrl.Token(candidate), StringComparison.OrdinalIgnoreCase))
                    {
                        status = candidate;
                        return true;
                    }
                }

                return false;
            default:
                return false;
        }
    }

    /// <summary>US-020 spec VR-004: absent is 1; one integer of ASCII digits from 1 to 100 000. False when malformed.</summary>
    internal static bool TryReadPage(IReadOnlyList<string?> values, out int page)
    {
        switch (Single(values, out var text))
        {
            case Presence.Absent:
                page = 1;
                return true;
            case Presence.One
                when text.Length <= 6
                     && text.All(char.IsAsciiDigit)
                     && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                     && parsed is >= 1 and <= MaxPage:
                page = parsed;
                return true;
            default:
                page = 1;
                return false;
        }
    }

    /// <summary>The stored state a filter selects; null for "all".</summary>
    internal static CourseState? StateOf(CourseStatusFilter status) => status switch
    {
        CourseStatusFilter.Active => CourseState.Active,
        CourseStatusFilter.Archived => CourseState.Archived,
        CourseStatusFilter.Provisioned => CourseState.Provisioned,
        CourseStatusFilter.Declined => CourseState.Declined,
        CourseStatusFilter.Suspended => CourseState.Suspended,
        _ => null,
    };
}
