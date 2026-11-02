using System.Net;
using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;
using ClassroomAgent.Web.Security;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureHostExtensions;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureTestData;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;
using Mr = ClassroomAgent.Tests.TestInfrastructure.MeetReportsHostExtensions;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-069 over HTTP: the role line of the landing page (FR-001), one title on the Meet codes and Erasure pages (FR-002),
/// the headings of the Database page (FR-003) and the prompt option of the Meet reports course picker (FR-004).
/// </summary>
public sealed partial class SmallTextsPageTests(PostgreSqlFixture database)
{
    private const string CodeWithCandidate = "abc-0001-xyz";
    private const string TeacherEmail = "mc.teacher1@school-one.example.test";
    private static readonly string[] Students = [Mc.Email("mc.s1"), Mc.Email("mc.s2"), Mc.Email("mc.s3")];

    public static TheoryData<Actor, string, string> RoleLines => new()
    {
        { Actor.Dean, "uk", "Роль: Декан" },
        { Actor.Admin, "uk", "Роль: Адміністратор школи" },
        { Actor.Dean, "en", "Role: Dean" },
        { Actor.Admin, "en", "Role: School administrator" },
    };

    public static TheoryData<string> TitledPages => new("meet-codes", "course-choice", "erasure", "erasure-confirmation");

    public static TheoryData<string, string, string, string> DatabaseHeadings => new()
    {
        { "uk", "Курси", "Дані", "Синхронізація" },
        { "en", "Courses", "Data", "Synchronization" },
    };

    public static TheoryData<Actor, string, string> PromptOptions => new()
    {
        { Actor.Dean, "uk", "— оберіть курс —" },
        { Actor.Admin, "en", "— choose a course —" },
    };

    private static Actor ActorFor(string language) => language == "uk" ? Actor.Dean : Actor.Admin;

    /// <summary>AC-001, FR-001: the landing page names the role in one text, in the user's language.</summary>
    [Theory]
    [MemberData(nameof(RoleLines))]
    public async Task TheLandingPage_ShowsTheRoleInOneText(Actor actor, string language, string expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        if (language == "en")
        {
            await client.ChooseLanguageAsync("/", "en", ct);
        }

        var page = await client.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(expected, Html.ElementText(page.Body, "signed-in-role"));
    }

