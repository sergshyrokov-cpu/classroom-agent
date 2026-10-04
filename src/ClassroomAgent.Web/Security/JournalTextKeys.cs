using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The translation keys of the journal page (US-025 spec FR-015; api-design §2.5). The openapi enums map to one key
/// family each. Every key exists in both the Ukrainian and the English file.
/// </summary>
public static class JournalTextKeys
{
    public const string Title = "Journal.Title";

    public const string NavigationEntry = "Journal.NavigationEntry";

    /// <summary><c>trebovaniya.md</c> v83 sections: the home page heading the journal link sits under.</summary>
    public const string WorkspaceSection = "Journal.WorkspaceSection";

    public const string CourseLabel = "Journal.Form.Course";

    public const string ChooseCourse = "Journal.Form.ChooseCourse";

    public const string FromLabel = "Journal.Form.From";

    public const string ToLabel = "Journal.Form.To";

    public const string Show = "Journal.Form.Show";

    public const string ViewLabel = "Journal.View.Label";

    public const string FullView = "Journal.View.Full";

    public const string ShortView = "Journal.View.Short";

    /// <summary>Spec FR-002: no course has been synchronized yet.</summary>
    public const string NoCourses = "Journal.NoCourses";

    public const string StudentHeader = "Journal.Column.Student";

    /// <summary>Spec FR-004: the maximum points in the header of graded work.</summary>
    public const string MaxPoints = "Journal.Column.MaxPoints";

    /// <summary>Spec FR-004: the marker in the header of a material.</summary>
    public const string Material = "Journal.Column.Material";

    /// <summary>Spec FR-005, OD-005 (a).</summary>
    public const string Unnamed = "Journal.Row.Unnamed";

    public const string Late = "Journal.Mark.Late";

    public const string Draft = "Journal.Mark.Draft";

    public const string TurnedInOn = "Journal.Mark.TurnedInOn";

    /// <summary>Every state but <c>Empty</c> and <c>Grade</c>, which render no sentence (openapi <c>JournalCellState</c>).</summary>
    public static string Cell(JournalCellState state) => "Journal.Cell." + state;

    public static string Validation(JournalMessageKey key) => "Journal.Validation." + key;

    public static string Empty(JournalEmptyStateKey key) => "Journal.Empty." + key;
}
