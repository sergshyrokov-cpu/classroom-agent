using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.NamesakeHostExtensions;
using N = ClassroomAgent.Tests.TestInfrastructure.NamesakeWorld;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-066 on the report page over HTTP and PostgreSQL (TC-2): a Dean opens the September report of the namesake course
/// with the built-in template (names from the profile, «Прізвище Ім'я»), or switches the page to names from the email
/// (US-042). The heading of every student row is read whole (spec FR-002, FR-003, FR-004.1).
/// </summary>
public sealed class NamesakeReportPageTests(PostgreSqlFixture database)
{
    private const string GradingId = "report-grading-table";

    private const string HeaderId = "report-header";

    public static TheoryData<string> Languages => new("uk", "en");

    private async Task<IReadOnlyList<string>> HeadingsAsync(
        string language, string? names, params NamesakeStudent[] students)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, language, ct);
        await using var _host = host;
        var courseId = await host.SeedNamesakeCourseAsync(ct, students);

        var page = await client.GetAsync(
            ReportTemplateTestData.SeptemberReportUrl(courseId, ReportTemplateTestData.BuiltInKey, names), ct);

        AssertServed(host, page.Status);
        return RowHeadings(page.Body, GradingId);
    }

    /// <summary>
    /// AC-002, FR-002.4, FR-004.1, FR-005, OD-001 a: both «Петренко Іван» rows show their own email after the name, in
    /// participant order; «Коваль Олена» shows the name only; the format is the same in both languages (I-5).
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task TwoStudentsWithTheSameProfileName_ShowTheirEmail_AndTheOtherRowDoesNot(string language)
    {
        var headings = await HeadingsAsync(language, null, N.PetrenkoA, N.PetrenkoB, N.Koval);

        Assert.Equal(
            ["Коваль Олена", N.WithEmail("Петренко Іван", N.PetrenkoA), N.WithEmail("Петренко Іван", N.PetrenkoB)],
            headings);
    }

    /// <summary>AC-006, FR-003.2: no two names coincide — every row shows the name alone.</summary>
    [Fact]
    public async Task NoNamesakes_NoRowShowsAnEmail()
    {
        var headings = await HeadingsAsync("uk", null, N.PetrenkoA, N.Koval);

        Assert.Equal(["Коваль Олена", "Петренко Іван"], headings);
    }

    /// <summary>AC-005, FR-002.3, I-1: «Петренко Іван» and «петренко іван» coincide; the order is not asserted.</summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task NamesDifferingOnlyInCase_AreNamesakes(string language)
    {
        var headings = await HeadingsAsync(language, null, N.PetrenkoA, N.PetrenkoLower, N.Koval);

        Assert.Equivalent(
            new[] { N.WithEmail("Петренко Іван", N.PetrenkoA), N.WithEmail("петренко іван", N.PetrenkoLower), "Коваль Олена" },
            headings,
            strict: true);
        Assert.Equal(3, headings.Count);
    }

    /// <summary>
    /// AC-007, FR-002.1, FR-002.2: switched to names from the email the two Petrenkos are «ivan.petrenko.a» and
    /// «ivan.petrenko.b» — no longer namesakes, so no row shows an additional email; switched back to the profile, the
    /// emails are shown again.
    /// </summary>
    [Fact]
    public async Task TheNameVariantDecides_WhoIsANamesake()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, "uk", ct);
        await using var _host = host;
        var courseId = await host.SeedNamesakeCourseAsync(ct, [N.PetrenkoA, N.PetrenkoB, N.Koval]);

        var email = await client.GetAsync(
            ReportTemplateTestData.SeptemberReportUrl(courseId, ReportTemplateTestData.BuiltInKey, "email"), ct);
        var profile = await client.GetAsync(
            ReportTemplateTestData.SeptemberReportUrl(courseId, ReportTemplateTestData.BuiltInKey, "profile"), ct);

        AssertServed(host, email.Status);
        AssertServed(host, profile.Status);
        Assert.Equal(["ivan.petrenko.a", "ivan.petrenko.b", "olena.koval"], RowHeadings(email.Body, GradingId));
        Assert.Equal(
            ["Коваль Олена", N.WithEmail("Петренко Іван", N.PetrenkoA), N.WithEmail("Петренко Іван", N.PetrenkoB)],
            RowHeadings(profile.Body, GradingId));
    }

    /// <summary>
    /// I-3, FR-002.2: names from the email — two addresses with the same part before <c>@</c> at different domains
    /// coincide and show the full address; from the profile («Петренко Іван», «Петрук Іван») they do not.
    /// </summary>
    [Fact]
    public async Task TheSamePartBeforeTheAt_AtTwoDomains_ShowsTheFullAddresses()
    {
        var email = await HeadingsAsync("uk", "email", N.PetrenkoA, N.PetrenkoOtherDomain, N.Koval);
        var profile = await HeadingsAsync("uk", "profile", N.PetrenkoA, N.PetrenkoOtherDomain, N.Koval);

        Assert.Equal(
            [N.WithEmail("ivan.petrenko.a", N.PetrenkoA), N.WithEmail("ivan.petrenko.a", N.PetrenkoOtherDomain), "olena.koval"],
            email);
        Assert.Equal(["Коваль Олена", "Петренко Іван", "Петрук Іван"], profile);
    }

    /// <summary>AC-008, FR-003.1, VR-001: the namesake with an email shows it; the one without shows the name only.</summary>
    [Fact]
    public async Task ANamesakeWithoutAnEmail_IsShownWithTheNameOnly()
    {
        var headings = await HeadingsAsync("uk", null, N.PetrenkoA, N.PetrenkoNoEmail, N.Koval);

        Assert.Equal(["Коваль Олена", N.WithEmail("Петренко Іван", N.PetrenkoA), "Петренко Іван"], headings);
    }

    /// <summary>AC-009, FR-004.3: the leaver among two namesakes shows «name (email)», then «вибув(-ла) &lt;дата&gt;».</summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task ANamesakeWhoLeft_ShowsTheEmail_AndTheLeftMark(string language)
    {
        var texts = DataMarksTexts.Of(language);

        var headings = await HeadingsAsync(language, null, N.PetrenkoA, N.PetrenkoB.Leaving(), N.Koval);

        Assert.Equal(
            [
                "Коваль Олена",
                N.WithEmail("Петренко Іван", N.PetrenkoA),
                N.WithEmail("Петренко Іван", N.PetrenkoB) + " " + texts.With(texts.LeftOn, N.LeftLastSeen),
            ],
            headings);
    }

    /// <summary>
    /// FR-001, OD-002 a: two teachers of the course with the same name — the report header names them without an
    /// email, and no teacher's address appears anywhere on the page.
    /// </summary>
    [Fact]
    public async Task TwoTeachersWithTheSameName_AreShownWithoutAnEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, "uk", ct);
        await using var _host = host;
        var courseId = await host.SeedNamesakeCourseAsync(ct, [N.PetrenkoA, N.Koval], TwoSameTeachers);

        var page = await client.GetAsync(ReportTemplateTestData.SeptemberReportUrl(courseId), ct);

        AssertServed(host, page.Status);
        var header = DataMarksHostExtensions.Element(page.Body, HeaderId);
        Assert.NotNull(header);
        Assert.Contains("Sameteacher Test", DataMarksHostExtensions.Text(header), StringComparison.Ordinal);
        foreach (var (_, _, email) in TwoSameTeachers)
        {
            Assert.DoesNotContain(email, page.Text, StringComparison.OrdinalIgnoreCase);
        }
    }
}