    /// <summary>AC-002, FR-002: each of the four pages renders exactly one title, the page's own.</summary>
    [Theory]
    [MemberData(nameof(TitledPages))]
    public async Task EachPage_HasExactlyOneTitle_TheOwnOne(string name)
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = name is "erasure" or "erasure-confirmation" ? Actor.Admin : Actor.Dean;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        string path;
        string key;
        switch (name)
        {
            case "meet-codes":
                path = Mc.Path;
                key = MeetCodesTextKeys.Title;
                break;
            case "course-choice":
                await SeedMeetCodesAsync(host, ct);
                path = Mc.ChoicePath(CodeWithCandidate);
                key = MeetCodesTextKeys.ChoiceTitle;
                break;
            case "erasure":
                path = PagePath;
                key = ErasureTextKeys.Title;
                break;
            case "erasure-confirmation":
                var world = await host.SeedErasureWorldAsync(ct);
                path = ConfirmationPath(world.Target);
                key = ErasureTextKeys.ConfirmationTitle;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(name), name, null);
        }

        var page = await client.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var headings = H1().Matches(page.Body);
        var heading = Assert.Single(headings);
        var inner = Tags().Replace(heading.Groups["inner"].Value, string.Empty);
        Assert.Equal(host.Text(key, "uk"), WebUtility.HtmlDecode(inner).Trim());
    }

    /// <summary>AC-003, FR-003: the Database page names its three blocks, each heading wrapping its list.</summary>
    [Theory]
    [MemberData(nameof(DatabaseHeadings))]
    public async Task TheDatabasePage_NamesItsThreeBlocks(string language, string first, string second, string third)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, ActorFor(language), ct);
        await using var _host = host;
        if (language == "en")
        {
            await client.ChooseLanguageAsync("/", "en", ct);
        }

        var page = await client.GetAsync(DatabaseStatisticsTestData.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var body = WebUtility.HtmlDecode(page.Body);
        const RegexOptions options = RegexOptions.Singleline | RegexOptions.IgnoreCase;
        Assert.Matches(
            new Regex(
                @"<section\b[^>]*id=""database-counters""[^>]*>\s*<(?<w>[a-z0-9]+)\b[^>]*>\s*<h2\b[^>]*>\s*"
                + Regex.Escape(first) + @"\s*</h2>\s*<dl\b",
                options),
            body);
        Assert.Matches(
            new Regex(
                @"</dl>\s*</(?<w>[a-z0-9]+)>\s*<\k<w>\b[^>]*>\s*<h2\b[^>]*>\s*" + Regex.Escape(second) + @"\s*</h2>\s*<dl\b",
                options),
            body);
        Assert.Matches(
            new Regex(
                @"<section\b[^>]*id=""database-synchronization""[^>]*>\s*<h2\b[^>]*>\s*" + Regex.Escape(third)
                + @"\s*</h2>\s*<dl\b",
                options),
            body);
    }

    /// <summary>AC-003, FR-003: every counter stays in its block; the synchronization lines stay in theirs.</summary>
    [Fact]
    public async Task EveryCounter_StaysInItsBlock()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;

        var page = await client.GetAsync(DatabaseStatisticsTestData.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var body = WebUtility.HtmlDecode(page.Body);
        var sections = string.Concat(DatabaseSections().Matches(body).Select(m => m.Value));
        var lists = DefinitionLists().Matches(sections).Select(m => m.Value).ToList();
        Assert.Equal(3, lists.Count);
        Assert.Equal(
            ["courses-active", "courses-archived", "courses-provisioned", "courses-declined", "courses-suspended", "courses-total"],
            DataNames(lists[0]));
        Assert.Equal(
            ["teachers", "students", "course-work-items", "submissions", "meet-meetings", "meet-codes-awaiting-decision"],
            DataNames(lists[1]));
        Assert.Equal(
            ["last-successful-run", "state", "last-full-run", "meet-loaded-up-to"],
            DataNames(lists[2]));
    }

    /// <summary>AC-004, FR-004: the first option of the course picker is the prompt, with an empty value.</summary>
    [Theory]
    [MemberData(nameof(PromptOptions))]
    public async Task TheCoursePicker_StartsWithThePrompt(Actor actor, string language, string expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        await host.SeedWorldAsync(ct);
        if (language == "en")
        {
            await client.ChooseLanguageAsync("/", "en", ct);
        }

        var page = await client.GetAsync(MeetReportsTestData.Url(), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var select = CoursePicker(page.Body);
        var options = Options(select);
        var prompt = options[0];
        Assert.Equal(string.Empty, Styling.Attribute(prompt.Tag, "value"));
        Assert.Equal(expected, prompt.Text);
        Assert.Equal(host.Text("Journal.Form.ChooseCourse", language), prompt.Text);
        Assert.Single(options, option => Styling.Attribute(option.Tag, "value") == string.Empty);
        Assert.Contains(Mr.CourseName, WebUtility.HtmlDecode(select), StringComparison.Ordinal);
    }

    /// <summary>AC-004, FR-004: with a chosen course the prompt stays first and unselected; the course is selected.</summary>
    [Fact]
    public async Task WithAChosenCourse_ThePromptIsNotSelected_AndTheCourseIs()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await host.SeedWorldAsync(ct);

        var page = await client.GetAsync(MeetReportsTestData.Url(world.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var options = Options(CoursePicker(page.Body));
        Assert.Equal(string.Empty, Styling.Attribute(options[0].Tag, "value"));
        Assert.Equal(host.Text("Journal.Form.ChooseCourse", "uk"), options[0].Text);
        Assert.False(PasswordFieldMarkup.HasAttribute(options[0].Tag, "selected"), "The prompt option is selected.");
        var course = Assert.Single(
            options,
            option => Styling.Attribute(option.Tag, "value") == world.CourseId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(PasswordFieldMarkup.HasAttribute(course.Tag, "selected"), "The chosen course option is not selected.");
    }

    /// <summary>AC-004, FR-004: an empty course id answers as no course at all and shows no report (guard, green by design).</summary>
    [Fact]
    public async Task AnEmptyCourseId_ShowsNoReport_LikeNoCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.SeedWorldAsync(ct);
        var withoutCourse = MeetReportsTestData.Url();
        var withEmptyCourse = withoutCourse + (withoutCourse.Contains('?', StringComparison.Ordinal) ? "&" : "?") + "courseId=";

        var plain = await client.GetAsync(withoutCourse, ct);
        var empty = await client.GetAsync(withEmptyCourse, ct);

        Assert.Equal(plain.Status, empty.Status);
        Assert.DoesNotContain(host.Text(MeetReportsTestData.Keys.SummaryTitle, "uk"), empty.Text, StringComparison.Ordinal);
    }

    private static string CoursePicker(string body)
    {
        var match = CoursePickerBlock().Match(body);
        Assert.True(match.Success, "The page has no course picker with id meet-reports-course.");
        return match.Groups["body"].Value;
    }

    private static List<(string Tag, string Text)> Options(string select)
    {
        var options = OptionBlock().Matches(select)
            .Select(m => (m.Groups["tag"].Value, WebUtility.HtmlDecode(Tags().Replace(m.Groups["text"].Value, string.Empty)).Trim()))
            .ToList();
        Assert.NotEmpty(options);
        return options;
    }

    private static List<string> DataNames(string list) =>
        DataName().Matches(list).Select(m => m.Groups["n"].Value).ToList();

    private static async Task SeedMeetCodesAsync(InstallationTestHost host, CancellationToken ct)
    {
        await host.PrepareAsync(ct);
        await host.SeedRosterCourseAsync("Test Course Alpha", [TeacherEmail], Students[..2], ct);
        for (var day = 4; day <= 10; day++)
        {
            await host.SeedMeetingAsync(
                CodeWithCandidate, TeacherEmail, new DateTimeOffset(2026, 9, day, 9, 0, 0, TimeSpan.Zero), ct, Students);
        }
    }

    [GeneratedRegex(@"<h1\b[^>]*>(?<inner>.*?)</h1\s*>|<h1\b", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex H1();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(
        @"<section\b[^>]*id=""database-(?:counters|synchronization)""[^>]*>.*?</section>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DatabaseSections();

    [GeneratedRegex(@"<dl\b.*?</dl>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DefinitionLists();

    [GeneratedRegex(@"data-(?:counter|sync-line)=""(?<n>[^""]+)""")]
    private static partial Regex DataName();

    [GeneratedRegex(
        @"<select\b[^>]*\bid=""meet-reports-course""[^>]*>(?<body>.*?)</select\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CoursePickerBlock();

    [GeneratedRegex(
        @"(?<tag><option\b[^>]*>)(?<text>.*?)</option\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex OptionBlock();
}
