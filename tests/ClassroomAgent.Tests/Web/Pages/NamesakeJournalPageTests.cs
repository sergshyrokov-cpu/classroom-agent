using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.NamesakeHostExtensions;
using N = ClassroomAgent.Tests.TestInfrastructure.NamesakeWorld;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-066 on the journal page over HTTP and PostgreSQL (TC-2): a Dean opens the September journal of the namesake
/// course; the row heading of every student is read whole, so a row shows exactly «name (email)» or the name alone
/// (spec FR-002, FR-003, FR-004.1). The journal labels a student by the Google full name, else the full email.
/// </summary>
public sealed class NamesakeJournalPageTests(PostgreSqlFixture database)
{
    private const string TableId = "journal-table";

    public static TheoryData<string> Languages => new("uk", "en");

    private async Task<IReadOnlyList<string>> HeadingsAsync(string language, params NamesakeStudent[] students)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, language, ct);
        await using var _host = host;
        var courseId = await host.SeedNamesakeCourseAsync(ct, students);

        var page = await client.GetAsync(JournalTestData.SeptemberUrl(courseId), ct);

        AssertServed(host, page.Status);
        return RowHeadings(page.Body, TableId);
    }

    /// <summary>
    /// AC-001, FR-002.4, FR-004.1, FR-005, OD-001 a: both «Іван Петренко» rows show their own email in parentheses after
    /// the name, in participant order; «Олена Коваль» shows the name only. The format is the same in both languages
    /// (FR-004.2, I-5).
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task TwoStudentsWithTheSameName_ShowTheirEmail_AndTheOtherRowDoesNot(string language)
    {
        var headings = await HeadingsAsync(language, N.PetrenkoA, N.PetrenkoB, N.Koval);

        Assert.Equal(
            [N.WithEmail("Іван Петренко", N.PetrenkoA), N.WithEmail("Іван Петренко", N.PetrenkoB), "Олена Коваль"],
            headings);
    }

    /// <summary>AC-006, FR-003.2: no two names coincide — every row shows the name alone, as before this Story.</summary>
    [Fact]
    public async Task NoNamesakes_NoRowShowsAnEmail()
    {
        var headings = await HeadingsAsync("uk", N.PetrenkoA, N.Koval);

        Assert.Equal(["Іван Петренко", "Олена Коваль"], headings);
    }

    /// <summary>AC-005, FR-002.3, I-1: names that differ only in letter case coincide; the order is not asserted.</summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task NamesDifferingOnlyInCase_AreNamesakes(string language)
    {
        var headings = await HeadingsAsync(language, N.PetrenkoA, N.PetrenkoLower, N.Koval);

        Assert.Equivalent(
            new[] { N.WithEmail("Іван Петренко", N.PetrenkoA), N.WithEmail("іван петренко", N.PetrenkoLower), "Олена Коваль" },
            headings,
            strict: true);
        Assert.Equal(3, headings.Count);
    }

    /// <summary>AC-008, FR-003.1, VR-001: of two namesakes the one with an email shows it, the one without shows the name.</summary>
    [Fact]
    public async Task ANamesakeWithoutAnEmail_IsShownWithTheNameOnly()
    {
        var headings = await HeadingsAsync("uk", N.PetrenkoA, N.PetrenkoNoEmail, N.Koval);

        Assert.Equal([N.WithEmail("Іван Петренко", N.PetrenkoA), "Іван Петренко", "Олена Коваль"], headings);
    }

    /// <summary>
    /// FR-002.5, I-2: two students with no name are labelled by their full email; the addresses differ only in case, so
    /// the labels coincide, yet neither row gets a second email.
    /// </summary>
    [Fact]
    public async Task RowsLabelledByTheirEmail_GetNoSecondEmail()
    {
        var headings = await HeadingsAsync("uk", N.MailUpper, N.MailLower, N.Koval);

        Assert.Equivalent(new[] { N.MailUpper.Email!, N.MailLower.Email!, "Олена Коваль" }, headings, strict: true);
        Assert.Equal(3, headings.Count);
    }

    /// <summary>
    /// AC-009, FR-004.3: a namesake who left shows «name (email)» and then the "left" mark with its date (US-060), in
    /// both languages.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task ANamesakeWhoLeft_ShowsTheEmail_AndTheLeftMark(string language)
    {
        var texts = DataMarksTexts.Of(language);

        var headings = await HeadingsAsync(language, N.PetrenkoA, N.PetrenkoB.Leaving(), N.Koval);

        Assert.Equal(
            [
                N.WithEmail("Іван Петренко", N.PetrenkoA),
                N.WithEmail("Іван Петренко", N.PetrenkoB) + " " + texts.With(texts.LeftOn, N.LeftLastSeen),
                "Олена Коваль",
            ],
            headings);
    }
}
