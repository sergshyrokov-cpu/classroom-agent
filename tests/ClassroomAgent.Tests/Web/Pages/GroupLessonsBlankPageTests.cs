using System.Net;
using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-043 over HTTP against PostgreSQL (TC-2; spec FR-004, FR-012, api-design §2.7, openapi <c>getReport</c>): the export
/// block of the report page offers the layout choice with the program layout preselected, the two number inputs with
/// their limits and defaults, translated labels, no fill-method choice, and remembers nothing.
/// </summary>
public sealed class GroupLessonsBlankPageTests(PostgreSqlFixture database)
{
    private static readonly Regex InputTag = new("<input[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static IReadOnlyList<string> Inputs(string body) => InputTag.Matches(body).Select(m => m.Value).ToList();

    private static string InputWithValue(string body, string value) =>
        Assert.Single(Inputs(body), t => t.Contains("value=\"" + value + "\"", StringComparison.Ordinal));

    private static bool IsChecked(string tag) => Regex.IsMatch(tag, @"\schecked(\s|=|/|>)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static IReadOnlyList<string> NumberInputs(string body) =>
        Inputs(body).Where(t => Regex.IsMatch(t, "type=\"number\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).ToList();

    private static void AssertDefaults(string body)
    {
        Assert.True(IsChecked(InputWithValue(body, "program")), "The program layout must be preselected.");
        Assert.False(IsChecked(InputWithValue(body, GroupLessonsBlank.Layout)), "The blank form must not be preselected.");
        var numbers = NumberInputs(body);
        Assert.Equal(2, numbers.Count);
        Assert.Contains(numbers, t => t.Contains("min=\"1\"", StringComparison.Ordinal) && t.Contains("max=\"30\"", StringComparison.Ordinal) && t.Contains("value=\"30\"", StringComparison.Ordinal));
        Assert.Contains(numbers, t => t.Contains("min=\"1\"", StringComparison.Ordinal) && t.Contains("max=\"23\"", StringComparison.Ordinal) && t.Contains("value=\"23\"", StringComparison.Ordinal));
    }

    /// <summary>FR-004: the export block offers both layouts, the program layout preselected.</summary>
    [Theory]
    [InlineData(Actor.Admin)]
    [InlineData(Actor.Dean)]
    public async Task TheExportBlock_OffersTheLayoutChoice_ProgramPreselected(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains("value=\"program\"", page.Body, StringComparison.Ordinal);
        Assert.Contains("value=\"group-lessons-blank\"", page.Body, StringComparison.Ordinal);
        var program = Regex.Match(page.Body, "<input[^>]*value=\"program\"[^>]*>");
        Assert.True(program.Success);
        Assert.Contains("checked", program.Value, StringComparison.Ordinal);
        var blank = Regex.Match(page.Body, "<input[^>]*value=\"group-lessons-blank\"[^>]*>");
        Assert.True(blank.Success);
        Assert.DoesNotContain("checked", blank.Value, StringComparison.Ordinal);
    }

    /// <summary>FR-004: the two number inputs carry the limits and the defaults; the orientation choice stays.</summary>
    [Fact]
    public async Task TheBlankInputs_CarryTheLimitsAndDefaults()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var numbers = Regex.Matches(page.Body, "<input[^>]*type=\"number\"[^>]*>").Select(m => m.Value).ToList();
        Assert.Equal(2, numbers.Count);
        Assert.Single(numbers, t => t.Contains("min=\"1\"", StringComparison.Ordinal) && t.Contains("max=\"30\"", StringComparison.Ordinal) && t.Contains("value=\"30\"", StringComparison.Ordinal));
        Assert.Single(numbers, t => t.Contains("min=\"1\"", StringComparison.Ordinal) && t.Contains("max=\"23\"", StringComparison.Ordinal) && t.Contains("value=\"23\"", StringComparison.Ordinal));
        Assert.Contains("value=\"portrait\"", page.Body, StringComparison.Ordinal);
        Assert.Contains("value=\"landscape\"", page.Body, StringComparison.Ordinal);
    }

    /// <summary>FR-012: the layout choice is in Ukrainian for a Ukrainian user.</summary>
    [Fact]
    public async Task TheLayoutChoice_IsTranslated_InUkrainian()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains("Бланк для групових занять", page.Text, StringComparison.Ordinal);
    }

    /// <summary>FR-012: the layout choice is in English for an English-speaking user; the fill method is never offered.</summary>
    [Fact]
    public async Task TheLayoutChoice_IsTranslated_InEnglish()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await InstallationTestHost.CreateAsync(database, ct);
        await using var _host = host;
        await ReadOnlyModeHost.SeedAsync(host, ReadOnlyModeHost.Cause.NotReadOnly, ct);
        await host.InsertAppUserAsync(ct, uiLanguage: "en");
        host.ControlPlaneHandler = WorkspaceConnectionHostExtensions.ApprovingChannel();
        host.Start();
        var (client, callback) = await host.SignInWithGoogleAsync(ct);
        Assert.Equal(HttpStatusCode.Redirect, callback.Status);
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains("Group lessons blank form", page.Text, StringComparison.Ordinal);
        Assert.Contains("Program layout", page.Text, StringComparison.Ordinal);
        Assert.Contains("Students per copy", page.Text, StringComparison.Ordinal);
        Assert.Contains("Lessons per copy", page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Basic", page.Text, StringComparison.Ordinal);
    }

    /// <summary>FR-004: the fill method is not shown to a Ukrainian user.</summary>
    [Fact]
    public async Task TheFillMethod_IsNotShown()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.DoesNotContain("Базовий", page.Text, StringComparison.Ordinal);
    }

    /// <summary>FR-004: nothing is remembered — after an export the page shows the defaults again.</summary>
    [Fact]
    public async Task NothingIsRemembered()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var export = await client.ExportAsync(GroupLessonsBlank.Body(seeded.CourseId, 10, 5), ct);
        Assert.Equal(HttpStatusCode.OK, export.Status);
        var page = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        AssertDefaults(page.Body);
    }
}
