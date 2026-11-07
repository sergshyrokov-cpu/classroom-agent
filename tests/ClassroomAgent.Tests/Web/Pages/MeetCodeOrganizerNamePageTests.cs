using System.Net;
using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Cl = ClassroomAgent.Tests.TestInfrastructure.CourseListHostExtensions;
using Ls = ClassroomAgent.Tests.TestInfrastructure.ListSearchTestData;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;
using S = ClassroomAgent.Tests.TestInfrastructure.SearchTestData;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-074 over HTTP, the Meet codes page and the header search: an organizer is shown with the participant's name beside the
/// email, as the Meet reports show a person, on every list; the name is encoded; a missing, empty or blank name leaves the email
/// alone; several organizers are ordered and merged by lower-cased email; an erased person shows nowhere; and the page search
/// and the header search find a code by its organizer's name (AC-001 … AC-006; spec FR-001 … FR-005).
/// </summary>
public sealed partial class MeetCodeOrganizerNamePageTests(PostgreSqlFixture database)
{
    private const string UnassignedCode = "abc-defg-hij";

    private const string LinkedCode = "lnk-mmmm-nnn";

    private const string MarkedCode = "mrk-pppp-qqq";

    private const string Named = "Петренко Іван";

    private static readonly DateTimeOffset At = new(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly string NamedEmail = Mc.Email("name.organizer");

    private static readonly string OtherEmail = Mc.Email("other.organizer");

    private static int personCounter;

    private sealed record World(long Algebra, long Maker);

    public static TheoryData<string> AllLists => new(Mc.Lists.Unassigned, Mc.Lists.Linked, Mc.Lists.NotACourse);

    private static string CodeOf(string list) => list switch
    {
        Mc.Lists.Unassigned => UnassignedCode,
        Mc.Lists.Linked => LinkedCode,
        _ => MarkedCode,
    };

    [GeneratedRegex(@"\s+,")]
    private static partial Regex SpaceBeforeComma();

    /// <summary>The text of the organizer cell with the separator spacing made uniform (no white space before a comma).</summary>
    private static string CellText(string body, string code) =>
        SpaceBeforeComma().Replace(Assert.Single(Ls.CellsOfCode(body, code).Take(1)), ",");

    /// <summary>The raw inner markup of the organizer cell (the first cell after the code); empty when the code is not listed.</summary>
    private static string RawCell(string body, string code)
    {
        var match = Regex.Match(
            body,
            @"<tr[^>]*>\s*<th scope=""row"">\s*" + Regex.Escape(code) + @"\s*</th>\s*<td[^>]*>(?<c>.*?)</td>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups["c"].Value : string.Empty;
    }

    private static Task<long> AddPersonAsync(InstallationTestHost host, string email, string? fullName, CancellationToken ct) =>
        CourseRows.InsertParticipantAsync(
            host,
            ct,
            googleUserId: "74" + Interlocked.Increment(ref personCounter).ToString("D10"),
            email: email,
            fullName: fullName);

    private static async Task<World> SeedWorldAsync(InstallationTestHost host, CancellationToken ct)
    {
        await host.PrepareAsync(ct);
        var algebra = await host.SeedRosterCourseAsync("Алгебра", [Mc.Email("algebra.teacher")], [], ct, section: "10-А");
        await host.InsertAppUserAsync(ct, email: Mc.Email("code.maker"));
        var maker = await host.AccountIdAsync(Mc.Email("code.maker"), ct);
        return new World(algebra, maker);
    }

    /// <summary>Puts a code on a list; every organizer gets one session of it.</summary>
    private static async Task PlaceAsync(
        InstallationTestHost host, World world, string list, string code, CancellationToken ct, params string[] organizers)
    {
        var minute = 0;
        foreach (var organizer in organizers)
        {
            await host.SeedMeetingAsync(code, organizer, At.AddMinutes(minute++), ct);
        }

        if (list == Mc.Lists.Linked)
        {
            await host.SeedPersonLinkAsync(code, world.Algebra, world.Maker, At, ct);
        }
        else if (list == Mc.Lists.NotACourse)
        {
            await host.SeedMarkAsync(code, world.Maker, At, ct);
        }
    }

    /// <summary>One code per list by the named organizer, and optionally one more per list by another named one.</summary>
    private static async Task<World> SeedAsync(
        InstallationTestHost host, CancellationToken ct, string name = Named, bool extras = false)
    {
        var world = await SeedWorldAsync(host, ct);
        await AddPersonAsync(host, NamedEmail, name, ct);
        await PlaceAsync(host, world, Mc.Lists.Unassigned, UnassignedCode, ct, NamedEmail);
        await PlaceAsync(host, world, Mc.Lists.Linked, LinkedCode, ct, NamedEmail);
        await PlaceAsync(host, world, Mc.Lists.NotACourse, MarkedCode, ct, NamedEmail);
        if (extras)
        {
            await AddPersonAsync(host, OtherEmail, "Шевчук Олена", ct);
            await PlaceAsync(host, world, Mc.Lists.Unassigned, "oth-unas-001", ct, OtherEmail);
            await PlaceAsync(host, world, Mc.Lists.Linked, "oth-link-002", ct, OtherEmail);
            await PlaceAsync(host, world, Mc.Lists.NotACourse, "oth-mrk-003", ct, OtherEmail);
        }

        return world;
    }

    private static async Task SeedManyUnassignedAsync(
        InstallationTestHost host, string prefix, int count, string organizer, CancellationToken ct)
    {
        for (var i = 1; i <= count; i++)
        {
            await host.SeedMeetingAsync($"{prefix}-{i:D3}-xxx", organizer, At.AddMinutes(i), ct);
        }
    }

    public static TheoryData<string, Actor, string> NameCases
    {
        get
        {
            var data = new TheoryData<string, Actor, string>();
            foreach (var list in new[] { Mc.Lists.Unassigned, Mc.Lists.Linked, Mc.Lists.NotACourse })
            {
                foreach (var actor in new[] { Actor.Admin, Actor.Dean })
                {
                    foreach (var language in new[] { "uk", "en" })
                    {
                        data.Add(list, actor, language);
                    }
                }
            }

            return data;
        }
    }

    /// <summary>AC-001, FR-001: on every list, for both roles and languages, the cell shows the name span, then the email span.</summary>
    [Theory]
    [MemberData(nameof(NameCases))]
    public async Task TheOrganizerCell_ShowsTheNameThenTheEmail_AsTheMeetReportsDo(string list, Actor actor, string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        await SeedAsync(host, ct);
        if (language == UiLanguageTestData.English)
        {
            await client.ChooseLanguageAsync(Mc.Path, UiLanguageTestData.English, ct);
        }

        var page = await client.GetAsync(Ls.MeetUrl(list), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var code = CodeOf(list);
        var raw = RawCell(page.Body, code);
        var nameAt = raw.IndexOf($"<span class=\"meet-person-name\">{Named}</span>", StringComparison.Ordinal);
        var emailAt = raw.IndexOf($"<span class=\"meet-person-email\">{NamedEmail}</span>", StringComparison.Ordinal);
        Assert.True(nameAt >= 0, "The name span is missing: " + raw);
        Assert.True(emailAt > nameAt, "The email span must follow the name span: " + raw);
        Assert.Equal(Named + " " + NamedEmail, Ls.CellsOfCode(page.Body, code)[0]);
    }

    /// <summary>AC-001, FR-001: a name with markup characters is HTML-encoded, never written as markup.</summary>
    [Fact]
    public async Task TheName_IsEncoded()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        const string Hostile = "Zz<b>&\"Ім'я";
        await SeedAsync(host, ct, Hostile);

        var page = await client.GetAsync(Ls.MeetUrl(Mc.Lists.Unassigned), ct);

        Assert.Contains("Zz&lt;b&gt;&amp;", page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Zz<b>", RawCell(page.Body, UnassignedCode), StringComparison.Ordinal);
        Assert.DoesNotContain("<b>&", RawCell(page.Body, UnassignedCode), StringComparison.Ordinal);
        Assert.Equal(Hostile + " " + NamedEmail, Ls.CellsOfCode(page.Body, UnassignedCode)[0]);
    }

    public static TheoryData<string, string> MissingNameCases
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var list in new[] { Mc.Lists.Unassigned, Mc.Lists.Linked, Mc.Lists.NotACourse })
            {
                foreach (var kind in new[] { "no-participant", "null-name", "blank-lowest-id" })
                {
                    data.Add(list, kind);
                }
            }

            return data;
        }
    }

