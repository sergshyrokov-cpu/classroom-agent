using System.Globalization;
using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The translation keys of the data-health banner and the «Стан даних» page (US-062 spec FR-007, OD-003; api-design §5).
/// Every key exists in both the Ukrainian and the English file.
/// </summary>
public static class DataHealthTextKeys
{
    public const string Title = "DataHealth.Title";

    public const string Banner = "DataHealth.Banner";

    public const string NoProblems = "DataHealth.NoProblems";

    public const string None = "DataHealth.None";

    public const string SectionTechnical = "DataHealth.Section.Technical";

    public const string SectionTeaching = "DataHealth.Section.Teaching";

    public const string SectionArchived = "DataHealth.Section.Archived";

    public const string AboveMaximum = "DataHealth.Teaching.AboveMaximum";

    public const string ItemsNotInClassroom = "DataHealth.Teaching.ItemsNotInClassroom";

    public const string NotUpdatedSince = "DataHealth.Teaching.NotUpdatedSince";

    public const string NotUpdated = "DataHealth.Teaching.NotUpdated";

    public const string NeverLoaded = "DataHealth.Teaching.NeverLoaded";

    public const string NotInClassroomSince = "DataHealth.Teaching.NotInClassroomSince";

    public const string ItemSince = "DataHealth.Teaching.ItemSince";

    public const string HintSkipped = "DataHealth.Hint.Skipped";

    public const string HintNotInClassroom = "DataHealth.Hint.NotInClassroom";

    public const string TellTheAdmin = "DataHealth.Hint.TellTheAdmin";

    public const string ContactTheAdmin = "DataHealth.Hint.ContactTheAdmin";

    public const string TellTheOwner = "DataHealth.Hint.TellTheOwner";

    /// <summary>US-075 FR-002.1: the hint under «Бали вище максимуму» (T1).</summary>
    public const string HintAboveMaximum = "DataHealth.Hint.AboveMaximum";

    /// <summary>US-075 FR-002.2: the hint under the items not in Classroom (T2).</summary>
    public const string HintItemsNotInClassroom = "DataHealth.Hint.ItemsNotInClassroom";

    /// <summary>US-075 FR-004.2, OD-001 a: the Dean's X4 title, ending at its date.</summary>
    public const string CourseListEmptyDean = "DataHealth.Technical.CourseListEmpty.Dean";

    public const string Untitled = "DataHealth.Course.Untitled";

    public const string UpgradeRequired = "DataHealth.Technical.UpgradeRequired";

    public const string SkippedSince = "DataHealth.Technical.SkippedSince";

    public const string LastSuccess = "DataHealth.Technical.LastSuccess";

    public const string FinishedAt = "DataHealth.Technical.FinishedAt";

    public const string ConnectionPage = "DataHealth.Technical.ConnectionPage";

    public const string MarkedSince = "DataHealth.Technical.MarkedSince";

    public const string LastCompleteRead = "DataHealth.Technical.LastCompleteRead";

    public const string NeverRead = "DataHealth.Technical.NeverRead";

    public const string ConfirmButton = "DataHealth.Confirm.Button";

    /// <summary>FR-007: the title of a technical problem (X1 … X6).</summary>
    public static string Of(TechnicalProblemKind kind) => "DataHealth.Technical." + kind;

    /// <summary>US-075 FR-003: the Admin's hint under a technical problem (X2a, X2b, X3, X4, X5).</summary>
    public static string Hint(TechnicalProblemKind kind) => "DataHealth.Hint." + kind;

    /// <summary>The one-shot message of the page.</summary>
    public static string Of(DataHealthMessage message) => "DataHealth.Message." + message;

    /// <summary>
    /// FR-003.2: the banner part's key in the current UI language's plural category — Ukrainian one / few / many, English
    /// one / other (the English file gives "few" the "other" text).
    /// </summary>
    public static string BannerPart(string part, int count) => "DataHealth.Banner." + part + "." + PluralCategory(count);

    private static string PluralCategory(int count)
    {
        if (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "uk")
        {
            return count == 1 ? "One" : "Many";
        }

        return (count % 10, count % 100) switch
        {
            (1, not 11) => "One",
            (2 or 3 or 4, < 12 or > 14) => "Few",
            _ => "Many",
        };
    }
}
