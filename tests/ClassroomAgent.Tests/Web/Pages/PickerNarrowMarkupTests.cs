using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Cl = ClassroomAgent.Tests.TestInfrastructure.CourseListHostExtensions;
using Ls = ClassroomAgent.Tests.TestInfrastructure.ListSearchTestData;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;
using Mr = ClassroomAgent.Tests.TestInfrastructure.MeetReportsHostExtensions;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-070 AC-009, api-design <c>x-picker-contract</c>: the markup hooks of the narrowing script on the four select pickers
/// and the course-choice radio list, present only where there is something to narrow, and the one external script tag
/// (no inline code).
/// </summary>
public sealed partial class PickerNarrowMarkupTests(PostgreSqlFixture database)
{
    private const string Candidate = "Candidate Course";

    private const string OtherName = "Other Course";

    private const string OtherLabel = "Other Course — S-1";

    private const string Code = "pck-0070-aaa";

    private static readonly string Teacher = Mc.Email("pck.teacher");

    private static readonly string[] Students = [Mc.Email("pck.s1"), Mc.Email("pck.s2"), Mc.Email("pck.s3")];

    private static readonly DateTimeOffset MeetingAt = new(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);

    public static TheoryData<string, string, string> SelectPickers
    {
        get
        {
            var data = new TheoryData<string, string, string>();
            foreach (var language in new[] { "uk", "en" })
            {
                data.Add("journal", "journal-course", language);
                data.Add("report", "report-course", language);
                data.Add("meet-reports", "meet-reports-course", language);
                data.Add("course-list", "course-list-teacher", language);
            }

            return data;
        }
    }

