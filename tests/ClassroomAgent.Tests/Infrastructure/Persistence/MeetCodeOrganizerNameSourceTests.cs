using System.Data.Common;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-074 over real PostgreSQL with the migrations applied (TC-2), at the read source: who an organizer is and the chosen name
/// (spec FR-001, db-design §2, entity model §4), the same name as the Meet reports (AC-009), search by the organizer's name on
/// the three lists, their counters and the header's first matches (FR-003, FR-004, AC-004, AC-005, AC-006), and the bounded
/// number of commands (FR-005, AC-008). Rows are inserted by SQL; every value is invented (TC-4).
/// </summary>
public sealed class MeetCodeOrganizerNameSourceTests(PostgreSqlFixture database)
{
    private static readonly DateTimeOffset At = new(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly TimeZoneInfo Zone = JournalTestData.Kyiv;

    private const string UnassignedCode = "abc-defg-hij";

    private const string LinkedCode = "lnk-mmmm-nnn";

    private const string MarkedCode = "mrk-pppp-qqq";

    private const string Unassigned = "unassigned";

    private const string Linked = "linked";

    private const string Marked = "marked";

    private const string Petrenko = "Петренко Іван";

    private const string Ivanenko = "Іваненко Петро";

    private static readonly string Organizer = Mc.Email("name.organizer");

    private static int _ordinal;

    private sealed record World(long Algebra, long Maker);

    private async Task<InstallationTestHost> StartEmptyAsync(CancellationToken ct)
    {
        var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct);
        return host;
    }

    /// <summary>A participant with exactly this email and full name (no name parts unless given); ids grow in call order.</summary>
    private static Task<long> PersonAsync(
        InstallationTestHost host, string email, string? fullName, CancellationToken ct, string? surname = null, string? givenName = null) =>
        CourseRows.InsertParticipantAsync(
            host,
            ct,
            googleUserId: "74" + Interlocked.Increment(ref _ordinal).ToString("D10", System.Globalization.CultureInfo.InvariantCulture),
            email: email,
            fullName: fullName,
            surname: surname,
            givenName: givenName);

    /// <summary>The course "Алгебра" (section "10-А") and an account that links and marks codes.</summary>
    private static async Task<World> WorldAsync(InstallationTestHost host, CancellationToken ct)
    {
        var algebra = await host.SeedRosterCourseAsync("Алгебра", [Mc.Email("algebra.teacher")], [], ct, section: "10-А");
        await host.InsertAppUserAsync(ct, email: Mc.Email("code.maker"));
        var maker = await host.AccountIdAsync(Mc.Email("code.maker"), ct);
        return new World(algebra, maker);
    }

    /// <summary>A meeting of <paramref name="code"/> by <paramref name="organizer"/>, the code put on <paramref name="list"/>.</summary>
    private static async Task SeedCodeAsync(
        InstallationTestHost host, World world, string code, string list, string organizer, CancellationToken ct, int day = 0)
    {
        await host.SeedMeetingAsync(code, organizer, At.AddDays(day), ct);
        if (list == Linked)
        {
            await host.SeedPersonLinkAsync(code, world.Algebra, world.Maker, At, ct);
        }
        else if (list == Marked)
        {
            await host.SeedMarkAsync(code, world.Maker, At, ct);
        }
    }

    private static string CodeOf(string list) => list switch
    {
        Unassigned => UnassignedCode,
        Linked => LinkedCode,
        Marked => MarkedCode,
        _ => throw new ArgumentOutOfRangeException(nameof(list), list, null),
    };

    /// <summary>One code on each list, all three organized by <paramref name="organizer"/>.</summary>
    private static async Task<World> SeedThreeAsync(InstallationTestHost host, string organizer, CancellationToken ct)
    {
        var world = await WorldAsync(host, ct);
        foreach (var list in new[] { Unassigned, Linked, Marked })
        {
            await SeedCodeAsync(host, world, CodeOf(list), list, organizer, ct);
        }

        return world;
    }

