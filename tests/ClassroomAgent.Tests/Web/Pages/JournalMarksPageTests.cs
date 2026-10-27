using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.DataMarksHostExtensions;
using W = ClassroomAgent.Tests.TestInfrastructure.DataMarksWorld;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-060 on the journal page over HTTP and PostgreSQL (TC-2): above-maximum grades (FR-004), the "not in Classroom"
/// item column (FR-005), the course header line above the table (FR-003, I-1) and the "left" mark next to a student's
/// name (FR-006), in Ukrainian and English (FR-009). A Dean opens the September journal of the marks course.
/// </summary>
public sealed class JournalMarksPageTests(PostgreSqlFixture database)
{
    private const string TableId = "journal-table";

    public static TheoryData<string> Languages => new("uk", "en");

    private async Task<(PageResponse Page, DataMarksTexts Texts)> JournalAsync(
        string language, string? view = null, CourseHeaderVariant variant = CourseHeaderVariant.None)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, language, ct);
        await using var _host = host;
        var world = await host.SeedMarksCourseAsync(ct, variant);

        var page = await client.GetAsync(JournalTestData.SeptemberUrl(world.CourseId, view), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(0, host.GoogleCallCount());
        return (page, DataMarksTexts.Of(language));
    }

    /// <summary>
    /// AC-003, AC-009, FR-004.1 … FR-004.5: in the full journal the grade 15 of 12 and the draft 13 of 12 keep their raw
    /// points, carry the label and a highlight the ordinary cell lacks; the grade 12 of 12 is ordinary (VR-001: equal is
    /// not above), and so are the cells of the 10-point item.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task TheFullJournal_LabelsAndHighlightsGradeAndDraftAboveMaximum(string language)
    {
        var (page, texts) = await JournalAsync(language, "full");
        var table = Table(page.Body, TableId);

        var over = CellOf(table, W.Over, W.GradedTitle);
        var draft = CellOf(table, W.Draft, W.GradedTitle);
        var equal = CellOf(table, W.Equal, W.GradedTitle);
        Assert.Contains("15 / 12", Text(over.Body), StringComparison.Ordinal);
        Assert.Contains(texts.AboveMaximum, Text(over.Body), StringComparison.Ordinal);
        Assert.Contains("13", Text(draft.Body), StringComparison.Ordinal);
        Assert.Contains(texts.AboveMaximum, Text(draft.Body), StringComparison.Ordinal);
        Assert.Contains("12 / 12", Text(equal.Body), StringComparison.Ordinal);
        Assert.DoesNotContain(texts.AboveMaximum, Text(equal.Body), StringComparison.Ordinal);
        Assert.DoesNotContain(texts.AboveMaximum, Text(CellOf(table, W.Equal, W.DeletedTitle).Body), StringComparison.Ordinal);
        Assert.DoesNotContain(texts.AboveMaximum, Text(CellOf(table, W.Gone, W.DeletedTitle).Body), StringComparison.Ordinal);

        // FR-004.2: the highlight is a class on the cell that the ordinary cell does not carry.
        Assert.False(string.IsNullOrEmpty(ClassOf(over.Attributes)));
        Assert.Equal(ClassOf(over.Attributes), ClassOf(draft.Attributes));
        Assert.NotEqual(ClassOf(over.Attributes), ClassOf(equal.Attributes));
    }

    /// <summary>AC-003, FR-004.2, BR-057: the short journal ignores drafts — the grade 15 is labelled, the draft 13 is not.</summary>
    [Fact]
    public async Task TheShortJournal_LabelsTheGradeAboveMaximum_ButNotTheDraft()
    {
        var (page, texts) = await JournalAsync("uk", "short");
        var table = Table(page.Body, TableId);

        var over = CellOf(table, W.Over, W.GradedTitle);
        Assert.Contains(texts.AboveMaximum, Text(over.Body), StringComparison.Ordinal);
        Assert.False(string.IsNullOrEmpty(ClassOf(over.Attributes)));
        Assert.DoesNotContain(texts.AboveMaximum, Text(CellOf(table, W.Draft, W.GradedTitle).Body), StringComparison.Ordinal);
        Assert.DoesNotContain(texts.AboveMaximum, Text(CellOf(table, W.Equal, W.GradedTitle).Body), StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-005, AC-009, FR-005.1, FR-005.2, I-5: the deleted item keeps its column and grades, its heading carries "not in
    /// Classroom" with no date; the ordinary item's heading carries no mark.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task TheDeletedItem_KeepsItsColumnAndGrades_WithTheMarkInItsHeading(string language)
    {
        var (page, texts) = await JournalAsync(language);
        var table = Table(page.Body, TableId);

        var deleted = Text(ColumnHeadingOf(table, W.DeletedTitle));
        Assert.Contains(texts.ItemNotInClassroom, deleted, StringComparison.Ordinal);
        Assert.DoesNotContain(texts.Date(W.DeletedSince), deleted, StringComparison.Ordinal);
        Assert.DoesNotContain(texts.ItemNotInClassroom, Text(ColumnHeadingOf(table, W.GradedTitle)), StringComparison.Ordinal);
        Assert.Contains("7 / 10", Text(CellOf(table, W.Equal, W.DeletedTitle).Body), StringComparison.Ordinal);
        Assert.Contains("8 / 10", Text(CellOf(table, W.Gone, W.DeletedTitle).Body), StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-008, AC-009, FR-006, VR-005, OD-003 a: the leaver shows "left 10.09.2026" (their last sighting in Kyiv), the
    /// submitter "left" with no date, the students on the roster nothing; the names stay as they are.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task AStudentOffTheRoster_IsMarkedLeft_NextToTheName(string language)
    {
        var (page, texts) = await JournalAsync(language);
        var table = Table(page.Body, TableId);

        var gone = RowHeading(RowOf(table, W.Gone));
        var submitter = RowHeading(RowOf(table, W.Submitter));
        Assert.Contains("Test " + W.Gone, gone, StringComparison.Ordinal);
        Assert.Contains(texts.With(texts.LeftOn, W.LeftLastSeen), gone, StringComparison.Ordinal);
        Assert.Contains("Test " + W.Submitter, submitter, StringComparison.Ordinal);
        Assert.True(HasUndatedLeft(submitter, texts), submitter);
        Assert.DoesNotContain(texts.Date(W.SubmitterSeen), submitter, StringComparison.Ordinal);
        foreach (var onRoster in new[] { W.Over, W.Draft, W.Equal })
        {
            Assert.DoesNotContain(texts.Left, RowHeading(RowOf(table, onRoster)), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC-006, AC-007, AC-011, AC-012, FR-003.1 … FR-003.4, VR-002 … VR-004, I-1, I-2: the header line of each stored
    /// state, once, above the journal table, in both views; no line where the variant has none; grades as before.
    /// </summary>
    [Theory]
    [InlineData(CourseHeaderVariant.None, "full")]
    [InlineData(CourseHeaderVariant.NotInClassroom, "full")]
    [InlineData(CourseHeaderVariant.NotInClassroom, "short")]
    [InlineData(CourseHeaderVariant.NotVisible, "full")]
    [InlineData(CourseHeaderVariant.Skipped, "full")]
    [InlineData(CourseHeaderVariant.Skipped, "short")]
    [InlineData(CourseHeaderVariant.SkippedNeverRead, "full")]
    [InlineData(CourseHeaderVariant.NotInClassroomAndSkipped, "full")]
    [InlineData(CourseHeaderVariant.ArchivedUnchanged, "full")]
    public async Task TheCourseHeaderLine_StandsAboveTheTable(CourseHeaderVariant variant, string view)
    {
        var (page, texts) = await JournalAsync("uk", view, variant);

        AssertHeaderLine(page.Text, variant, texts);
        Assert.Contains("15 / 12", Text(CellOf(Table(page.Body, TableId), W.Over, W.GradedTitle).Body), StringComparison.Ordinal);
    }

    /// <summary>AC-006, AC-007, AC-009: the header lines in English.</summary>
    [Theory]
    [InlineData(CourseHeaderVariant.NotInClassroom)]
    [InlineData(CourseHeaderVariant.NotVisible)]
    [InlineData(CourseHeaderVariant.SkippedNeverRead)]
    public async Task TheCourseHeaderLine_IsInEnglish_ForAnEnglishUser(CourseHeaderVariant variant)
    {
        var (page, texts) = await JournalAsync("en", variant: variant);

        AssertHeaderLine(page.Text, variant, texts);
        Assert.DoesNotContain(W.HeaderLine(variant, DataMarksTexts.Uk)!, page.Text, StringComparison.Ordinal);
    }

    /// <summary>The variant's line occurs once and before the table; the stems of every other line do not occur.</summary>
    private static void AssertHeaderLine(string text, CourseHeaderVariant variant, DataMarksTexts texts)
    {
        var line = W.HeaderLine(variant, texts);
        var table = text.IndexOf("id=\"" + TableId + "\"", StringComparison.Ordinal);
        Assert.True(table > 0);
        if (line is null)
        {
            foreach (var stem in W.HeaderStems(texts))
            {
                Assert.DoesNotContain(stem, text, StringComparison.Ordinal);
            }

            return;
        }

        var at = text.IndexOf(line, StringComparison.Ordinal);
        Assert.True(at >= 0, $"The journal does not show «{line}».");
        Assert.True(at < table, "The header line must stand above the journal table.");
        Assert.Equal(-1, text.IndexOf(line, at + line.Length, StringComparison.Ordinal));
        var rest = text.Remove(at, line.Length);
        foreach (var stem in W.HeaderStems(texts))
        {
            Assert.DoesNotContain(stem, rest, StringComparison.Ordinal);
        }
    }
}
