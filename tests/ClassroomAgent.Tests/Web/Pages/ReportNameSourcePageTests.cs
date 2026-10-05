using System.Net;
using System.Text.RegularExpressions;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-042 over HTTP: the name source of the report page (query and template), the switch, the refused value, the saved
/// and re-rendered template form, the read-only mode and the language switcher's return path (AC-002, 003, 005, 006,
/// 007, 010, 014; api-design §2–§3).
/// </summary>
public sealed partial class ReportNameSourcePageTests(PostgreSqlFixture database)
{
    private const string Surname = "Тестова";

    private const string GivenName = "Олена";

    private const string ProfileName = Surname + " " + GivenName;

    private static async Task GiveProfileNameAsync(InstallationTestHost host, SeededJournal seeded, CancellationToken ct) =>
        await host.ExecuteAsync(
            "UPDATE classroom_participant SET surname = @surname, given_name = @given WHERE id = @id",
            ct,
            ("surname", Surname),
            ("given", GivenName),
            ("id", seeded.StudentId));

    /// <summary>The input tags of the radio group <c>names</c>, as the form renders them.</summary>
    private static IReadOnlyList<string> NameRadios(string html) =>
        InputTag().Matches(html)
            .Select(m => m.Value)
            .Where(tag => NamesName().IsMatch(tag))
            .ToList();

    /// <summary>The radio of the <c>names</c> group carrying that value, or null.</summary>
    private static string? RadioFor(string html, string value) =>
        NameRadios(html).SingleOrDefault(tag => tag.Contains("value=\"" + value + "\"", StringComparison.Ordinal));

    /// <summary>US-042 AC-002, AC-003, AC-006: the page's source wins over the built-in's profile source, for this view only.</summary>
    [Fact]
    public async Task TheQuerySource_DecidesWhichNameIsShown()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);
        await GiveProfileNameAsync(host, seeded, ct);

        var byEmail = await client.GetAsync(
            ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId, names: "email"), ct);
        var byTemplate = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, byEmail.Status);
        Assert.Contains("student.one", byEmail.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(ProfileName, byEmail.Text, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, byTemplate.Status);
        Assert.Contains(ProfileName, byTemplate.Text, StringComparison.Ordinal);
    }

    /// <summary>US-042 AC-006, api-design §2.2: the switch is two links, each carrying the other source.</summary>
    [Fact]
    public async Task TheSwitch_OffersTheOtherSource()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var profile = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId), ct);
        var email = await client.GetAsync(
            ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId, names: "email"), ct);

        Assert.Equal(HttpStatusCode.OK, profile.Status);
        Assert.Contains("names=email", profile.Body, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, email.Status);
        Assert.Contains("names=profile", email.Body, StringComparison.Ordinal);
    }

    /// <summary>US-042 AC-010, VR-002: an unknown source is a 400 with the translated message and is never echoed.</summary>
    [Fact]
    public async Task AMalformedSource_Is400_WithTheMessage_AndIsNotEchoed()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(
            ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId, names: "zz-bad-zz"), ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.DoesNotContain("zz-bad-zz", page.Body, StringComparison.Ordinal);
        Assert.Contains(
            host.Text(ReportTemplateTestData.TextKeys.NameSourceMalformed, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>US-042 AC-005: the source chosen in the template form is stored and shown again by the change form.</summary>
    [Fact]
    public async Task ATemplate_IsSavedWithItsNameSource_AndTheChangeFormChecksIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await client.GetAsync(ReportTemplateTestData.NewPath, ct);
        var form = ReportTemplateFormBuilder.Valid("Test Template Email").Set(ReportTemplateTestData.NamesField, "email");

        var saved = await client.PostFormAsync(ReportTemplateTestData.ListPath, form.Http(), ct);

        Assert.Equal(HttpStatusCode.Redirect, saved.Status);
        var id = Assert.Single(await host.TemplateRowsAsync(ct)).Id;
        Assert.Equal("email", await host.NameSourceOfAsync(id, ct));
        var edit = await client.GetAsync(ReportTemplateTestData.EditPath(id.ToString(System.Globalization.CultureInfo.InvariantCulture)), ct);
        Assert.Equal(HttpStatusCode.OK, edit.Status);
        var email = RadioFor(edit.Body, "email");
        Assert.NotNull(email);
        Assert.Contains("checked", email, StringComparison.OrdinalIgnoreCase);
        var profile = RadioFor(edit.Body, "profile");
        Assert.NotNull(profile);
        Assert.DoesNotContain("checked", profile, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>US-042 AC-010, api-design §2.3: a bad source re-renders the form (400), stores nothing and is not echoed.</summary>
    [Fact]
    public async Task AnInvalidSource_ReRendersTheForm_AndStoresNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await client.GetAsync(ReportTemplateTestData.NewPath, ct);
        var form = ReportTemplateFormBuilder.Valid("Test Template Bad Source").Set(ReportTemplateTestData.NamesField, "bad");

        var page = await client.PostFormAsync(ReportTemplateTestData.ListPath, form.Http(), ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.True(Html.HasInput(page.Body, "name"));
        Assert.Equal("Test Template Bad Source", Html.InputValue(page.Body, "name"));
        Assert.DoesNotContain("value=\"bad\"", page.Body, StringComparison.Ordinal);
        Assert.Contains(
            host.Text(ReportTemplateTestData.TextKeys.Validation(ReportTemplateFieldErrorKey.NameSourceInvalid), "uk"),
            page.Text,
            StringComparison.Ordinal);
        Assert.Empty(await host.TemplateRowsAsync(ct));
    }

    /// <summary>US-042 AC-007: read-only mode shows the report with the page's source exactly as outside it.</summary>
    [Fact]
    public async Task InReadOnlyMode_TheSourceStillApplies()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(
            database, Actor.Admin, ct, ReadOnlyModeHost.Cause.GracePeriodExpired);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(
            ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId, names: "email"), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains("student.one", page.Text, StringComparison.Ordinal);
    }

    /// <summary>US-042 AC-014, api-design §2.4: the language switcher returns to an address that keeps the source.</summary>
    [Fact]
    public async Task TheLanguageSwitcher_KeepsTheSourceInItsReturnPath()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(
            ReportTemplateTestData.SeptemberReportUrl(seeded.CourseId, names: "email"), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var returnPath = UiLanguageTestData.RenderedReturnPath(page.Body);
        Assert.NotNull(returnPath);
        Assert.Contains("names=email", returnPath, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"<input\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex InputTag();

    [GeneratedRegex(@"\sname\s*=\s*""names""", RegexOptions.IgnoreCase)]
    private static partial Regex NamesName();
}
