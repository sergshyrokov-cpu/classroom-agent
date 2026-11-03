using System.Data.Common;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-070 db-design §2 … §5 over real PostgreSQL with the migrations applied (TC-2): the one set of matching rules of spec
/// FR-001 as the three sources apply them, what the Meet codes search looks at (FR-004, AC-005), the organizers on the
/// Linked and Not-a-course rows (OD-001 a), the word as a bound parameter and a number of commands that the number of
/// words does not change (spec sections 7 and 9). Rows are inserted by SQL; every value is invented (TC-4).
/// </summary>
public sealed class SearchMatchingSourceTests(PostgreSqlFixture database)
{
    private static readonly DateTimeOffset At = new(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly TimeZoneInfo Zone = JournalTestData.Kyiv;

    private async Task<InstallationTestHost> StartEmptyAsync(CancellationToken ct)
    {
        var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct);
        return host;
    }

    /// <summary>A teacher of one active course.</summary>
    private static async Task<long> TeacherAsync(
        InstallationTestHost host, int ordinal, string? surname, string? givenName, string? email, CancellationToken ct, string state = CourseTestData.States.Active)
    {
        var course = await host.InsertCourseAsync(ordinal, "Курс " + ordinal, ct, state);
        var person = await host.InsertPersonAsync(ordinal, surname, givenName, email, ct);
        await host.AddTeacherAsync(course, person, ct);
        return person;
    }

    private static async Task<IReadOnlyList<long>> TeachersFoundAsync(
        InstallationTestHost host, string? search, CancellationToken ct, CourseState? state = null)
    {
        IReadOnlyList<long> found = [];
        await host.WithTeacherListSourceAsync(async source =>
        {
            found = (await source.GetTeacherPageAsync(state, search, 0, 50, ct)).Select(r => r.ParticipantId).ToList();
            Assert.Equal(found.Count, await source.CountTeachersAsync(state, search, ct));
        });
        return found;
    }

    private static async Task<int> CoursesFoundAsync(
        InstallationTestHost host, string? search, CancellationToken ct, CourseState? state = null, long? teacher = null)
    {
        var count = 0;
        await host.WithCourseListSourceAsync(async source =>
        {
            var filter = new CourseListFilter(state, teacher, search);
            count = await source.CountCoursesAsync(filter, ct);
            Assert.Equal(count, (await source.GetCoursePageAsync(filter, 0, 50, ct)).Count);
        });
        return count;
    }