    private static string PathOf(string page, Mr.SeededWorld world) => page switch
    {
        "journal" => JournalTestData.Path,
        "report" => ReportTemplateTestData.SeptemberReportUrl(world.CourseId),
        "meet-reports" => MeetReportsTestData.Url(),
        "course-list" => CourseListTestData.Path,
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, null),
    };

    private static string NameOf(string page) =>
        page == "course-list" ? CourseListTestData.Parameters.TeacherId : JournalTestData.Parameters.CourseId;

    private static int ScriptTagCount(string html) => NarrowScriptTag().Count(html);

    private static bool HasInlineScriptAboutNarrowing(string html) =>
        InlineScript().Matches(html).Any(m => m.Groups["code"].Value.Contains("narrow", StringComparison.OrdinalIgnoreCase));

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex(@"<script\b[^>]*\bsrc=""[^""]*/js/picker-narrow\.js[^""]*""", RegexOptions.IgnoreCase)]
    private static partial Regex NarrowScriptTag();

    [GeneratedRegex(@"<script\b(?![^>]*\bsrc\s*=)[^>]*>(?<code>.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex InlineScript();

    [GeneratedRegex(@"<p\b[^>]*>.*?</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Paragraph();

    [GeneratedRegex(@"<input\b[^>]*\btype=""radio""[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex Radio();

    [GeneratedRegex(@"\d+\s*%")]
    private static partial Regex Percent();

    private static void AssertNoPicker(string html, string what)
    {
        Assert.DoesNotContain("data-narrow", html, StringComparison.Ordinal);
        Assert.Equal(0, ScriptTagCount(html));
        Assert.True(!HasInlineScriptAboutNarrowing(html), $"{what} carries inline narrowing code.");
    }

    /// <summary>AC-009: each select picker carries the hooks, in the user's language, and loads the script once.</summary>
    [Theory]
    [MemberData(nameof(SelectPickers))]
    public async Task ASelectPicker_CarriesTheHooks_AndLoadsTheScriptOnce(string pageName, string selectId, string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);
        var path = PathOf(pageName, world);
        if (language == UiLanguageTestData.English)
        {
            await client.ChooseLanguageAsync(path, UiLanguageTestData.English, ct);
        }

        var page = await client.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var tag = Ls.TagOf(page.Body, selectId);
        Assert.NotNull(tag);
        Assert.StartsWith("<select", tag, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("select", Ls.AttributeOf(tag, "data-narrow"));
        Assert.Equal(host.Text(Ls.Keys.PickerLabel, language), Ls.AttributeOf(tag, "data-narrow-label"));
        Assert.Equal(host.Text(Ls.Keys.PickerNone, language), Ls.AttributeOf(tag, "data-narrow-none"));
        Assert.Equal(NameOf(pageName), Ls.AttributeOf(tag, "name"));
        Assert.Equal(1, ScriptTagCount(page.Body));
        Assert.False(HasInlineScriptAboutNarrowing(page.Body), "An inline script mentions narrowing.");
    }

    /// <summary>AC-009: with no course stored there is no select to narrow, so no hook and no script.</summary>
    [Theory]
    [InlineData("journal", "journal-course")]
    [InlineData("report", "report-course")]
    [InlineData("meet-reports", "meet-reports-course")]
    public async Task WithNoCourseStored_ThePageHasNoHookAndNoScript(string pageName, string selectId)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.WaitForPurgeRunsAsync(1, ct);
        var path = pageName switch
        {
            "journal" => JournalTestData.Path,
            "report" => ReportTemplateTestData.ReportPath,
            _ => MeetReportsTestData.Url(),
        };

        var page = await client.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Null(Ls.TagOf(page.Body, selectId));
        AssertNoPicker(page.Body, pageName);
    }

    /// <summary>AC-009: a course list with a course but no teacher to choose has no hook on the teacher select and no script.</summary>
    [Fact]
    public async Task ACourseListWithNoTeacherToChoose_HasNoHookAndNoScript()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.WaitForPurgeRunsAsync(1, ct);
        await host.InsertCourseAsync(1, "Course Without Teacher", ct);

        var page = await client.GetAsync(CourseListTestData.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Single(Cl.RowIds(page.Body));
        Assert.NotNull(Ls.TagOf(page.Body, "course-list-teacher"));
        AssertNoPicker(page.Body, "the course list");
    }

    /// <summary>
    /// AC-009: the course-choice form is a radios picker; each radio's paragraph carries the row hooks and the plain course
    /// label (no share, no absence mark) while the visible row keeps showing them; the radios and the script are as contracted.
    /// </summary>
    [Fact]
    public async Task TheCourseChoice_CarriesTheRadiosHooks_WithThePlainLabelsAsRowText()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.PrepareAsync(ct);
        await host.SeedRosterCourseAsync(Candidate, [Teacher], Students, ct);
        var other = await host.SeedRosterCourseAsync(OtherName, [Mc.Email("pck.teacher2")], [], ct, section: "S-1");
        await host.MarkCourseAsync(other, "not_in_classroom", DataMarksWorld.D1, ct);
        await host.SeedMeetingAsync(Code, Teacher, MeetingAt, ct, Students);

        var page = await client.GetAsync(Ls.ChoiceUrl(Code, 0, null), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var form = Ls.TagOf(page.Body, Ls.ChoiceForm);
        Assert.NotNull(form);
        Assert.Equal("radios", Ls.AttributeOf(form, "data-narrow"));
        Assert.Equal(host.Text(Ls.Keys.PickerLabel, "uk"), Ls.AttributeOf(form, "data-narrow-label"));
        Assert.Equal(host.Text(Ls.Keys.PickerNone, "uk"), Ls.AttributeOf(form, "data-narrow-none"));
        var submit = Ls.TagOf(page.Body, Ls.ChoiceSubmit);
        Assert.NotNull(submit);
        Assert.StartsWith("<button", submit, StringComparison.OrdinalIgnoreCase);

        var rows = Paragraph().Matches(page.Body).Where(m => Radio().IsMatch(m.Value)).Select(m => m.Value).ToList();
        Assert.Equal(2, rows.Count);
        foreach (var row in rows)
        {
            var open = Regex.Match(row, @"^<p\b[^>]*>", RegexOptions.IgnoreCase).Value;
            Assert.Matches(@"\sdata-narrow-row(?=[\s=>/])", open);
            Assert.NotNull(Ls.AttributeOf(open, "data-narrow-text"));
            Assert.Equal(Mc.Fields.CourseId, Ls.AttributeOf(Radio().Match(row).Value, "name"));
        }

        var candidateRow = Assert.Single(rows, r => Mc.VisibleText(r).Contains(Candidate, StringComparison.Ordinal));
        var otherRow = Assert.Single(rows, r => Mc.VisibleText(r).Contains(OtherName, StringComparison.Ordinal));
        Assert.Equal(Candidate, Ls.AttributeOf(OpenOf(candidateRow), "data-narrow-text"));
        Assert.Equal(OtherLabel, Ls.AttributeOf(OpenOf(otherRow), "data-narrow-text"));
        Assert.Matches(Percent(), Mc.VisibleText(candidateRow));
        Assert.Contains(OtherLabel + " (", Mc.VisibleText(otherRow), StringComparison.Ordinal);
        Assert.Equal(1, ScriptTagCount(page.Body));
        Assert.False(HasInlineScriptAboutNarrowing(page.Body), "An inline script mentions narrowing.");
    }

    /// <summary>AC-009: a course-choice page with no radio to narrow — the code's own course is the only course — has no hook and no script.</summary>
    [Fact]
    public async Task ACourseChoiceWithNothingToChoose_HasNoHookAndNoScript()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.PrepareAsync(ct);
        var only = await host.SeedRosterCourseAsync("Only Course", [Teacher], [], ct);
        await host.SeedMeetingAsync(Code, Teacher, MeetingAt, ct);
        await host.SeedAutoLinkAsync(Code, only, MeetingAt, ct);

        var page = await client.GetAsync(Ls.ChoiceUrl(Code, 0, null), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.NotNull(Ls.TagOf(page.Body, Ls.ChoiceForm));
        AssertNoPicker(page.Body, "the course choice");
    }

    /// <summary>AC-009, x-picker-contract: the selected option stays selected and the option with an empty value is still there.</summary>
    [Fact]
    public async Task TheJournalSelect_KeepsTheSelectedOption_AndTheEmptyOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(JournalTestData.Url(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(Id(world.CourseId), Cl.SelectedValue(page.Body, JournalTestData.Parameters.CourseId));
        var select = Regex.Match(
            page.Body,
            @"<select\b[^>]*\bid=""journal-course""[^>]*>(?<body>.*?)</select>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        Assert.True(select.Success, "The journal has no course select.");
        Assert.Matches(@"<option\b[^>]*\bvalue=""""", select.Groups["body"].Value);
    }

    private static string OpenOf(string paragraph) => Regex.Match(paragraph, @"^<p\b[^>]*>", RegexOptions.IgnoreCase).Value;
}
