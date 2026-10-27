namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The ids <see cref="DataMarksHostExtensions.SeedMarksCourseAsync"/> created, and the invented strings and instants it
/// used (TC-4). Instants fall at night in UTC so their Kyiv date is the next day (TC-8).
/// </summary>
public sealed record DataMarksWorld(long CourseId, string CourseGoogleId, string TemplateId)
{
    public const string CourseName = "Test Marks Course";

    /// <summary>Graded, maximum 12, dated 2026-09-05: the above-maximum item of AC-003 and the ordinary item of AC-005.</summary>
    public const string GradedTitle = "Test Marks Graded";

    /// <summary>Graded, maximum 10, dated 2026-09-06, marked "not in Classroom" (AC-005).</summary>
    public const string DeletedTitle = "Test Marks Deleted";

    public const int GradedMaximum = 12;

    // Surnames are unique tokens that locate a student's row whatever name form a page shows.

    /// <summary>On the roster; grade 15 of 12 on the graded item (AC-003).</summary>
    public const string Over = "Overmax";

    /// <summary>On the roster; draft 13 of 12 on the graded item, not returned (AC-003).</summary>
    public const string Draft = "Draftov";

    /// <summary>On the roster; grade 12 of 12 on the graded item, 7 of 10 on the deleted one (AC-003, AC-008).</summary>
    public const string Equal = "Equalson";

    /// <summary>Off the roster since <see cref="LeftLastSeen"/>; grade 8 of 10 on the deleted item (AC-008).</summary>
    public const string Gone = "Gonevych";

    /// <summary>Known only through a submission: off the roster, first and last seen equal (AC-008, OD-003 a).</summary>
    public const string Submitter = "Submitson";

    public const int OverGrade = 15;

    public const int DraftGrade = 13;

    /// <summary>The scale of the report template: every grade at or above the maximum shows <see cref="Full"/>.</summary>
    public static readonly IReadOnlyList<(int From, int To, string Label)> Scale =
        [(0, 49, "TSLOW"), (50, 99, "TSMID"), (100, 100, Full)];

    public const string Full = "TSFULL";

    /// <summary>D1: 2026-09-14 22:30 UTC — 15.09.2026 in Kyiv.</summary>
    public static readonly DateTimeOffset D1 = new(2026, 9, 14, 22, 30, 0, TimeSpan.Zero);

    /// <summary>D2: 2026-09-15 21:15 UTC — 16.09.2026 in Kyiv.</summary>
    public static readonly DateTimeOffset D2 = new(2026, 9, 15, 21, 15, 0, TimeSpan.Zero);

    /// <summary>R1: the last complete read of the "not visible" course, 2026-09-11 21:30 UTC — 12.09.2026 in Kyiv.</summary>
    public static readonly DateTimeOffset R1 = new(2026, 9, 11, 21, 30, 0, TimeSpan.Zero);

    /// <summary>R2: the last complete read of the skipped course, 2026-09-07 22:00 UTC — 08.09.2026 in Kyiv.</summary>
    public static readonly DateTimeOffset R2 = new(2026, 9, 7, 22, 0, 0, TimeSpan.Zero);

    /// <summary>L: the leaver's last sighting, 2026-09-09 21:45 UTC — 10.09.2026 in Kyiv.</summary>
    public static readonly DateTimeOffset LeftLastSeen = new(2026, 9, 9, 21, 45, 0, TimeSpan.Zero);

    /// <summary>The submitter's single sighting (first = last), inside the period.</summary>
    public static readonly DateTimeOffset SubmitterSeen = new(2026, 9, 5, 8, 0, 0, TimeSpan.Zero);

    /// <summary>When the deleted item was marked (US-058); the item mark shows no date (I-5).</summary>
    public static readonly DateTimeOffset DeletedSince = new(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset GradedDate = new(2026, 9, 5, 9, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset DeletedDate = new(2026, 9, 6, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The header line spec FR-003.1 gives the variant in a language, or null for none.</summary>
    public static string? HeaderLine(CourseHeaderVariant variant, DataMarksTexts texts) => variant switch
    {
        CourseHeaderVariant.NotInClassroom or CourseHeaderVariant.NotInClassroomAndSkipped =>
            texts.With(texts.HeaderNotInClassroomSince, D1),
        CourseHeaderVariant.NotVisible => texts.With(texts.HeaderNotUpdatedSince, R1),
        CourseHeaderVariant.Skipped => texts.With(texts.HeaderNotUpdatedSince, R2),
        CourseHeaderVariant.SkippedNeverRead => texts.HeaderNotUpdated,
        _ => null,
    };

    /// <summary>Every header text a variant must not show besides its own line (the stems, so no date escapes).</summary>
    public static IReadOnlyList<string> HeaderStems(DataMarksTexts texts) =>
        [Stem(texts.HeaderNotInClassroomSince), texts.HeaderNotUpdated];

    private static string Stem(string format) => format[..format.IndexOf("{0}", StringComparison.Ordinal)].TrimEnd();
}
