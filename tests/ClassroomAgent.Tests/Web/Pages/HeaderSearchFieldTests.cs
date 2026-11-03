using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Ls = ClassroomAgent.Tests.TestInfrastructure.ListSearchTestData;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;
using Mr = ClassroomAgent.Tests.TestInfrastructure.MeetReportsHostExtensions;
using S = ClassroomAgent.Tests.TestInfrastructure.SearchTestData;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-071 AC-001, FR-001 over HTTP on real PostgreSQL: the header search field is on every installation page of a signed-in
/// Admin or Dean, in the header, once, empty, in both languages, as a plain GET form; and on no sign-in, error, forced-change or
/// Control Plane page (openapi <c>x-page-frame</c>).
/// </summary>
public sealed class HeaderSearchFieldTests(PostgreSqlFixture database)
{
    private static readonly string[] TeachingPageNames =
        ["course-list", "course-card", "teacher-list", "journal", "report", "meet-reports", "meeting", "meet-codes", "database"];

    public static TheoryData<string, Actor> TeachingPagesByRole
    {
        get
        {
            var data = new TheoryData<string, Actor>();
            foreach (var page in TeachingPageNames)
            {
                data.Add(page, Actor.Admin);
                data.Add(page, Actor.Dean);
            }

            return data;
        }
    }

    public static TheoryData<string> Languages => new("uk", "en");

    public static TheoryData<string, string> ListsWithOwnField => new()
    {
        { "courses", Ls.CourseField },
        { "teachers", Ls.TeacherField },
        { "meet-codes", Ls.MeetField },
    };

    public static TheoryData<string> AnonymousPages => new(SignInTestData.SignInPath, SignInTestData.ErrorPath(404));