    private static async Task<(IReadOnlyList<string> Unassigned, IReadOnlyList<string> Linked, IReadOnlyList<string> Marked, MeetCodeListCounts Counts)>
        CodesFoundAsync(InstallationTestHost host, string? search, CancellationToken ct)
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
        return (unassigned, linked, marked, counts);
    }

    /// <summary>One code in each list; the course of the linked one is "Алгебра" with the section "10-А".</summary>
    private sealed record MeetWorld(long Algebra, long Maker);

    private const string UnassignedCode = "abc-defg-hij";

    private const string LinkedCode = "lnk-mmmm-nnn";

    private const string MarkedCode = "mrk-pppp-qqq";

    private static readonly string UnassignedOrganizer = Mc.Email("uns.organizer");

    private static readonly string LinkedOrganizer = Mc.Email("lnk.organizer");

    private static readonly string MarkedOrganizer = Mc.Email("mrk.organizer");

    private static async Task<MeetWorld> SeedMeetAsync(InstallationTestHost host, CancellationToken ct)
    {
        var algebra = await host.SeedRosterCourseAsync("Алгебра", [Mc.Email("algebra.teacher")], [], ct, section: "10-А");
        await host.InsertAppUserAsync(ct, email: Mc.Email("code.maker"));
        var maker = await host.AccountIdAsync(Mc.Email("code.maker"), ct);
        await host.SeedMeetingAsync(UnassignedCode, UnassignedOrganizer, At, ct);
        await host.SeedMeetingAsync(LinkedCode, LinkedOrganizer, At, ct);
        await host.SeedPersonLinkAsync(LinkedCode, algebra, maker, At, ct);
        await host.SeedMeetingAsync(MarkedCode, MarkedOrganizer, At, ct);
        await host.SeedMarkAsync(MarkedCode, maker, At, ct);
        return new MeetWorld(algebra, maker);
    }

    /// <summary>AC-001, FR-001 rules 1 to 4: words in any order, each a contiguous part, in any case, repeated white space.</summary>
    [Theory]
    [InlineData("петро іван")]
    [InlineData("іван петро")]
    [InlineData("ПЕТРО   ІВАН")]
    [InlineData("ване")]
    [InlineData("етр")]
    public async Task ATeacher_IsFound_ByWordsInAnyOrder_AsPartsOfWords_InAnyCase(string text)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var ivanenko = await TeacherAsync(host, 1, "Іваненко", "Петро", Mc.Email("first.one"), ct);
        await TeacherAsync(host, 2, "Шевчук", "Олена", Mc.Email("second.two"), ct);

        Assert.Equal([ivanenko], await TeachersFoundAsync(host, text, ct));
    }

    /// <summary>AC-001, I-1, FR-001 rule 2: every word must be found, and words may land in different fields.</summary>
    [Fact]
    public async Task Words_LandInDifferentFields_AndEveryWordMustBeFound()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var ivanenko = await TeacherAsync(host, 1, "Іваненко", "Петро", Mc.Email("first.one"), ct);
        await TeacherAsync(host, 2, "Іваненко", "Олена", Mc.Email("second.two"), ct);

        Assert.Equal([ivanenko], await TeachersFoundAsync(host, "first.one іваненко", ct));
        Assert.Equal([ivanenko], await TeachersFoundAsync(host, "іваненко петро", ct));
        Assert.Empty(await TeachersFoundAsync(host, "іваненко тарас", ct));
        Assert.Empty(await TeachersFoundAsync(host, "іванекно", ct));
    }

    /// <summary>FR-003: an absent value is simply not searched: a teacher with no email is found by name, and the name-less by email.</summary>
    [Fact]
    public async Task AnAbsentValue_IsNotSearched_AndTheOtherFieldsStillFind()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var noEmail = await TeacherAsync(host, 1, "Коваленко", "Ірина", null, ct);
        var noName = await TeacherAsync(host, 2, null, null, Mc.Email("noname.person"), ct);

        Assert.Equal([noEmail], await TeachersFoundAsync(host, "коваленко", ct));
        Assert.Equal([noName], await TeachersFoundAsync(host, "noname", ct));
    }

    /// <summary>FR-003: the status filter combines with the search by AND, and the count counts the matches only.</summary>
    [Fact]
    public async Task TheSearch_CombinesWithTheStatus_AndCountsMatchesOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var active = await TeacherAsync(host, 1, "Романенко", "Марія", null, ct);
        var archived = await TeacherAsync(host, 2, "Романюк", "Оксана", null, ct, CourseTestData.States.Archived);
        await TeacherAsync(host, 3, "Шевчук", "Олена", null, ct);

        Assert.Equal([active], await TeachersFoundAsync(host, "роман", ct, CourseState.Active));
        Assert.Equal([archived], await TeachersFoundAsync(host, "роман", ct, CourseState.Archived));
        Assert.Equal(2, (await TeachersFoundAsync(host, "роман", ct)).Count);
    }

    /// <summary>AC-001, FR-001 rule 5: any of the three apostrophes in the word finds any of the three in the field, on all three sources.</summary>
    [Theory]
    [InlineData("'", "’")]
    [InlineData("'", "ʼ")]
    [InlineData("’", "'")]
    [InlineData("’", "ʼ")]
    [InlineData("ʼ", "'")]
    [InlineData("ʼ", "’")]
    public async Task ApostropheVariants_CountAsOneCharacter(string stored, string typed)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var teacher = await TeacherAsync(host, 1, $"Д{stored}ячук", "Анна", null, ct);
        await host.InsertCourseAsync(2, $"Об{stored}єкти", ct);
        var section = await host.SeedRosterCourseAsync("Хімія", [Mc.Email("chem.teacher")], [], ct, section: $"П{stored}ятий");
        await host.SeedMeetingAsync("apo-aaaa-bbb", Mc.Email("apo.organizer"), At, ct);
        await host.SeedPersonLinkAsync("apo-aaaa-bbb", section, 987654, At, ct);

        Assert.Equal([teacher], await TeachersFoundAsync(host, $"д{typed}яч", ct));
        Assert.Equal(1, await CoursesFoundAsync(host, $"об{typed}єк", ct));
        Assert.Equal(["apo-aaaa-bbb"], (await CodesFoundAsync(host, $"п{typed}ят", ct)).Linked);
    }

    /// <summary>I-2: only the three listed characters fold; the others stay literal.</summary>
    [Theory]
    [InlineData("‘")]
    [InlineData("`")]
    [InlineData("′")]
    public async Task OtherApostropheLikeCharacters_StayLiteral(string typed)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var teacher = await TeacherAsync(host, 1, "Д'ячук", "Анна", null, ct);

        Assert.Equal([teacher], await TeachersFoundAsync(host, "д’яч", ct));
        Assert.Empty(await TeachersFoundAsync(host, $"д{typed}яч", ct));
    }

    /// <summary>AC-001, FR-001 rule 7: <c>%</c>, <c>_</c> and <c>\</c> match only themselves, on all three sources.</summary>
    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("\\")]
    public async Task PatternCharacters_MatchOnlyThemselves(string character)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var holder = await TeacherAsync(host, 1, $"Сто{character}ляр", "Анна", null, ct);
        await TeacherAsync(host, 2, "Столяр", "Олена", null, ct);
        await host.InsertCourseAsync(3, $"Сто{character}відсотків", ct);
        var section = await host.SeedRosterCourseAsync("Фізика", [Mc.Email("phys.teacher")], [], ct, section: $"Гру{character}па");
        await host.SeedMeetingAsync("lit-aaaa-bbb", Mc.Email("lit.organizer"), At, ct);
        await host.SeedPersonLinkAsync("lit-aaaa-bbb", section, 987654, At, ct);

        Assert.Equal([holder], await TeachersFoundAsync(host, character, ct));
        Assert.Equal(1, await CoursesFoundAsync(host, $"Сто{character}", ct));
        Assert.Equal(["lit-aaaa-bbb"], (await CodesFoundAsync(host, $"гру{character}", ct)).Linked);
    }

    /// <summary>AC-002, FR-002: a course is found by name and section, words spanning both (I-1), and a course with no section by name.</summary>
    [Fact]
    public async Task ACourse_IsFound_ByNameAndSection_WithWordsAcrossBoth()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        await host.InsertCourseAsync(1, "Математика", ct, section: "10-Б");
        await host.InsertCourseAsync(2, "Математика", ct);
        await host.InsertCourseAsync(3, "Хімія", ct, section: "11-А");

        Assert.Equal(1, await CoursesFoundAsync(host, "10-б математ", ct));
        Assert.Equal(2, await CoursesFoundAsync(host, "математика", ct));
        Assert.Equal(0, await CoursesFoundAsync(host, "математика 11", ct));
        Assert.Equal(0, await CoursesFoundAsync(host, "матемтика", ct));
    }

    /// <summary>AC-002, FR-002: the status and teacher filters combine with the search by AND.</summary>
    [Fact]
    public async Task OnTheCourseList_StatusAndTeacher_CombineWithTheSearch()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var taught = await host.InsertCourseAsync(1, "Фізика базова", ct);
        await host.InsertCourseAsync(2, "Фізика профільна", ct);
        await host.InsertCourseAsync(3, "Фізика архів", ct, CourseTestData.States.Archived);
        var teacher = await host.InsertPersonAsync(1, "Шевчук", "Олена", null, ct);
        await host.AddTeacherAsync(taught, teacher, ct);

        Assert.Equal(3, await CoursesFoundAsync(host, "фізика", ct));
        Assert.Equal(2, await CoursesFoundAsync(host, "фізика", ct, CourseState.Active));
        Assert.Equal(1, await CoursesFoundAsync(host, "фізика", ct, teacher: teacher));
        Assert.Equal(1, await CoursesFoundAsync(host, "фізика", ct, CourseState.Active, teacher));
        Assert.Equal(0, await CoursesFoundAsync(host, "фізика профільна", ct, CourseState.Active, teacher));
    }

    /// <summary>AC-001, FR-001 rule 6: hyphens are ignored in a code and in the word.</summary>
    [Theory]
    [InlineData("abcdefg")]
    [InlineData("abc-defg")]
    [InlineData("ABC-DEFG-HIJ")]
    [InlineData("c-d-e-f")]
    [InlineData("defghij")]
    public async Task ACode_IsFound_WithItsHyphensIgnored(string text)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        await SeedMeetAsync(host, ct);

        var found = await CodesFoundAsync(host, text, ct);

        Assert.Equal([UnassignedCode], found.Unassigned);
        Assert.Empty(found.Linked);
        Assert.Empty(found.Marked);
        Assert.Equal(new MeetCodeListCounts(1, 0, 0), found.Counts);
    }

    /// <summary>
    /// AC-001, FR-001 rule 6: a word of hyphens only finds no code, but may match another field: here the section "10-А"
    /// of the linked course holds a hyphen, so the linked code alone is found. The organizers here have no hyphen in their
    /// address on purpose: an email is an ordinary field, where a hyphen would match.
    /// </summary>
    [Theory]
    [InlineData("-")]
    [InlineData("---")]
    public async Task AWordOfHyphensOnly_FindsNoCode_ButMayMatchAnotherField(string text)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var algebra = await host.SeedRosterCourseAsync("Алгебра", [Mc.Email("algebra.teacher")], [], ct, section: "10-А");
        await host.SeedMeetingAsync(UnassignedCode, "plain@nohyphen.test", At, ct);
        await host.SeedMeetingAsync(LinkedCode, "plain@nohyphen.test", At, ct);
        await host.SeedPersonLinkAsync(LinkedCode, algebra, 987654, At, ct);
        await host.SeedMeetingAsync(MarkedCode, "plain@nohyphen.test", At, ct);
        await host.SeedMarkAsync(MarkedCode, 987654, At, ct);

        var found = await CodesFoundAsync(host, text, ct);

        Assert.Empty(found.Unassigned);
        Assert.Empty(found.Marked);
        if (text == "-")
        {
            Assert.Equal([LinkedCode], found.Linked);
            Assert.Equal(new MeetCodeListCounts(0, 1, 0), found.Counts);
        }
        else
        {
            Assert.Empty(found.Linked);
            Assert.Equal(new MeetCodeListCounts(0, 0, 0), found.Counts);
        }
    }

    /// <summary>FR-001 rule 6: outside a Meet code field a hyphen is an ordinary character.</summary>
    [Fact]
    public async Task OutsideACodeField_AHyphenIsOrdinary()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        await host.InsertCourseAsync(1, "Фізика", ct, section: "10-А");

        Assert.Equal(1, await CoursesFoundAsync(host, "10-а", ct));
        Assert.Equal(0, await CoursesFoundAsync(host, "10а", ct));
    }

    /// <summary>AC-004, FR-004: each list holds its own matches: by code, by organizer on every list, by the course only where linked.</summary>
    [Fact]
    public async Task EachList_HoldsItsMatches_AndTheCountsAgree()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        await SeedMeetAsync(host, ct);
        await host.SeedMeetingAsync("second-organizer", UnassignedOrganizer, At, ct);

        var byOrganizer = await CodesFoundAsync(host, "organizer@", ct);
        Assert.Equal(new MeetCodeListCounts(2, 1, 1), byOrganizer.Counts);
        Assert.Equal(2, byOrganizer.Unassigned.Count);
        Assert.Equal([LinkedCode], byOrganizer.Linked);
        Assert.Equal([MarkedCode], byOrganizer.Marked);

        var byLinkedOrganizer = await CodesFoundAsync(host, "lnk.organizer", ct);
        Assert.Equal(new MeetCodeListCounts(0, 1, 0), byLinkedOrganizer.Counts);

        var byMarkedOrganizer = await CodesFoundAsync(host, "mrk.organizer", ct);
        Assert.Equal(new MeetCodeListCounts(0, 0, 1), byMarkedOrganizer.Counts);

        var byCourseName = await CodesFoundAsync(host, "алгебр", ct);
        Assert.Equal(new MeetCodeListCounts(0, 1, 0), byCourseName.Counts);
        Assert.Equal([LinkedCode], byCourseName.Linked);

        var bySection = await CodesFoundAsync(host, "10-а", ct);
        Assert.Equal(new MeetCodeListCounts(0, 1, 0), bySection.Counts);

        var acrossFields = await CodesFoundAsync(host, "алгебра lnk.organizer", ct);
        Assert.Equal(new MeetCodeListCounts(0, 1, 0), acrossFields.Counts);

        var noSearch = await CodesFoundAsync(host, null, ct);
        Assert.Equal(new MeetCodeListCounts(2, 1, 1), noSearch.Counts);
    }

    /// <summary>FR-004: a candidate course of an unassigned code is not searched, though that code's candidate is known to the page.</summary>
    [Fact]
    public async Task ACandidateCourse_IsNotSearched()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var teacher = Mc.Email("candidate.teacher");
        await host.SeedRosterCourseAsync("Кандидатський курс", [teacher], [], ct);
        await host.SeedMeetingAsync("cnd-aaaa-bbb", teacher, At, ct);
        await host.SeedMeetingAsync("cnd-cccc-ddd", Mc.Email("other.person"), At, ct);

        Assert.Equal(["cnd-aaaa-bbb"], (await CodesFoundAsync(host, "candidate.teacher", ct)).Unassigned);
        var found = await CodesFoundAsync(host, "кандидатський", ct);
        Assert.Empty(found.Unassigned);
        Assert.Equal(0, found.Counts.Unassigned);
    }

    /// <summary>FR-004: the accounts that linked, confirmed or marked a code are not searched.</summary>
    [Fact]
    public async Task TheAccountsThatActedOnACode_AreNotSearched()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        await SeedMeetAsync(host, ct);

        Assert.Equal(new MeetCodeListCounts(0, 0, 0), (await CodesFoundAsync(host, "code.maker", ct)).Counts);
        Assert.Equal(new MeetCodeListCounts(0, 1, 0), (await CodesFoundAsync(host, "lnk.organizer", ct)).Counts);
    }

    /// <summary>
    /// AC-005, FR-004: an organizer erased or excluded is stored as null (US-038 I-9), so no email finds that meeting; a code
    /// with one surviving organizer is still found by the survivor and shows only that one.
    /// </summary>
    [Fact]
    public async Task AnErasedOrganizer_FindsNothing_OnAnyList_AndASurvivorStillFinds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var world = await SeedMeetAsync(host, ct);
        var erased = Mc.Email("erased.person");
        var survivor = Mc.Email("survivor.person");
        foreach (var (code, list) in new[] { ("ers-unas-001", "u"), ("ers-link-002", "l"), ("ers-mark-003", "m") })
        {
            await host.SeedMeetingAsync(code, erased, At, ct);
            await host.SeedMeetingAsync(code, survivor, At.AddDays(1), ct);
            if (list == "l")
            {
                await host.SeedPersonLinkAsync(code, world.Algebra, world.Maker, At, ct);
            }
            else if (list == "m")
            {
                await host.SeedMarkAsync(code, world.Maker, At, ct);
            }
        }

        // Control: before the erasure the email finds a code on each list.
        Assert.Equal(new MeetCodeListCounts(1, 1, 1), (await CodesFoundAsync(host, "erased.person", ct)).Counts);

        await host.ExecuteAsync("UPDATE meet_session SET organizer_email = NULL WHERE organizer_email = @e", ct, ("e", erased));

        var byErased = await CodesFoundAsync(host, "erased.person", ct);
        Assert.Equal(new MeetCodeListCounts(0, 0, 0), byErased.Counts);
        Assert.Empty(byErased.Unassigned);
        Assert.Empty(byErased.Linked);
        Assert.Empty(byErased.Marked);
        Assert.Equal(new MeetCodeListCounts(1, 1, 1), (await CodesFoundAsync(host, "survivor.person", ct)).Counts);
    }

    /// <summary>FR-004, db-design §4: every session of a code counts, not only the latest.</summary>
    [Fact]
    public async Task AnyStoredSessionOfACode_LendsItsOrganizer()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        await host.SeedMeetingAsync("two-sess-ion", Mc.Email("older.organizer"), At, ct);
        await host.SeedMeetingAsync("two-sess-ion", Mc.Email("newer.organizer"), At.AddDays(7), ct);
        await host.SeedMeetingAsync("oth-code-001", Mc.Email("other.person"), At, ct);

        Assert.Equal(["two-sess-ion"], (await CodesFoundAsync(host, "older.organizer", ct)).Unassigned);
        Assert.Equal(["two-sess-ion"], (await CodesFoundAsync(host, "newer.organizer", ct)).Unassigned);
    }

    /// <summary>
    /// FR-004, OD-001 a, db-design §5: Linked and Not-a-course rows carry the code's distinct non-null organizers in ordinal
    /// order; a code with none left, or no session, has an empty list.
    /// </summary>
    [Fact]
    public async Task LinkedAndMarkedRows_CarryDistinctOrganizers_InOrdinalOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var world = await SeedMeetAsync(host, ct);
        var zed = Mc.Email("zed.person");
        var ann = Mc.Email("ann.person");
        await host.SeedMeetingAsync(LinkedCode, zed, At.AddDays(1), ct);
        await host.SeedMeetingAsync(LinkedCode, ann, At.AddDays(2), ct);
        await host.SeedMeetingAsync(LinkedCode, ann, At.AddDays(3), ct);
        await host.SeedMeetingAsync(MarkedCode, zed, At.AddDays(1), ct);
        await host.SeedPersonLinkAsync("lnk-noss-ion", world.Algebra, world.Maker, At, ct);
        await host.SeedMarkAsync("mrk-noss-ion", world.Maker, At, ct);

        await host.WithMeetCodesReadSourceAsync(async source =>
        {
            var linked = (await source.GetLinkedPageAsync(0, 50, null, ct)).Rows;
            string[] expectedLinked = [LinkedOrganizer, ann, zed];
            Array.Sort(expectedLinked, StringComparer.Ordinal);
            Assert.Equal(expectedLinked, linked.Single(r => r.MeetingCode == LinkedCode).OrganizerEmails);
            Assert.Empty(linked.Single(r => r.MeetingCode == "lnk-noss-ion").OrganizerEmails);

            var marked = (await source.GetMarkedPageAsync(0, 50, null, ct)).Rows;
            string[] expectedMarked = [MarkedOrganizer, zed];
            Array.Sort(expectedMarked, StringComparer.Ordinal);
            Assert.Equal(expectedMarked, marked.Single(r => r.MeetingCode == MarkedCode).OrganizerEmails);
            Assert.Empty(marked.Single(r => r.MeetingCode == "mrk-noss-ion").OrganizerEmails);
        });
    }

    /// <summary>OD-001 a: a row keeps every organizer of the code even when the search matched just one of them.</summary>
    [Fact]
    public async Task UnderASearch_ARow_StillListsAllItsOrganizers()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        await SeedMeetAsync(host, ct);
        var second = Mc.Email("second.person");
        await host.SeedMeetingAsync(LinkedCode, second, At.AddDays(1), ct);

        await host.WithMeetCodesReadSourceAsync(async source =>
        {
            var row = Assert.Single((await source.GetLinkedPageAsync(0, 50, "second.person", ct)).Rows);
            Assert.Contains(LinkedOrganizer, row.OrganizerEmails);
            Assert.Contains(second, row.OrganizerEmails);
        });
    }

    /// <summary>Db-design §4: the total of a page read is the matches, and the page holds only them, with no search as today.</summary>
    [Fact]
    public async Task ThePagedReads_TotalTheMatchesOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        await SeedMeetAsync(host, ct);
        await host.SeedMeetingAsync("second-organizer", UnassignedOrganizer, At, ct);

        await host.WithMeetCodesReadSourceAsync(async source =>
        {
            Assert.Equal(2, (await source.GetUnassignedPageAsync(0, 1, Zone, null, ct)).TotalElements);
            var one = await source.GetUnassignedPageAsync(0, 1, Zone, "abcdefg", ct);
            Assert.Equal(1, one.TotalElements);
            Assert.Equal([UnassignedCode], one.Rows.Select(r => r.MeetingCode));
            Assert.Equal(1, (await source.GetLinkedPageAsync(0, 1, "lnk", ct)).TotalElements);
            Assert.Equal(0, (await source.GetLinkedPageAsync(0, 1, "zzz", ct)).TotalElements);
            Assert.Equal(1, (await source.GetMarkedPageAsync(0, 1, "mrk", ct)).TotalElements);
            Assert.Equal(0, (await source.GetMarkedPageAsync(0, 1, "zzz", ct)).TotalElements);
        });
    }

    /// <summary>
    /// Spec section 7, db-design §2: the word is a bound parameter on every path, the raw SQL of the Unassigned page included,
    /// and the read still finds the record, so the assertion is about a word that really was used.
    /// </summary>
    [Fact]
    public async Task TheWord_IsABoundParameter_InEveryRead()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        await SeedMeetAsync(host, ct);
        await TeacherAsync(host, 50, "Сентинельчук", "Анна", null, ct);
        await host.SeedMeetingAsync("sentinelword-code", Mc.Email("uns.other"), At, ct);
        await host.InsertCourseAsync(51, "sentinelword курс", ct);
        var seen = new CommandRecorder();
        await using var db = NewContext(host, seen);

        var teachers = new TeacherListSource(db);
        Assert.Equal(1, await teachers.CountTeachersAsync(null, "сентинельчук", ct));
        Assert.Single(await teachers.GetTeacherPageAsync(null, "сентинельчук", 0, 50, ct));
        var courses = new CourseListSource(db);
        var filter = new CourseListFilter(null, null, "sentinelword");
        Assert.Equal(1, await courses.CountCoursesAsync(filter, ct));
        Assert.Single(await courses.GetCoursePageAsync(filter, 0, 50, ct));
        var codes = new MeetCodesReadSource(db);
        Assert.Equal(1, (await codes.GetCountsAsync("sentinelword", ct)).Unassigned);
        Assert.Single((await codes.GetUnassignedPageAsync(0, 50, Zone, "sentinelword", ct)).Rows);
        await codes.GetLinkedPageAsync(0, 50, "sentinelword", ct);
        await codes.GetMarkedPageAsync(0, 50, "sentinelword", ct);

        Assert.NotEmpty(seen.Commands);
        Assert.DoesNotContain(seen.Commands, c => c.Contains("sentinelword", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(seen.Commands, c => c.Contains("сентинельчук", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Spec section 9, db-design §4: the number of words changes a query's conditions, never the number of commands; a page
    /// read of one word and of many take as many commands as each other.
    /// </summary>
    [Theory]
    [InlineData("unassigned")]
    [InlineData("linked")]
    [InlineData("marked")]
    [InlineData("counts")]
    [InlineData("teachers")]
    public async Task TheNumberOfWords_DoesNotChangeTheNumberOfCommands(string read)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var world = await SeedMeetAsync(host, ct);
        await TeacherAsync(host, 50, "Абвгдеж", "Анна", null, ct);

        // A second row on each list that the words do not match, so "finds its one row" proves the search was applied.
        var plain = Mc.Email("plain.person");
        await host.SeedMeetingAsync("zzz-unas-001", plain, At, ct);
        await host.SeedMeetingAsync("zzz-link-002", plain, At, ct);
        await host.SeedPersonLinkAsync("zzz-link-002", world.Algebra, world.Maker, At, ct);
        await host.SeedMeetingAsync("zzz-mark-003", plain, At, ct);
        await host.SeedMarkAsync("zzz-mark-003", world.Maker, At, ct);
        var seen = new CommandRecorder();
        await using var db = NewContext(host, seen);
        var codes = new MeetCodesReadSource(db);
        var teachers = new TeacherListSource(db);

        // Each text finds its one row, so a page takes the same follow-up reads either way, and the search was applied.
        Func<string, Task<long>> run = read switch
        {
            "unassigned" => async text => (await codes.GetUnassignedPageAsync(0, 50, Zone, text, ct)).TotalElements,
            "linked" => async text => (await codes.GetLinkedPageAsync(0, 50, text, ct)).TotalElements,
            "marked" => async text => (await codes.GetMarkedPageAsync(0, 50, text, ct)).TotalElements,
            "counts" => async text =>
            {
                var c = await codes.GetCountsAsync(text, ct);
                return c.Unassigned + c.Linked + c.NotACourse;
            }
            ,
            "teachers" => async text => await teachers.CountTeachersAsync(null, text, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(read), read, null),
        };
        var word = read switch
        {
            "unassigned" => "abc",
            "linked" => "lnk",
            "marked" => "mrk",
            "counts" => "abc",
            _ => "абв",
        };

        var before = seen.Commands.Count;
        Assert.Equal(1, await run(word));
        var one = seen.Commands.Count - before;

        before = seen.Commands.Count;
        Assert.Equal(1, await run(string.Join(' ', Enumerable.Repeat(word, 10))));
        var many = seen.Commands.Count - before;

        Assert.Equal(one, many);
    }

    /// <summary>Spec section 7 ("reads only"): searching writes nothing.</summary>
    [Fact]
    public async Task Searching_WritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        await SeedMeetAsync(host, ct);
        await host.SeedMeetingAsync("oth-code-001", Mc.Email("other.person"), At, ct);
        var before = await host.McLinksFingerprintAsync(ct);

        var found = await CodesFoundAsync(host, "abcdefg", ct);

        Assert.Equal([UnassignedCode], found.Unassigned);
        Assert.Equal(before, await host.McLinksFingerprintAsync(ct));
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
