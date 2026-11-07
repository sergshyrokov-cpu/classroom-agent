namespace ClassroomAgent.Web.Security;

/// <summary>
/// The page help of US-076 (spec FR-001 … FR-006, api-design §3): its five page ids, the view-data key a view sets to
/// one of them, and the translation keys of each page's lines and of the control's accessible name. Every key exists in
/// both the Ukrainian and the English file; the texts themselves live only there.
/// </summary>
public static class PageHelpTextKeys
{
    /// <summary>The view-data entry a view sets to its page id (spec FR-004, VR-001); the layout reads it.</summary>
    public const string ViewDataKey = "PageHelp";

    public const string Journal = "journal";

    public const string MeetCodes = "meet-codes";

    public const string MeetReports = "meet-reports";

    public const string Meeting = "meeting";

    public const string ReportTemplates = "report-templates";

    /// <summary>The control's accessible name.</summary>
    public const string ControlName = "PageHelp.Control.Name";

    /// <summary>
    /// The page id when the value is one of the five, else null — a missing or unknown id renders the plain title (VR-001).
    /// </summary>
    public static string? PageOf(object? value) =>
        value is string page && LineCount(page) > 0 ? page : null;

    /// <summary>The keys of a page's lines, in order (api-design §3); empty for an unknown page.</summary>
    public static IReadOnlyList<string> LineKeys(string page)
    {
        var count = LineCount(page);
        var keys = new string[count];
        for (var n = 1; n <= count; n++)
        {
            keys[n - 1] = $"PageHelp.{KeySegment(page)}.Line{n}";
        }

        return keys;
    }

    private static int LineCount(string page) => page switch
    {
        Journal or MeetCodes => 4,
        MeetReports or Meeting or ReportTemplates => 3,
        _ => 0,
    };

    private static string KeySegment(string page) => page switch
    {
        Journal => "Journal",
        MeetCodes => "MeetCodes",
        MeetReports => "MeetReports",
        Meeting => "Meeting",
        ReportTemplates => "ReportTemplates",
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, null),
    };
}