    /// <summary>The organizers of a code as its list's page read gives them.</summary>
    private static async Task<IReadOnlyList<MeetOrganizerRecord>> OrganizersAsync(
        InstallationTestHost host, string list, string code, CancellationToken ct)
    {
        IReadOnlyList<MeetOrganizerRecord> found = [];
        await host.WithMeetCodesReadSourceAsync(async source =>
        {
            found = list switch
            {
                Unassigned => (await source.GetUnassignedPageAsync(0, 50, Zone, null, ct)).Rows.Single(r => r.MeetingCode == code).Organizers,
                Linked => (await source.GetLinkedPageAsync(0, 50, null, ct)).Rows.Single(r => r.MeetingCode == code).Organizers,
                Marked => (await source.GetMarkedPageAsync(0, 50, null, ct)).Rows.Single(r => r.MeetingCode == code).Organizers,
                _ => throw new ArgumentOutOfRangeException(nameof(list), list, null),
            };
        });
        return found;
    }

    private static async Task<(IReadOnlyList<string> Unassigned, IReadOnlyList<string> Linked, IReadOnlyList<string> Marked, MeetCodeListCounts Counts)>
        CodesFoundAsync(InstallationTestHost host, string search, CancellationToken ct)
    {
        IReadOnlyList<string> unassigned = [];
        IReadOnlyList<string> linked = [];
        IReadOnlyList<string> marked = [];
        MeetCodeListCounts counts = new(0, 0, 0);
        await host.WithMeetCodesReadSourceAsync(async source =>
        {
            counts = await source.GetCountsAsync(search, ct);
            unassigned = (await source.GetUnassignedPageAsync(0, 50, Zone, search, ct)).Rows.Select(r => r.MeetingCode).ToList();
            linked = (await source.GetLinkedPageAsync(0, 50, search, ct)).Rows.Select(r => r.MeetingCode).ToList();
            marked = (await source.GetMarkedPageAsync(0, 50, search, ct)).Rows.Select(r => r.MeetingCode).ToList();
        });
        Assert.Equal(counts.Unassigned, unassigned.Count);
        Assert.Equal(counts.Linked, linked.Count);
        Assert.Equal(counts.NotACourse, marked.Count);
        return (unassigned, linked, marked, counts);
    }

