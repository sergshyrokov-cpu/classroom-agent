using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Ls = ClassroomAgent.Tests.TestInfrastructure.ListSearchTestData;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-071 AC-013, FR-013 items 1 to 3 over HTTP on real PostgreSQL: a searched text holding markup characters
/// (<c>&amp;</c>, <c>=</c>, <c>&lt;</c>, <c>&gt;</c>, both quotes) travels through the Meet code list's row forms, the course-choice
/// page and the refusal pages encoded, and only the validated text is echoed. These guard behaviour already built.
/// </summary>
public sealed class MarkupTextSurvivesActionsTests(PostgreSqlFixture database)
{
    private const string Code = "act-aaaa-bbb";

    /// <summary>Text with every character that is special in a query string, in markup and in an attribute.</summary>
    private const string Text = "a&b=<c>\"d'";

    private static readonly DateTimeOffset At = new(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A course named exactly <see cref="Text"/>, so the search matches the code linked to it.</summary>
    private static async Task<long> SeedAsync(InstallationTestHost host, CancellationToken ct)
    {
        await host.PrepareAsync(ct);
        var course = await host.SeedRosterCourseAsync(Text, [Mc.Email("mc.teacher1")], [], ct);
        await host.InsertAppUserAsync(ct, email: Mc.Email("code.maker"));
        await host.SeedMeetingAsync(Code, Mc.Email("mc.teacher1"), At, ct);
        return course;
    }

    public static TheoryData<string> RefusedTeacherQueries => new(
        "?status=nonsense&q=" + Uri.EscapeDataString("шевченко"),
        "?page=abc&q=" + Uri.EscapeDataString("шевченко"));

    /// <summary>AC-013, FR-013.1: on the Linked list the search field, the forms' hidden fields and the return path carry the text, encoded.</summary>
    [Fact]
    public async Task OnTheLinkedList_TheTextIsCarriedEncoded()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var course = await SeedAsync(host, ct);
        await host.SeedAutoLinkAsync(Code, course, At, ct);

        var page = await client.GetAsync(Ls.MeetUrl(Mc.Lists.Linked, Text), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(Text, Ls.FieldValue(page.Body, Ls.MeetField));
        var hidden = Ls.HiddenValues(page.Body, Ls.ReturnQ);
        Assert.NotEmpty(hidden);
        Assert.All(hidden, value => Assert.Equal(Text, value));
        Assert.DoesNotContain("<c>", page.Body, StringComparison.Ordinal);
        Assert.Equal(Ls.MeetUrl(Mc.Lists.Linked, Text, 0, 20), UiLanguageTestData.RenderedReturnPath(page.Body));
    }

    /// <summary>AC-013, FR-013.2: the confirm form of a link not yet confirmed carries the text in its hidden field.</summary>
    [Fact]
    public async Task TheConfirmForm_CarriesTheText()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var course = await SeedAsync(host, ct);
        await host.SeedAutoLinkAsync(Code, course, At, ct);

        var page = await client.GetAsync(Ls.MeetUrl(Mc.Lists.Linked, Text), ct);

        Assert.True(Ls.FormCount(page.Body, "/confirmation") >= 1, "The page has no confirm form.");
        var hidden = Ls.HiddenValues(page.Body, Ls.ReturnQ);
        Assert.NotEmpty(hidden);
        Assert.All(hidden, value => Assert.Equal(Text, value));
    }

    /// <summary>AC-013, FR-013.3: the course-choice page carries the text in its form and its return path, encoded.</summary>
    [Fact]
    public async Task TheCourseChoicePage_CarriesTheText()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var page = await client.GetAsync(Ls.ChoiceUrl(Code, 3, Text), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var hidden = Ls.HiddenValues(page.Body, Ls.ReturnQ);
        Assert.Contains(Text, hidden);
        Assert.All(hidden, value => Assert.Equal(Text, value));
        Assert.Equal(Ls.ChoiceUrl(Code, 3, Text), UiLanguageTestData.RenderedReturnPath(page.Body));
        Assert.DoesNotContain("<c>", page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-013, FR-013: an unknown code on the course-choice page answers 404 on the Unassigned list, keeping the search.</summary>
    [Fact]
    public async Task AnUnknownCode_OnTheCourseChoicePage_KeepsTheSearch()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var page = await client.GetAsync(Ls.ChoiceUrl("unk-nown-cod", 4, "act-aaaa"), ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        Assert.Contains(host.Text(Mc.Keys.MessageCodeNotFound, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Equal("act-aaaa", Ls.FieldValue(page.Body, Ls.MeetField));
        Assert.Equal(Ls.MeetUrl(Mc.Lists.Unassigned, "act-aaaa", 0, 20), UiLanguageTestData.RenderedReturnPath(page.Body));
    }

    /// <summary>AC-013, FR-013: the teacher list's 400 page keeps the validated search in its field and its return path.</summary>
    [Theory]
    [MemberData(nameof(RefusedTeacherQueries))]
    public async Task TheTeacherListRefusal_KeepsTheSearch(string query)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.SeedTeacherListAsync(ct);

        var page = await client.GetAsync(TeacherListTestData.Path + query, ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Equal("шевченко", Ls.FieldValue(page.Body, Ls.TeacherField));
        var returnPath = UiLanguageTestData.RenderedReturnPath(page.Body);
        Assert.NotNull(returnPath);
        Assert.Contains("q=" + Uri.EscapeDataString("шевченко"), returnPath, StringComparison.Ordinal);
    }
}