    /// <summary>AC-002, FR-001: no participant, a null name, or a blank name at the lowest id: the email alone, no name span.</summary>
    [Theory]
    [MemberData(nameof(MissingNameCases))]
    public async Task WithNoUsableName_TheEmailStandsAlone(string list, string kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedWorldAsync(host, ct);
        var plain = Mc.Email("plain.organizer");
        switch (kind)
        {
            case "null-name":
                await AddPersonAsync(host, plain, null, ct);
                break;
            case "blank-lowest-id":
                await AddPersonAsync(host, plain, "  ", ct);
                await AddPersonAsync(host, plain, Named, ct);
                break;
        }

        await AddPersonAsync(host, NamedEmail, Named, ct);
        var code = CodeOf(list);
        const string Control = "ctl-aaaa-bbb";
        await PlaceAsync(host, world, list, code, ct, plain);
        await PlaceAsync(host, world, list, Control, ct, NamedEmail);

        var page = await client.GetAsync(Ls.MeetUrl(list), ct);

        Assert.Equal(plain, Ls.CellsOfCode(page.Body, code)[0]);
        Assert.DoesNotContain("meet-person-name", RawCell(page.Body, code), StringComparison.Ordinal);
        Assert.Equal(Named + " " + NamedEmail, Ls.CellsOfCode(page.Body, Control)[0]);
    }