    /// <summary>
    /// Entity model §4, the six cases of the name rule: the participants of one email in id order (a "-" is no participant) and
    /// the chosen stored name, before the blank test.
    /// </summary>
    public static TheoryData<string, string> NameRuleCases
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var rule in new[] { "none", "null", "named", "null-then-named", "blank-then-named", "two-named" })
            {
                foreach (var list in new[] { Unassigned, Linked, Marked })
                {
                    data.Add(rule, list);
                }
            }

            return data;
        }
    }

    private static async Task<string?> SeedRuleAsync(InstallationTestHost host, string rule, string email, CancellationToken ct)
    {
        switch (rule)
        {
            case "none":
                return null;
            case "null":
                await PersonAsync(host, email, null, ct);
                return null;
            case "named":
                await PersonAsync(host, email, Petrenko, ct);
                return Petrenko;
            case "null-then-named":
                await PersonAsync(host, email, null, ct);
                await PersonAsync(host, email, Petrenko, ct);
                return Petrenko;
            case "blank-then-named":
                await PersonAsync(host, email, "  ", ct);
                await PersonAsync(host, email, Petrenko, ct);
                return "  ";
            case "two-named":
                await PersonAsync(host, email, Petrenko, ct);
                await PersonAsync(host, email, Ivanenko, ct);
                return Petrenko;
            default:
                throw new ArgumentOutOfRangeException(nameof(rule), rule, null);
        }
    }

    /// <summary>
    /// AC-001, AC-002, AC-009, FR-001, db-design §2: on each list the organizer carries the chosen stored name (lowest id among
    /// those with a non-null name; a blank one is chosen and only then counts as no name), and it is the very name the Meet
    /// reports read for the same email. A participant who is not an organizer lends nothing (the control row).
    /// </summary>
    [Theory]
    [MemberData(nameof(NameRuleCases))]
    public async Task TheOrganizer_CarriesTheChosenName_AsTheMeetReportsChooseIt(string rule, string list)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var world = await WorldAsync(host, ct);
        await PersonAsync(host, Mc.Email("not.an.organizer"), "Сторонній Олег", ct);
        var expected = await SeedRuleAsync(host, rule, Organizer, ct);
        await SeedCodeAsync(host, world, CodeOf(list), list, Organizer, ct);

        var organizers = await OrganizersAsync(host, list, CodeOf(list), ct);

        Assert.Equal([new MeetOrganizerRecord(Organizer, expected)], organizers);

        IReadOnlyList<MeetReportPersonRecord> people = [];
        await host.WithMeetReportSourceAsync(async source => people = await source.GetPeopleAsync([Organizer.ToLowerInvariant()], ct));
        Assert.Equal(expected, people.SingleOrDefault()?.FullName);
    }

    /// <summary>FR-001: a Meet email and a participant email are the same when equal after lower-casing.</summary>
    [Fact]
    public async Task AnOrganizerEmailInAnotherCase_StillFindsTheParticipantsName()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var world = await WorldAsync(host, ct);
        var stored = Mc.Email("mixed.case");
        var spelled = "Mixed.Case@" + stored.Split('@')[1];
        await PersonAsync(host, stored, Petrenko, ct);
        await SeedCodeAsync(host, world, LinkedCode, Linked, spelled, ct);

        Assert.Equal([new MeetOrganizerRecord(spelled, Petrenko)], await OrganizersAsync(host, Linked, LinkedCode, ct));
        Assert.Equal([LinkedCode], (await CodesFoundAsync(host, "петренко", ct)).Linked);
    }

    /// <summary>
    /// AC-003, FR-001 (I-1), db-design §4: several organizers, each once, in ordinal order of the shown email; two spellings of one
    /// email that differ only in case are one organizer, shown with the ordinally first spelling; each by its own name rule.
    /// </summary>
    [Theory]
    [InlineData(Unassigned)]
    [InlineData(Linked)]
    [InlineData(Marked)]
    public async Task SeveralOrganizers_EachOnce_InOrdinalOrder_CaseOnlyDuplicatesMerged(string list)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var world = await WorldAsync(host, ct);
        var domain = "@" + Organizer.Split('@')[1];
        var named = "b.named" + domain;
        var unnamed = "c.unnamed" + domain;
        var upper = "A.Dup" + domain;
        var lower = "a.dup" + domain;
        await PersonAsync(host, named, Petrenko, ct);
        await PersonAsync(host, lower, Ivanenko, ct);
        var code = CodeOf(list);
        await SeedCodeAsync(host, world, code, list, unnamed, ct);
        await host.SeedMeetingAsync(code, named, At.AddDays(1), ct);
        await host.SeedMeetingAsync(code, named, At.AddDays(2), ct);
        await host.SeedMeetingAsync(code, lower, At.AddDays(3), ct);
        await host.SeedMeetingAsync(code, upper, At.AddDays(4), ct);

        var organizers = await OrganizersAsync(host, list, code, ct);

        Assert.Equal(
            [new MeetOrganizerRecord(upper, Ivanenko), new MeetOrganizerRecord(named, Petrenko), new MeetOrganizerRecord(unnamed, null)],
            organizers);
    }

    /// <summary>
    /// AC-005, FR-003 (BR-087): a part of the name, words in any order, another case, repeated white space find the code on each
    /// list, with its counter; another organizer's code is not found.
    /// </summary>
    [Theory]
    [InlineData("петренко")]
    [InlineData("іван петренко")]
    [InlineData("ПЕТРЕНКО   ІВАН")]
    [InlineData("етренк")]
    public async Task ACode_IsFound_ByItsOrganizersName_OnEveryList(string text)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        await PersonAsync(host, Organizer, Petrenko, ct);
        await PersonAsync(host, Mc.Email("other.organizer"), "Шевчук Олена", ct);
        var world = await SeedThreeAsync(host, Organizer, ct);
        await SeedCodeAsync(host, world, "oth-unas-001", Unassigned, Mc.Email("other.organizer"), ct);
        await SeedCodeAsync(host, world, "oth-link-002", Linked, Mc.Email("other.organizer"), ct);
        await SeedCodeAsync(host, world, "oth-mark-003", Marked, Mc.Email("other.organizer"), ct);

        var found = await CodesFoundAsync(host, text, ct);

        Assert.Equal([UnassignedCode], found.Unassigned);
        Assert.Equal([LinkedCode], found.Linked);
        Assert.Equal([MarkedCode], found.Marked);
        Assert.Equal(new MeetCodeListCounts(1, 1, 1), found.Counts);
    }

    /// <summary>AC-005, FR-003 (BR-087 rule 5): the three apostrophe variants are equal in a name.</summary>
    [Theory]
    [InlineData("мар'яна")]
    [InlineData("мар’яна")]
    [InlineData("марʼяна")]
    [InlineData("МАР'ЯНА коваль")]
    public async Task AnApostropheVariant_FindsTheName(string text)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        await PersonAsync(host, Organizer, "Мар’яна Коваль", ct);
        var world = await WorldAsync(host, ct);
        await SeedCodeAsync(host, world, UnassignedCode, Unassigned, Organizer, ct);
        await SeedCodeAsync(host, world, "oth-unas-001", Unassigned, Mc.Email("other.organizer"), ct);

        Assert.Equal([UnassignedCode], (await CodesFoundAsync(host, text, ct)).Unassigned);
    }

    /// <summary>
    /// FR-003 (US-070 FR-001 rule 2, I-2): the words of one search may land in a name and in the code, the linked course or an
    /// email, or in the names of two organizers of one code; every word must still be found.
    /// </summary>
    [Fact]
    public async Task WordsOfOneSearch_LandInANameAndInAnotherField()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var shevchuk = Mc.Email("second.organizer");
        await PersonAsync(host, Organizer, Petrenko, ct);
        await PersonAsync(host, shevchuk, "Шевчук Олена", ct);
        var world = await SeedThreeAsync(host, Organizer, ct);
        await SeedCodeAsync(host, world, "two-orgs-001", Unassigned, Organizer, ct);
        await host.SeedMeetingAsync("two-orgs-001", shevchuk, At.AddDays(1), ct);
        await SeedCodeAsync(host, world, "one-orgs-002", Unassigned, shevchuk, ct);

        Assert.Equal([UnassignedCode], (await CodesFoundAsync(host, "петренко abcdefg", ct)).Unassigned);
        Assert.Equal([LinkedCode], (await CodesFoundAsync(host, "алгебр петренко", ct)).Linked);
        Assert.Equal(new MeetCodeListCounts(2, 1, 1), (await CodesFoundAsync(host, "name.organizer іван", ct)).Counts);
        Assert.Equal(["two-orgs-001"], (await CodesFoundAsync(host, "петренко шевчук", ct)).Unassigned);
        Assert.Equal(new MeetCodeListCounts(0, 0, 0), (await CodesFoundAsync(host, "петренко тарас", ct)).Counts);
    }

    /// <summary>
    /// AC-005, FR-003 (I-3, BR-085): only the chosen name is searched: neither the name of another participant of the same email,
    /// nor a surname or given name kept apart from the full name.
    /// </summary>
    [Fact]
    public async Task OnlyTheChosenName_IsSearched()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        await PersonAsync(host, Organizer, Petrenko, ct, surname: "Zzsurname", givenName: "Zzgiven");
        await PersonAsync(host, Organizer, Ivanenko, ct);
        await SeedThreeAsync(host, Organizer, ct);

        Assert.Equal(new MeetCodeListCounts(1, 1, 1), (await CodesFoundAsync(host, "петренко", ct)).Counts);
        Assert.Equal(new MeetCodeListCounts(0, 0, 0), (await CodesFoundAsync(host, "іваненко", ct)).Counts);
        Assert.Equal(new MeetCodeListCounts(0, 0, 0), (await CodesFoundAsync(host, "zzsurname", ct)).Counts);
        Assert.Equal(new MeetCodeListCounts(0, 0, 0), (await CodesFoundAsync(host, "zzgiven", ct)).Counts);
    }

    /// <summary>
    /// AC-004, FR-001, FR-003: an organizer erased or excluded is stored as null (US-038 I-9); after that neither their name nor their
    /// email finds the code, and the row no longer lists them, while a surviving organizer still finds it by name.
    /// </summary>
    [Fact]
    public async Task AClearedOrganizer_IsNotFoundByName_AndNotListed_ASurvivorStillIs()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var survivor = Mc.Email("survivor.person");
        await PersonAsync(host, Organizer, Petrenko, ct);
        await PersonAsync(host, survivor, "Шевчук Олена", ct);
        await SeedThreeAsync(host, Organizer, ct);
        foreach (var code in new[] { UnassignedCode, LinkedCode, MarkedCode })
        {
            await host.SeedMeetingAsync(code, survivor, At.AddDays(1), ct);
        }

        // Control: before the clearing the name finds a code on each list.
        Assert.Equal(new MeetCodeListCounts(1, 1, 1), (await CodesFoundAsync(host, "петренко", ct)).Counts);

        await host.ExecuteAsync("UPDATE meet_session SET organizer_email = NULL WHERE organizer_email = @e", ct, ("e", Organizer));

        Assert.Equal(new MeetCodeListCounts(0, 0, 0), (await CodesFoundAsync(host, "петренко", ct)).Counts);
        Assert.Equal(new MeetCodeListCounts(0, 0, 0), (await CodesFoundAsync(host, "name.organizer", ct)).Counts);
        Assert.Equal(new MeetCodeListCounts(1, 1, 1), (await CodesFoundAsync(host, "шевчук", ct)).Counts);
        foreach (var list in new[] { Unassigned, Linked, Marked })
        {
            Assert.Equal([new MeetOrganizerRecord(survivor, "Шевчук Олена")], await OrganizersAsync(host, list, CodeOf(list), ct));
        }
    }

    /// <summary>AC-006, FR-004: the header's first matches come through the same read, so a name finds the codes of all three lists.</summary>
    [Fact]
    public async Task TheHeadersFirstMatches_FindACodeByItsOrganizersName()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        await PersonAsync(host, Organizer, Petrenko, ct);
        var world = await SeedThreeAsync(host, Organizer, ct);
        await SeedCodeAsync(host, world, "oth-unas-001", Unassigned, Mc.Email("other.organizer"), ct);

        var found = await host.CodesMatchedAsync("петренко", 20, ct);

        Assert.Equal(
            [
                new MeetCodeMatch(UnassignedCode, MeetCodeList.Unassigned),
                new MeetCodeMatch(LinkedCode, MeetCodeList.Linked),
                new MeetCodeMatch(MarkedCode, MeetCodeList.NotACourse),
            ],
            found);
    }

    /// <summary>
    /// AC-008, FR-005: a page's names are read in a number of commands that the number of rows does not change: a page of ten codes,
    /// each with two named organizers, takes as many commands as a page of one.
    /// </summary>
    [Theory]
    [InlineData(Unassigned)]
    [InlineData(Linked)]
    [InlineData(Marked)]
    public async Task APageOfManyRows_ReadsNamesInAsManyCommands_AsAPageOfOne(string list)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var world = await WorldAsync(host, ct);
        for (var i = 0; i < 10; i++)
        {
            var first = Mc.Email($"first.{i}");
            var second = Mc.Email($"second.{i}");
            await PersonAsync(host, first, $"Перший{i} Викладач", ct);
            await PersonAsync(host, second, $"Другий{i} Викладач", ct);
            var code = $"pg{i}-aaaa-bbb";
            await SeedCodeAsync(host, world, code, list, first, ct, i);
            await host.SeedMeetingAsync(code, second, At.AddDays(i), ct);
        }

        var seen = new CommandRecorder();
        await using var db = NewContext(host, seen);
        var source = new MeetCodesReadSource(db);
        Func<int, Task<IReadOnlyList<IReadOnlyList<MeetOrganizerRecord>>>> read = list switch
        {
            Unassigned => async size => (await source.GetUnassignedPageAsync(0, size, Zone, null, ct)).Rows.Select(r => r.Organizers).ToList(),
            Linked => async size => (await source.GetLinkedPageAsync(0, size, null, ct)).Rows.Select(r => r.Organizers).ToList(),
            _ => async size => (await source.GetMarkedPageAsync(0, size, null, ct)).Rows.Select(r => r.Organizers).ToList(),
        };

        var before = seen.Commands.Count;
        var one = await read(1);
        var forOne = seen.Commands.Count - before;
        before = seen.Commands.Count;
        var ten = await read(10);
        var forTen = seen.Commands.Count - before;

        // The names were really read, so the count is about a read that happened.
        Assert.All(one.Concat(ten), organizers => Assert.All(organizers, o => Assert.NotNull(o.StoredFullName)));
        Assert.Equal(10, ten.Count);
        Assert.Equal(forOne, forTen);
    }

    /// <summary>Spec VR-003, db-design §5: a word searched in the names is a bound parameter, and the read still finds the code.</summary>
    [Fact]
    public async Task ANameWord_IsABoundParameter()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        await PersonAsync(host, Organizer, "Sentinelname Іван", ct);
        await SeedThreeAsync(host, Organizer, ct);
        var seen = new CommandRecorder();
        await using var db = NewContext(host, seen);
        var source = new MeetCodesReadSource(db);

        Assert.Equal(new MeetCodeListCounts(1, 1, 1), await source.GetCountsAsync("sentinelname", ct));
        Assert.Single((await source.GetUnassignedPageAsync(0, 50, Zone, "sentinelname", ct)).Rows);
        Assert.Single((await source.GetLinkedPageAsync(0, 50, "sentinelname", ct)).Rows);
        Assert.Single((await source.GetMarkedPageAsync(0, 50, "sentinelname", ct)).Rows);
        Assert.Equal(3, (await source.GetFirstMatchesAsync("sentinelname", 20, ct)).Count);

        Assert.NotEmpty(seen.Commands);
        Assert.DoesNotContain(seen.Commands, c => c.Contains("sentinelname", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Spec section 7 ("reads only"): reading names and searching by them write nothing.</summary>
    [Fact]
    public async Task ReadingAndSearchingNames_WritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        await PersonAsync(host, Organizer, Petrenko, ct);
        await SeedThreeAsync(host, Organizer, ct);
        var before = await host.McLinksFingerprintAsync(ct);
        var participants = await host.ScalarAsync<long>("SELECT count(*) FROM classroom_participant", ct);

        var found = await CodesFoundAsync(host, "петренко", ct);

        Assert.Equal(new MeetCodeListCounts(1, 1, 1), found.Counts);
        Assert.Equal(before, await host.McLinksFingerprintAsync(ct));
        Assert.Equal(participants, await host.ScalarAsync<long>("SELECT count(*) FROM classroom_participant", ct));
    }

    private static ClassroomAgentDbContext NewContext(InstallationTestHost host, CommandRecorder recorder)
    {
        var options = ClassroomAgentDbContextOptions
            .Configure(new DbContextOptionsBuilder<ClassroomAgentDbContext>(), host.ConnectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .AddInterceptors(recorder)
            .Options;
        return new ClassroomAgentDbContext((DbContextOptions<ClassroomAgentDbContext>)options);
    }

    /// <summary>Keeps the text of every database command a context executes.</summary>
    private sealed class CommandRecorder : DbCommandInterceptor
    {
        private readonly List<string> _commands = [];

        public IReadOnlyList<string> Commands
        {
            get
            {
                lock (_commands)
                {
                    return _commands.ToList();
                }
            }
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Add(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            Add(command);
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void Add(DbCommand command)
        {
            lock (_commands)
            {
                _commands.Add(command.CommandText);
            }
        }
    }
}