    private static string PathOf(string page, Mr.SeededWorld world) => page switch
    {
        "course-list" => CourseListTestData.Path,
        "course-card" => CourseCardTestData.Path(world.CourseId),
        "teacher-list" => TeacherListTestData.Path,
        "journal" => JournalTestData.Path,
        "report" => ReportTemplateTestData.SeptemberReportUrl(world.CourseId),
        "meet-reports" => MeetReportsTestData.Url(),
        "meeting" => MeetReportsTestData.MeetingUrl(
            world.MeetingA.ToString(CultureInfo.InvariantCulture),
            world.CourseId,
            MeetReportsTestData.Period.FromText,
            MeetReportsTestData.Period.ToText),
        "meet-codes" => Mc.Path,
        "database" => DatabaseStatisticsTestData.Path,
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, null),
    };

    private static string UrlOfList(string list, string text) => list switch
    {
        "courses" => CourseListTestData.Url(q: text),
        "teachers" => Ls.TeachersUrl(q: text),
        "meet-codes" => Ls.MeetUrl(q: text),
        _ => throw new ArgumentOutOfRangeException(nameof(list), list, null),
    };

    /// <summary>The field is in the header, once, with no value (the page is not the results page).</summary>
    private static void AssertHeaderField(PageResponse page, string what)
    {
        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.True(S.HasHeaderField(page.Body), $"{what} has no header search field.");
        Assert.Contains($"id=\"{S.HeaderField}\"", S.HeaderMarkup(page.Body), StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(page.Body, $"\\bid=\"{Regex.Escape(S.HeaderField)}\""));
        Assert.Null(S.HeaderValue(page.Body));
    }

    private static void AssertNoHeaderField(PageResponse page, string what) =>
        Assert.False(S.HasHeaderField(page.Body), $"{what} carries the header search field.");

    private async Task<(InstallationTestHost Host, FormClient Client, Mr.SeededWorld World)> StartAsync(Actor actor, CancellationToken ct)
    {
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        var world = await host.SeedWorldAsync(ct);
        return (host, client, world);
    }

    /// <summary>AC-001, FR-001: every teaching-data page shows the field in the header, once and empty, to an Admin and a Dean.</summary>
    [Theory]
    [MemberData(nameof(TeachingPagesByRole))]
    public async Task EveryTeachingPage_HasTheHeaderField(string name, Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartAsync(actor, ct);
        await using var _host = host;

        var page = await client.GetAsync(PathOf(name, world), ct);

        AssertHeaderField(page, name);
    }

    /// <summary>AC-001, FR-001: the Admin-only pages show the field too.</summary>
    [Fact]
    public async Task EveryAdminOnlyPage_HasTheHeaderField()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        using var _admin = admin;

        var adminPages = new[]
        {
            SignInTestData.LandingPath,
            DeanAccountTestData.Paths.Deans,
            WorkspaceConnectionTestData.Path,
            ConnectionInstructionTestData.Path,
            AccessCheckTestData.Path,
            ReportTemplateTestData.ListPath,
            ReportTemplateTestData.NewPath,
        };
        foreach (var path in adminPages)
        {
            AssertHeaderField(await admin.GetAsync(path, ct), path);
        }
    }

    /// <summary>AC-001, FR-001: the Dean's own pages show the field.</summary>
    [Fact]
    public async Task TheDeansOwnPages_HaveTheHeaderField()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, dean) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        using var _dean = dean;

        foreach (var path in new[] { SignInTestData.LandingPath, DeanAccountTestData.Paths.OwnPassword })
        {
            AssertHeaderField(await dean.GetAsync(path, ct), path);
        }
    }

    /// <summary>AC-001, FR-001, NFR-073: the label and the submit button are the translated texts of the page's language.</summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task TheLabelAndTheButton_AreTheTranslatedTexts(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        if (language == UiLanguageTestData.English)
        {
            await client.ChooseLanguageAsync(SignInTestData.LandingPath, UiLanguageTestData.English, ct);
        }

        var page = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(host.Text(S.Keys.FieldLabel, language), Ls.LabelOf(page.Body, S.HeaderField));
        Assert.Contains(host.Text(S.Keys.FieldSubmit, language), S.TextOf(S.HeaderMarkup(page.Body)), StringComparison.Ordinal);
    }

    /// <summary>AC-001, FR-001: a plain GET form to the results page, no antiforgery token, a short non-autofilled search input.</summary>
    [Fact]
    public async Task TheFormAndTheInput_HaveTheSpecifiedAttributes()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;

        var page = await client.GetAsync(SignInTestData.LandingPath, ct);

        var form = Ls.TagOf(page.Body, S.HeaderForm);
        var input = Ls.TagOf(page.Body, S.HeaderField);
        Assert.NotNull(form);
        Assert.NotNull(input);
        Assert.Equal("get", Ls.AttributeOf(form, "method"), ignoreCase: true);
        Assert.Equal(S.Path, Ls.AttributeOf(form, "action"));
        Assert.Equal("search", Ls.AttributeOf(form, "role"));
        Assert.Equal("search", Ls.AttributeOf(input, "type"));
        Assert.Equal("q", Ls.AttributeOf(input, "name"));
        Assert.Equal("100", Ls.AttributeOf(input, "maxlength"));
        Assert.Equal("off", Ls.AttributeOf(input, "autocomplete"));
        Assert.Null(Ls.AttributeOf(input, "style"));
        var formMarkup = Ls.ElementOf(page.Body, S.HeaderForm);
        Assert.NotNull(formMarkup);
        Assert.DoesNotContain(Html.AntiforgeryFieldName, formMarkup, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-001, FR-001: the header field is empty on every other page, including a list that shows its own search text in its own field.
    /// </summary>
    [Theory]
    [MemberData(nameof(ListsWithOwnField))]
    public async Task OnAList_TheOwnFieldShowsTheText_AndTheHeaderFieldStaysEmpty(string list, string ownField)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;

        var page = await client.GetAsync(UrlOfList(list, "шукане"), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal("шукане", Ls.FieldValue(page.Body, ownField));
        Assert.True(S.HasHeaderField(page.Body), "The list has no header search field.");
        Assert.Null(S.HeaderValue(page.Body));
    }

    /// <summary>AC-001, FR-001: the forced password change of a Dean with a temporary password has no header field.</summary>
    [Fact]
    public async Task TheForcedPasswordChange_HasNoHeaderField()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.DeanWithTemporaryPassword, ct);
        await using var _host = host;

        var page = await client.GetAsync(DeanAccountTestData.Paths.ForcedChange, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        AssertNoHeaderField(page, "The forced password change");
    }

    /// <summary>AC-001, FR-001: an anonymous visitor's sign-in and error pages have no header field.</summary>
    [Theory]
    [MemberData(nameof(AnonymousPages))]
    public async Task AnAnonymousPage_HasNoHeaderField(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Anonymous, ct);
        await using var _host = host;

        var page = await client.GetAsync(path, ct);

        Assert.True(
            page.Status is HttpStatusCode.OK or HttpStatusCode.NotFound,
            $"{path} answered {(int)page.Status}.");
        AssertNoHeaderField(page, path);
    }

    /// <summary>AC-001, FR-001: the error pages have no header field even for a signed-in Dean; the landing page, the control, has it.</summary>
    [Fact]
    public async Task TheErrorPages_OfASignedInDean_HaveNoHeaderField()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;

        var notFound = await client.GetAsync(SignInTestData.ErrorPath(404), ct);
        var failure = await client.GetAsync(SignInTestData.ErrorPath(500), ct);
        var landing = await client.GetAsync(SignInTestData.LandingPath, ct);

        AssertNoHeaderField(notFound, "The 404 page");
        AssertNoHeaderField(failure, "The 500 page");
        AssertHeaderField(landing, "The landing page");
    }

    /// <summary>AC-001, FR-001: no Control Plane page has the field or an address of the installation's search.</summary>
    [Fact]
    public async Task NoControlPlanePage_HasTheHeaderField()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var controlPlane = await ControlPlaneTestHost.StartAsync(database, ct);
        var anonymous = controlPlane.CreateClient();
        using var _anonymous = anonymous;

        var setup = await anonymous.GetAsync("/setup", ct);
        var owner = await controlPlane.CreateOwnerAsync(ct);
        using var _owner = owner;
        var home = await owner.GetAsync("/", ct);
        var installations = await owner.GetAsync("/installations", ct);
        var visitor = controlPlane.CreateClient();
        using var _visitor = visitor;
        var signIn = await visitor.GetAsync("/sign-in", ct);

        foreach (var page in new[] { setup, home, installations, signIn })
        {
            Assert.Equal(HttpStatusCode.OK, page.Status);
            Assert.False(S.HasHeaderField(page.Body), "A Control Plane page carries the header search field.");
            Assert.DoesNotContain(S.Path, page.Body, StringComparison.Ordinal);
        }
    }

    /// <summary>AC-001, FR-001: submitting the form is a GET of the results page, whose header field then holds the text.</summary>
    [Fact]
    public async Task SubmittingTheForm_ShowsTheTextInTheHeaderFieldOfTheResultsPage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.SeedOneOfEachAsync(ct);

        var page = await client.GetAsync(S.Path + "?q=" + Ls.Needle, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(Ls.Needle, S.HeaderValue(page.Body));
    }
}