    /// <summary>AC-003, FR-001: several organizers are ordered by email, merged by lower-cased email, each once, the upper-case spelling shown.</summary>
    [Theory]
    [MemberData(nameof(AllLists))]
    public async Task SeveralOrganizers_AreOrdered_AndEachShowsOnce(string list)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedWorldAsync(host, ct);
        var named = Mc.Email("b.named");
        var unnamed = Mc.Email("c.unnamed");
        var upper = Mc.Email("A.Dup");
        var lower = Mc.Email("a.dup");
        await AddPersonAsync(host, named, Named, ct);
        await AddPersonAsync(host, lower, "Іваненко Петро", ct);
        var code = CodeOf(list);
        await PlaceAsync(host, world, list, code, ct, named, unnamed, upper, lower);
        await host.SeedMeetingAsync(code, named, At.AddHours(5), ct);

        var page = await client.GetAsync(Ls.MeetUrl(list), ct);

        Assert.Equal(
            "Іваненко Петро " + upper + ", " + Named + " " + named + ", " + unnamed,
            CellText(page.Body, code));
        var cell = Ls.CellsOfCode(page.Body, code)[0];
        foreach (var email in new[] { named, unnamed, lower })
        {
            Assert.Equal(1, Regex.Count(cell, Regex.Escape(email), RegexOptions.IgnoreCase));
        }
    }

    /// <summary>AC-004, FR-005: an erased person's name is found nowhere and no longer shows; the survivor's name stays.</summary>
    [Fact]
    public async Task AnErasedOrganizer_IsNotFound_AndNotShown_TheSurvivorStays()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedWorldAsync(host, ct);
        var course = await host.InsertCourseAsync(1, "Курс", ct);
        var erasedEmail = Mc.Email("erased.person");
        var survivorEmail = Mc.Email("survivor.person");
        var person = await host.InsertPersonAsync(1, "Стертий", "Анна", erasedEmail, ct);
        await host.AddStudentAsync(course, person, ct);
        await AddPersonAsync(host, survivorEmail, "Шевчук Олена", ct);
        await PlaceAsync(host, world, Mc.Lists.Linked, LinkedCode, ct, erasedEmail, survivorEmail);

        var before = await client.GetAsync(Ls.MeetUrl(Mc.Lists.Linked, "стертий"), ct);
        Assert.Equal([LinkedCode], Ls.ListedCodes(before.Body));

        var admin = await host.InsertAdminAsync(ct);
        await host.EraseAsync(admin, person, ct);

        foreach (var text in new[] { "стертий", "erased.person" })
        {
            var after = await client.GetAsync(Ls.MeetUrl(Mc.Lists.Linked, text), ct);
            Assert.Empty(Ls.ListedCodes(after.Body));
            var nav = Ls.ListsNavText(after.Body);
            Assert.Contains(Ls.Counter(host, Mc.Lists.Unassigned, 0), nav, StringComparison.Ordinal);
            Assert.Contains(Ls.Counter(host, Mc.Lists.Linked, 0), nav, StringComparison.Ordinal);
            Assert.Contains(Ls.Counter(host, Mc.Lists.NotACourse, 0), nav, StringComparison.Ordinal);
        }

        var row = await client.GetAsync(Ls.MeetUrl(Mc.Lists.Linked, LinkedCode), ct);
        Assert.Equal("Шевчук Олена " + survivorEmail, Ls.CellsOfCode(row.Body, LinkedCode)[0]);
        Assert.DoesNotContain("Стертий", row.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(erasedEmail, row.Body, StringComparison.Ordinal);
    }

    public static TheoryData<string, string> SearchCases
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var list in new[] { Mc.Lists.Unassigned, Mc.Lists.Linked, Mc.Lists.NotACourse })
            {
                foreach (var text in new[] { "петренко", "іван петренко", "ПЕТРЕНКО", "етренк" })
                {
                    data.Add(list, text);
                }
            }

            return data;
        }
    }

    /// <summary>AC-005, FR-002: a part of a word, any order, any case finds the code; the counters count each list's matches.</summary>
    [Theory]
    [MemberData(nameof(SearchCases))]
    public async Task TheSearch_FindsACode_ByTheOrganizersName(string list, string text)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct, extras: true);

        var page = await client.GetAsync(Ls.MeetUrl(list, text), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal([CodeOf(list)], Ls.ListedCodes(page.Body));
        var nav = Ls.ListsNavText(page.Body);
        Assert.Contains(Ls.Counter(host, Mc.Lists.Unassigned, 1), nav, StringComparison.Ordinal);
        Assert.Contains(Ls.Counter(host, Mc.Lists.Linked, 1), nav, StringComparison.Ordinal);
        Assert.Contains(Ls.Counter(host, Mc.Lists.NotACourse, 1), nav, StringComparison.Ordinal);
    }

    /// <summary>AC-005, FR-002: the apostrophe variants are equal in the name and in the text.</summary>
    [Theory]
    [InlineData("мар'яна")]
    [InlineData("марʼяна")]
    [InlineData("мар’яна")]
    public async Task TheSearch_TreatsApostropheVariantsAsEqual(string text)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct, "Мар’яна Коваль", extras: true);

        var page = await client.GetAsync(Ls.MeetUrl(Mc.Lists.Unassigned, text), ct);

        Assert.Equal([UnassignedCode], Ls.ListedCodes(page.Body));
    }

    /// <summary>AC-005, FR-002: paging walks the matches by name and each link carries the search.</summary>
    [Fact]
    public async Task PagingLinks_CarryTheNameSearch_AndWalkTheMatches()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.PrepareAsync(ct);
        await AddPersonAsync(host, NamedEmail, Named, ct);
        await AddPersonAsync(host, OtherEmail, "Шевчук Олена", ct);
        await SeedManyUnassignedAsync(host, "pag", 25, NamedEmail, ct);
        await SeedManyUnassignedAsync(host, "oth", 3, OtherEmail, ct);

        var first = await client.GetAsync(Ls.MeetUrl(Mc.Lists.Unassigned, "петренко"), ct);

        Assert.Contains(Ls.Counter(host, Mc.Lists.Unassigned, 25), Ls.ListsNavText(first.Body), StringComparison.Ordinal);
        Assert.Equal(20, Ls.ListedCodes(first.Body).Count);
        Assert.Equal([Ls.MeetUrl(Mc.Lists.Unassigned, "петренко", 1, 20)], Cl.HrefsInside(first.Body, Ls.MeetPaging, "nav"));

        var second = await client.GetAsync(Ls.MeetUrl(Mc.Lists.Unassigned, "петренко", 1, 20), ct);
        Assert.Equal(5, Ls.ListedCodes(second.Body).Count);
        Assert.Equal([Ls.MeetUrl(Mc.Lists.Unassigned, "петренко", 0, 20)], Cl.HrefsInside(second.Body, Ls.MeetPaging, "nav"));
    }

    /// <summary>AC-005, FR-002: a row action keeps the name search (returnQ) and the code shows on its new list under it.</summary>
    [Fact]
    public async Task ARowAction_KeepsTheNameSearch()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var response = await client.PostWithTokenAsync(
            Mc.MarkPath(UnassignedCode),
            [
                Mc.Field(Mc.Fields.ExpectedState, Mc.States.Unassigned),
                Mc.Field(Mc.Fields.ReturnPage, 0),
                new KeyValuePair<string, string>(Ls.ReturnQ, "петренко"),
            ],
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(Ls.MeetUrl(Mc.Lists.Unassigned, "петренко", 0), response.LocationPath);
        var after = await client.GetAsync(response.LocationPath!, ct);
        Assert.Equal("петренко", Ls.FieldValue(after.Body, Ls.MeetField));
        Assert.Empty(Ls.ListedCodes(after.Body));
        var marked = await client.GetAsync(Ls.MeetUrl(Mc.Lists.NotACourse, "петренко"), ct);
        Assert.Contains(UnassignedCode, Ls.ListedCodes(marked.Body));
    }

    /// <summary>AC-005: the search by code, by email and by linked course keeps working.</summary>
    [Theory]
    [InlineData("abcdefg", Mc.Lists.Unassigned, UnassignedCode)]
    [InlineData("name.organizer", Mc.Lists.Unassigned, UnassignedCode)]
    [InlineData("алгебр", Mc.Lists.Linked, LinkedCode)]
    public async Task TheOtherSearchFields_StillFindTheirCodes(string text, string list, string expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var page = await client.GetAsync(Ls.MeetUrl(list, text), ct);

        Assert.Equal([expected], Ls.ListedCodes(page.Body));
    }

    /// <summary>AC-006, FR-003: the header search finds the three codes by the organizer's name and shows no name in the group.</summary>
    [Fact]
    public async Task TheHeaderSearch_FindsCodes_ByTheOrganizersName()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct, extras: true);

        var page = await client.GetAsync(S.Url("петренко"), ct);

        var group = S.RequireGroup(page.Body, S.MeetCodes);
        foreach (var list in new[] { Mc.Lists.Unassigned, Mc.Lists.Linked, Mc.Lists.NotACourse })
        {
            var row = S.TextFromLink(group, Ls.MeetUrl(list, "петренко"));
            Assert.Contains(CodeOf(list), row, StringComparison.Ordinal);
            Assert.Contains(host.Text(Ls.Keys.MeetListName(list), "uk"), row, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("oth-", S.TextOf(group), StringComparison.Ordinal);
        Assert.DoesNotContain(Named, S.TextOf(group), StringComparison.Ordinal);
    }

    /// <summary>AC-006, FR-003: 21 matches show 20 rows with the count line and the link to the whole list under the same search.</summary>
    [Fact]
    public async Task TheHeaderSearch_LimitsToTwenty_AndLinksToTheWholeList()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.PrepareAsync(ct);
        await AddPersonAsync(host, NamedEmail, Named, ct);
        await SeedManyUnassignedAsync(host, "lim", 21, NamedEmail, ct);

        var page = await client.GetAsync(S.Url("петренко"), ct);

        var group = S.RequireGroup(page.Body, S.MeetCodes);
        Assert.Equal(host.Text(S.Keys.LimitShown, "uk").Replace("{0}", "20").Replace("{1}", "21"), S.LimitLine(group));
        Assert.Equal(Ls.MeetUrl(Mc.Lists.Unassigned, "петренко"), S.WholeListHref(group));
    }
}
