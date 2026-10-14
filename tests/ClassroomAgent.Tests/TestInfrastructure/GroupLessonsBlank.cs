using ClosedXML.Excel;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-043 test oracle of the shipped form "Бланк для групових занять" (spec FR-001 … FR-003, FR-006). The cell
/// addresses below are the facts spec FR-002 records about the shipped file "for whoever prepares it"; production code
/// never contains them (OD-008 a), the tests use them to read the result. <see cref="FormPath"/> is the versioned file
/// (OD-009 a), opened read-only.
/// </summary>
public static class GroupLessonsBlank
{
    public const string Layout = "group-lessons-blank";

    public const string ProgramLayout = "program";

    /// <summary>The form's own sheet names — the school's printed form, never translated (spec FR-006).</summary>
    public const string GradingSheet = "Оцінювання";

    public const string TopicsSheet = "Теми занять";

    /// <summary>Spec FR-003 capacities.</summary>
    public const int StudentRows = 30;

    public const int LessonColumns = 24;

    public const int TopicRows = 23;

    /// <summary>VR-001: the per-copy maximums and defaults (23 = lesson columns minus the final-grade column).</summary>
    public const int StudentsPerCopyMax = 30;

    public const int LessonsPerCopyMax = 23;

    /// <summary>OD-011 a: the most sheets one file may have.</summary>
    public const int SheetLimit = 100;

    /// <summary>Spec I-4: the date format set on the two date marker cells.</summary>
    public const string DateFormat = "dd.mm.yyyy";

    /// <summary>Spec I-14: 0.5 cm, in inches as Excel stores margins.</summary>
    public const double FooterMarginInches = 0.5 / 2.54;

    // Spec FR-002: the first cell of each area in the shipped file.
    public const int CourseRow = 2;

    public const int CourseColumn = 1;

    public const int FirstStudentRow = 9;

    public const int StudentNumberColumn = 1;

    public const int StudentNameColumn = 2;

    public const int LessonDateRow = 8;

    public const int FirstLessonColumn = 3;

    public const int TeachersRow = 1;

    public const int TeachersColumn = 4;

    public const int FirstTopicRow = 4;

    public const int TopicNumberColumn = 1;

    public const int TopicDateColumn = 2;

    public const int TopicHoursColumn = 3;

    public const int TopicTitleColumn = 4;

    /// <summary>"Самостійна робота студента" and "Підпис викладача": never written (spec FR-005.4).</summary>
    public const int TopicIndependentWorkColumn = 5;

    public const int TopicSignatureColumn = 6;

    /// <summary>The cells compared with the form as "untouched" (beyond the forms' drawn lines on every side).</summary>
    public const string GradingCompareRange = "A1:AB45";

    public const string TopicsCompareRange = "A1:H32";

    /// <summary>Spec FR-002: marker → (sheet, cell) in the shipped file; each occurs exactly once.</summary>
    public static readonly IReadOnlyDictionary<string, (string Sheet, string Cell)> Markers =
        new Dictionary<string, (string Sheet, string Cell)>
        {
            ["{{course}}"] = (GradingSheet, "A2"),
            ["{{student.no}}"] = (GradingSheet, "A9"),
            ["{{student.name}}"] = (GradingSheet, "B9"),
            ["{{lesson.date}}"] = (GradingSheet, "C8"),
            ["{{grade}}"] = (GradingSheet, "C9"),
            ["{{teachers}}"] = (TopicsSheet, "D1"),
            ["{{topic.no}}"] = (TopicsSheet, "A4"),
            ["{{topic.date}}"] = (TopicsSheet, "B4"),
            ["{{topic.hours}}"] = (TopicsSheet, "C4"),
            ["{{topic.title}}"] = (TopicsSheet, "D4"),
        };

    public static string FormPath() =>
        Path.Combine(StaticFiles.RepositoryRoot(), "docs", "product", "report-templates", "group-lessons-blank.xlsx");

    public static string ReferencePath() =>
        Path.Combine(StaticFiles.RepositoryRoot(), "docs", "product", "report-templates", "school-journal-full.xlsx");

    /// <summary>Opens a versioned spreadsheet into memory; the file itself is never written (Hard Stop, spec FR-001).</summary>
    public static XLWorkbook Open(string path) => new(new MemoryStream(File.ReadAllBytes(path)));

    /// <summary>The export body with the form, per-copy members as JSON numbers (api-design §2.7).</summary>
    public static Dictionary<string, object?> Body(
        long courseId,
        int? studentsPerCopy = null,
        int? lessonsPerCopy = null,
        string? template = ReportTemplateTestData.BuiltInKey,
        string? layout = Layout,
        string? orientation = null)
    {
        var body = new Dictionary<string, object?>();
        foreach (var (key, value) in JournalExportHostExtensions.September(courseId, template, orientation: orientation))
        {
            body[key] = value;
        }

        if (layout is not null)
        {
            body["layout"] = layout;
        }

        if (studentsPerCopy is not null)
        {
            body["studentsPerCopy"] = studentsPerCopy;
        }

        if (lessonsPerCopy is not null)
        {
            body["lessonsPerCopy"] = lessonsPerCopy;
        }

        return body;
    }

    /// <summary>The September export body as raw JSON with extra raw members, for values a dictionary cannot express.</summary>
    public static string RawBody(long courseId, params (string Name, string RawJson)[] members)
    {
        var parts = new List<string>
        {
            "\"template\":\"" + ReportTemplateTestData.BuiltInKey + "\"",
            "\"courseId\":\"" + courseId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\"",
            "\"from\":\"" + JournalTestData.Period.FromText + "\"",
            "\"to\":\"" + JournalTestData.Period.ToText + "\"",
        };
        parts.AddRange(members.Select(m => "\"" + m.Name + "\":" + m.RawJson));
        return "{" + string.Join(',', parts) + "}";
    }

    /// <summary>One sheet of the file as spec FR-006 orders it.</summary>
    public sealed record PlannedSheet(bool IsGrading, string Name, int Group, int Block, int Number);

    /// <summary>
    /// Spec FR-006: groups = max(1, ⌈students / N⌉), blocks = max(1, ⌈lessons / M⌉); for each block the grading copy of
    /// every group, then the topics copy; names numbered per kind through the file, no number for a single copy (I-7).
    /// </summary>
    public static IReadOnlyList<PlannedSheet> Plan(int students, int lessons, int perCopyStudents, int perCopyLessons)
    {
        var groups = Math.Max(1, (students + perCopyStudents - 1) / perCopyStudents);
        var blocks = Math.Max(1, (lessons + perCopyLessons - 1) / perCopyLessons);
        var gradingCount = groups * blocks;
        var sheets = new List<PlannedSheet>();
        var gradingNumber = 0;
        for (var block = 0; block < blocks; block++)
        {
            for (var group = 0; group < groups; group++)
            {
                gradingNumber++;
                sheets.Add(new PlannedSheet(true, Name(GradingSheet, gradingNumber, gradingCount), group, block, gradingNumber));
            }

            sheets.Add(new PlannedSheet(false, Name(TopicsSheet, block + 1, blocks), 0, block, block + 1));
        }

        return sheets;
    }

    private static string Name(string kind, int number, int count) =>
        count == 1 ? kind : kind + " " + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
