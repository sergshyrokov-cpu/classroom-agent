using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The fixed values of US-027: the paths of api-design §1, the built-in key, the form field names of api-design §2.7,
/// the 12-point preset of OD-002 and the translation key families of the openapi enums. Kept in one place so a rename
/// shows up as one edit (the <see cref="JournalTestData"/> pattern). Every name and text is invented (TC-4).
/// </summary>
public static class ReportTemplateTestData
{
    public const string ListPath = "/reports/templates";

    public const string NewPath = "/reports/templates/new";

    public const string ReportPath = "/reports";

    /// <summary>api-design §2.2: the built-in template's key.</summary>
    public const string BuiltInKey = "academic-journal";

    public static string CopyPath(string reference) => $"/reports/templates/{reference}/copy";

    public static string EditPath(string reference) => $"/reports/templates/{reference}/edit";

    public static string ChangePath(string reference) => $"/reports/templates/{reference}";

    public static string DeletionPath(string reference) => $"/reports/templates/{reference}/deletion";

    /// <summary>The query string of a report request; null values are left out.</summary>
    public static string ReportUrl(string? template = null, long? courseId = null, string? from = null, string? to = null)
    {
        var parts = new List<string>();
        if (template is not null)
        {
            parts.Add("template=" + Uri.EscapeDataString(template));
        }

        if (courseId is not null)
        {
            parts.Add("courseId=" + courseId);
        }

        if (from is not null)
        {
            parts.Add("from=" + Uri.EscapeDataString(from));
        }

        if (to is not null)
        {
            parts.Add("to=" + Uri.EscapeDataString(to));
        }

        return parts.Count == 0 ? ReportPath : ReportPath + "?" + string.Join('&', parts);
    }

    /// <summary>The September report of one course (the <see cref="JournalTestData.Period"/> period).</summary>
    public static string SeptemberReportUrl(long courseId, string template = BuiltInKey) =>
        ReportUrl(template, courseId, JournalTestData.Period.FromText, JournalTestData.Period.ToText);

    /// <summary>Every member of <see cref="ReportCellState"/>, in declaration order (spec FR-003).</summary>
    public static IReadOnlyList<ReportCellState> States { get; } = Enum.GetValues<ReportCellState>();

    /// <summary>OD-002 / spec FR-015: the 12-point preset as (from, to, label).</summary>
    public static IReadOnlyList<(int From, int To, string Label)> TwelvePoint { get; } =
    [
        (0, 8, "1"), (9, 16, "2"), (17, 25, "3"), (26, 33, "4"), (34, 41, "5"), (42, 50, "6"),
        (51, 58, "7"), (59, 66, "8"), (67, 75, "9"), (76, 83, "10"), (84, 91, "11"), (92, 100, "12"),
    ];

    /// <summary>A two-row scale used where the exact preset does not matter: below 50 % "low", from 50 % "high".</summary>
    public static IReadOnlyList<(int From, int To, string Label)> TwoRow { get; } = [(0, 49, "low"), (50, 100, "high")];

    /// <summary>Settings built directly (for repository and report tests), every mark program, late program.</summary>
    public static ReportTemplateSettings Settings(
        ReportView view = ReportView.Full,
        bool hideMaterials = false,
        int hours = 2,
        IReadOnlyList<(int From, int To, string Label)>? scale = null,
        IReadOnlyDictionary<ReportCellState, ReportMark>? marks = null,
        ReportLateMark? late = null)
    {
        var rows = (scale ?? []).Select(r => new ReportScaleRow(r.From, r.To, r.Label)).ToList();
        var allMarks = States.ToDictionary(s => s, s => marks is not null && marks.TryGetValue(s, out var m) ? m : new ReportMark(ReportMarkKind.Program, null));
        return new ReportTemplateSettings(
            view,
            hideMaterials,
            hours,
            rows.Count == 0 ? ReportScaleMode.None : ReportScaleMode.Ranges,
            rows,
            allMarks,
            late ?? new ReportLateMark(ReportLateMarkKind.Program, null));
    }

    /// <summary>The translation keys this Story adds or reuses (spec FR-017; api-design §2.9).</summary>
    public static class TextKeys
    {
        public const string BuiltInName = "ReportTemplate.BuiltIn.AcademicJournal";

        public static string Validation(ReportTemplateFieldErrorKey key) => "ReportTemplate.Validation." + key;

        public static string Reference(ReportTemplateReferenceMessageKey key) => "ReportTemplate.Reference." + key;

        public static string Confirmation(ReportTemplateConfirmationKey key) => "ReportTemplate.Confirmation." + key;

        public static string Empty(ReportEmptyStateKey key) => "Report.Empty." + key;

        public const string NoStudents = "Report.Empty.NoStudents";

        public const string TeacherUnnamed = "Report.Teacher.Unnamed";

        /// <summary>The keys the openapi enums name explicitly; the missing-key test checks them in both files.</summary>
        public static IEnumerable<string> All =>
            new[] { BuiltInName, NoStudents, TeacherUnnamed }
                .Concat(Enum.GetValues<ReportTemplateFieldErrorKey>().Select(Validation))
                .Concat(Enum.GetValues<ReportTemplateReferenceMessageKey>().Select(Reference))
                .Concat(Enum.GetValues<ReportTemplateConfirmationKey>().Select(Confirmation))
                .Concat(Enum.GetValues<ReportEmptyStateKey>().Select(Empty));
    }
}
