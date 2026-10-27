using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.DataMarksHostExtensions;
using W = ClassroomAgent.Tests.TestInfrastructure.DataMarksWorld;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-060 on the report page over HTTP and PostgreSQL (TC-2): a full-view scale template, so a grade at or above the
/// maximum shows the 100 % label (v98); above-maximum grades (FR-004), the deleted item in its column and on «Теми
/// занять» (FR-005), the header line in <c>#report-header</c> (FR-003.2) and the "left" mark (FR-006), Ukrainian and
/// English (FR-009).
/// </summary>
public sealed class ReportMarksPageTests(PostgreSqlFixture database)
{
    private const string GradingId = "report-grading-table";

    private const string TopicsId = "report-topics-table";

    public static TheoryData<string> Languages => new("uk", "en");

    private async Task<(PageResponse Page, DataMarksTexts Texts)> ReportAsync(
        string language, CourseHeaderVariant variant = CourseHeaderVariant.None)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, language, ct);
        await using var _host = host;
        var world = await host.SeedMarksCourseAsync(ct, variant);

        var page = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(world.CourseId, world.TemplateId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(0, host.GoogleCallCount());
        return (page, DataMarksTexts.Of(language));
    }

    /// <summary>
    /// AC-003, AC-009, FR-004.2, FR-004.3: the grade 15 of 12 and the draft 13 of 12 show the 100 % label as today and
    /// carry the label and a highlight; the grade 12 of 12 shows the same label and is ordinary.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task TheReport_LabelsGradeAndDraftAboveMaximum_AndKeepsTheScaleValue(string language)
    {
        var (page, texts) = await ReportAsync(language);
        var table = Table(page.Body, GradingId);

        var over = CellOf(table, W.Over, W.GradedTitle);
        var draft = CellOf(table, W.Draft, W.GradedTitle);
        var equal = CellOf(table, W.Equal, W.GradedTitle);
        Assert.Contains(W.Full, Text(over.Body), StringComparison.Ordinal);
        Assert.Contains(texts.AboveMaximum, Text(over.Body), StringComparison.Ordinal);
        Assert.Contains(W.Full, Text(draft.Body), StringComparison.Ordinal);
        Assert.Contains(texts.AboveMaximum, Text(draft.Body), StringComparison.Ordinal);
        Assert.Contains(W.Full, Text(equal.Body), StringComparison.Ordinal);
        Assert.DoesNotContain(texts.AboveMaximum, Text(equal.Body), StringComparison.Ordinal);
        Assert.DoesNotContain(texts.AboveMaximum, Text(CellOf(table, W.Gone, W.DeletedTitle).Body), StringComparison.Ordinal);

        Assert.False(string.IsNullOrEmpty(ClassOf(over.Attributes)));
        Assert.Equal(ClassOf(over.Attributes), ClassOf(draft.Attributes));
        Assert.NotEqual(ClassOf(over.Attributes), ClassOf(equal.Attributes));
    }

    /// <summary>
    /// AC-005, AC-009, FR-005.2, FR-005.3: the deleted item's column is there with its grades and the mark in its heading,
    /// and on «Теми занять» the mark follows its topic; the ordinary item has no mark in either place.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task TheDeletedItem_IsMarkedInItsColumnAndOnTheLessonTopics(string language)
    {
        var (page, texts) = await ReportAsync(language);
        var grading = Table(page.Body, GradingId);
        var topics = Table(page.Body, TopicsId);

        Assert.Contains(texts.ItemNotInClassroom, Text(ColumnHeadingOf(grading, W.DeletedTitle)), StringComparison.Ordinal);
        Assert.DoesNotContain(texts.ItemNotInClassroom, Text(ColumnHeadingOf(grading, W.GradedTitle)), StringComparison.Ordinal);
        Assert.Contains("TSMID", Text(CellOf(grading, W.Gone, W.DeletedTitle).Body), StringComparison.Ordinal);

        var rows = BodyRows(topics).Select(Text).ToList();
        var deleted = Assert.Single(rows, r => r.Contains(W.DeletedTitle, StringComparison.Ordinal));
        var graded = Assert.Single(rows, r => r.Contains(W.GradedTitle, StringComparison.Ordinal));
        Assert.True(
            deleted.IndexOf(texts.ItemNotInClassroom, StringComparison.Ordinal)
            > deleted.IndexOf(W.DeletedTitle, StringComparison.Ordinal),
            $"The mark must follow the topic: {deleted}");
        Assert.DoesNotContain(texts.ItemNotInClassroom, graded, StringComparison.Ordinal);
    }

    /// <summary>AC-008, AC-009, FR-006, OD-003 a: "left L" for the leaver, "left" for the submitter, nothing on the roster.</summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task AStudentOffTheRoster_IsMarkedLeft_NextToTheName(string language)
    {
        var (page, texts) = await ReportAsync(language);
        var table = Table(page.Body, GradingId);

        var gone = RowHeading(RowOf(table, W.Gone));
        var submitter = RowHeading(RowOf(table, W.Submitter));
        Assert.Contains(texts.With(texts.LeftOn, W.LeftLastSeen), gone, StringComparison.Ordinal);
        Assert.True(HasUndatedLeft(submitter, texts), submitter);
        foreach (var onRoster in new[] { W.Over, W.Draft, W.Equal })
        {
            Assert.DoesNotContain(texts.Left, RowHeading(RowOf(table, onRoster)), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC-006, AC-007, AC-011, AC-012, FR-003.1, FR-003.2, VR-002: the header line of each stored state is in
    /// <c>#report-header</c>, once; no line where the variant has none; the grades are shown as before.
    /// </summary>
    [Theory]
    [InlineData(CourseHeaderVariant.None, "uk")]
    [InlineData(CourseHeaderVariant.NotInClassroom, "uk")]
    [InlineData(CourseHeaderVariant.NotInClassroom, "en")]
    [InlineData(CourseHeaderVariant.NotVisible, "uk")]
    [InlineData(CourseHeaderVariant.NotVisible, "en")]
    [InlineData(CourseHeaderVariant.Skipped, "uk")]
    [InlineData(CourseHeaderVariant.SkippedNeverRead, "uk")]
    [InlineData(CourseHeaderVariant.SkippedNeverRead, "en")]
    [InlineData(CourseHeaderVariant.NotInClassroomAndSkipped, "uk")]
    [InlineData(CourseHeaderVariant.ArchivedUnchanged, "uk")]
    public async Task TheCourseHeaderLine_IsInTheReportHeader(CourseHeaderVariant variant, string language)
    {
        var (page, texts) = await ReportAsync(language, variant);
        var header = Element(page.Body, "report-header");
        Assert.NotNull(header);
        var headerText = Text(header);
        var line = W.HeaderLine(variant, texts);

        if (line is null)
        {
            foreach (var stem in W.HeaderStems(texts))
            {
                Assert.DoesNotContain(stem, page.Text, StringComparison.Ordinal);
            }
        }
        else
        {
            Assert.Contains(line, headerText, StringComparison.Ordinal);
            var rest = page.Text.Replace(line, string.Empty, StringComparison.Ordinal);
            Assert.Equal(page.Text.Length - line.Length, rest.Length);
            foreach (var stem in W.HeaderStems(texts))
            {
                Assert.DoesNotContain(stem, rest, StringComparison.Ordinal);
            }
        }

        Assert.Contains(W.Full, Text(CellOf(Table(page.Body, GradingId), W.Over, W.GradedTitle).Body), StringComparison.Ordinal);
    }
}
