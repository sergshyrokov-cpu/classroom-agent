using System.Data.Common;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-071 db-design §3 and §4 over real PostgreSQL with the migrations applied (TC-2): who is a student of the search and what
/// is matched, the courses of the shown students with what the BR-068 mark needs, the first Meet code matches across the three
/// lists, what must not be found (AC-006), the text as a bound parameter and a fixed number of commands (spec sections 7, 9).
/// Rows are inserted by SQL; every value is invented (TC-4).
/// </summary>
public sealed class StudentSearchSourceTests(PostgreSqlFixture database)
{
    private static readonly DateTimeOffset At = SearchHostExtensions.At;

    private async Task<InstallationTestHost> StartEmptyAsync(CancellationToken ct)
    {
        var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct);
        return host;
    }

    private static async Task<long> StudentAsync(
        InstallationTestHost host, int ordinal, string? surname, string? givenName, string? email, CancellationToken ct, bool onRoster = true)
    {
        var course = await host.InsertCourseAsync(ordinal, "Курс " + ordinal, ct);
        var person = await host.InsertPersonAsync(ordinal, surname, givenName, email, ct);
        await host.AddStudentAsync(course, person, ct, onRoster);
        return person;
    }

    /// <summary>FR-003: found by surname, given name or email, by words in any order, as parts, in any case.</summary>
    [Theory]
    [InlineData("іваненко")]
    [InlineData("ПЕТРО")]
    [InlineData("петро іван")]
    [InlineData("ване")]
    [InlineData("first.one")]
    [InlineData("first.one іваненко")]
    public async Task AStudent_IsFound_BySurnameGivenNameOrEmail(string text)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var ivanenko = await StudentAsync(host, 1, "Іваненко", "Петро", Mc.Email("first.one"), ct);
        await StudentAsync(host, 2, "Шевчук", "Олена", Mc.Email("second.two"), ct);

        Assert.Equal([ivanenko], await host.StudentsFoundAsync(text, ct));
    }

    /// <summary>FR-003, BR-087: every word must be found; a typo finds nothing (no typo tolerance).</summary>
    [Theory]
    [InlineData("іваненко тарас")]
    [InlineData("іванекно")]
    public async Task EveryWord_MustBeFound(string text)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        await StudentAsync(host, 1, "Іваненко", "Петро", Mc.Email("first.one"), ct);

        Assert.Empty(await host.StudentsFoundAsync(text, ct));
    }

    /// <summary>US-070 FR-001 rule 5, section 7 "Folding": any apostrophe finds any, through <c>SearchMatch</c>.</summary>
    [Theory]
    [InlineData("'", "’")]
    [InlineData("’", "ʼ")]
    [InlineData("ʼ", "'")]
    public async Task ApostropheVariants_CountAsOneCharacter(string stored, string typed)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var student = await StudentAsync(host, 1, "Д" + stored + "яченко", "Анна", null, ct);

        Assert.Equal([student], await host.StudentsFoundAsync("Д" + typed + "ячен", ct));
    }

    /// <summary>Section 7 "Injection": <c>%</c>, <c>_</c> and <c>\</c> match only themselves.</summary>
    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData(@"\")]
    public async Task PatternCharacters_MatchOnlyThemselves(string character)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var holder = await StudentAsync(host, 1, "Ко" + character + "валь", "Анна", null, ct);
        await StudentAsync(host, 2, "Шевчук", "Олена", null, ct);

        Assert.Equal([holder], await host.StudentsFoundAsync(character + "в", ct));
    }

    /// <summary>FR-003, AC-003: a student who has left every roster is found.</summary>
    [Fact]
    public async Task ADepartedStudent_IsFound()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var departed = await StudentAsync(host, 1, "Мельник", "Петро", null, ct, onRoster: false);

        Assert.Equal([departed], await host.StudentsFoundAsync("мельник", ct));
    }

    /// <summary>
    /// FR-003, AC-003, I-4: one known only through submissions counts when named; the same membership with no name or email
    /// cannot be matched by any text, the control proving that it is stored as a student membership.
    /// </summary>
    [Fact]
    public async Task AStudentWithNoNameOrEmail_IsNotFound_ByAnyText()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var course = await host.InsertCourseAsync(1, "Курс", ct);
        var unnamed = await host.InsertPersonAsync(1, null, null, null, ct);
        await CourseRows.InsertMembershipAsync(host, course, unnamed, ct, firstSeenAt: At, lastSeenAt: At, onRoster: false);
        var named = await host.InsertPersonAsync(2, "Названий", null, null, ct);
        await CourseRows.InsertMembershipAsync(host, course, named, ct, firstSeenAt: At, lastSeenAt: At, onRoster: false);

        Assert.Equal([named], await host.StudentsFoundAsync("названий", ct));
        Assert.Empty(await host.StudentsFoundAsync("null", ct));
        Assert.Single(await host.StudentCoursesAsync([unnamed], ct));
    }

    /// <summary>FR-003, AC-002: only a teacher is not a student; a person in both roles is found here.</summary>
    [Fact]
    public async Task OnlyAStudentMembership_Qualifies()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var course = await host.InsertCourseAsync(1, "Курс", ct);
        var teacherOnly = await host.InsertPersonAsync(1, "Романенко", "Марія", null, ct);
        await host.AddTeacherAsync(course, teacherOnly, ct);
        var both = await host.InsertPersonAsync(2, "Романюк", "Оксана", null, ct);
        var other = await host.InsertCourseAsync(2, "Другий", ct);
        await host.AddTeacherAsync(course, both, ct);
        await host.AddStudentAsync(other, both, ct);

        Assert.Equal([both], await host.StudentsFoundAsync("роман", ct));
    }

    /// <summary>Section 10: <c>full_name</c> is not searched.</summary>
    [Fact]
    public async Task TheFullName_IsNotSearched()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var course = await host.InsertCourseAsync(1, "Курс", ct);
        var person = await CourseRows.InsertParticipantAsync(
            host, ct, googleUserId: CourseTestData.UserId(1), email: null, fullName: "Лише Повнеімя", surname: "Коваль", givenName: "Анна");
        await host.AddStudentAsync(course, person, ct);

        Assert.Empty(await host.StudentsFoundAsync("повнеімя", ct));
        Assert.Equal([person], await host.StudentsFoundAsync("коваль", ct));
    }

    /// <summary>FR-007, FR-002: alphabetical by the label, namesakes two rows in a stable order, at most the limit; the count is all of them.</summary>
    [Fact]
    public async Task Rows_AreAlphabetical_NamesakesApart_UpToTheLimit_AndTheCountIsAll()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var c = await StudentAsync(host, 1, "Спільна", "Юлія", null, ct);
        var a = await StudentAsync(host, 2, "Спільна", "Анна", Mc.Email("first.namesake"), ct);
        var b = await StudentAsync(host, 3, "Спільна", "Анна", Mc.Email("second.namesake"), ct);
        var byEmail = await StudentAsync(host, 4, null, null, "spilna@" + InstallationTestData.Domain, ct);

        await host.WithStudentSearchSourceAsync(async source =>
        {
            Assert.Equal(1, await source.CountStudentsAsync("spilna", ct));
            Assert.Equal(3, await source.CountStudentsAsync("спільна", ct));
            var rows = await source.GetStudentsAsync("спільна", 2, ct);
            Assert.Equal([Math.Min(a, b), Math.Max(a, b)], rows.Select(r => r.ParticipantId));
            var all = await source.GetStudentsAsync("спільна", 20, ct);
            Assert.Equal([Math.Min(a, b), Math.Max(a, b), c], all.Select(r => r.ParticipantId));
            var named = Assert.Single(await source.GetStudentsAsync("spilna", 20, ct));
            Assert.Equal(new StudentSearchRecord(byEmail, null, null, "spilna@" + InstallationTestData.Domain), named);
        });
    }

    /// <summary>
    /// FR-003: all the courses a person is a student of, whichever the search matched, each with its sightings; teacher
    /// memberships are not listed, any course status is, and the order is the course list's.
    /// </summary>
    [Fact]
    public async Task TheCourses_AreEveryStudentMembership_InTheCourseListsOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var person = await host.InsertPersonAsync(1, "Мельник", "Петро", null, ct);
        var other = await host.InsertPersonAsync(2, "Шевчук", "Олена", null, ct);
        var yaBiology = await host.InsertCourseAsync(1, "Ядро", ct, CourseTestData.States.Archived);
        var eSection = await host.InsertCourseAsync(2, "Алгебра", ct, section: "10-Б");
        var eNoSection = await host.InsertCourseAsync(3, "Алгебра", ct);
        var teaching = await host.InsertCourseAsync(4, "Викладає", ct);
        var firstSeen = At.AddDays(-5);
        await CourseRows.InsertMembershipAsync(host, yaBiology, person, ct, firstSeenAt: At, lastSeenAt: At, onRoster: false);
        await CourseRows.InsertMembershipAsync(host, eSection, person, ct, firstSeenAt: firstSeen, lastSeenAt: At, onRoster: false);
        await CourseRows.InsertMembershipAsync(host, eNoSection, person, ct, firstSeenAt: firstSeen, lastSeenAt: At);
        await host.AddTeacherAsync(teaching, person, ct);
        await host.AddStudentAsync(teaching, other, ct);

        var courses = await host.StudentCoursesAsync([person], ct);

        Assert.Equal(
            [
                new StudentSearchCourseRecord(person, eNoSection, "Алгебра", null, true, false, At),
                new StudentSearchCourseRecord(person, eSection, "Алгебра", "10-Б", false, false, At),
                new StudentSearchCourseRecord(person, yaBiology, "Ядро", null, false, true, At),
            ],
            courses);
    }

    /// <summary>Section 9: no shown student takes no read; the courses of several come from one command.</summary>
    [Fact]
    public async Task TheCoursesOfTheShownStudents_TakeOneCommand_AndNoneForNoStudent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var ids = await host.SeedNumberedStudentsAsync(20, ct);
        var seen = new CommandRecorder();
        await using var db = NewContext(host, seen);
        var source = new StudentSearchSource(db);

        Assert.Empty(await source.GetStudentCoursesAsync([], ct));
        Assert.Empty(seen.Commands);

        var courses = await source.GetStudentCoursesAsync(ids, ct);

        Assert.Equal(20, courses.Count);
        Assert.Single(seen.Commands);
    }

    /// <summary>Section 7 "Injection": the text is a bound parameter in the count and the rows.</summary>
    [Fact]
    public async Task TheWord_IsABoundParameter()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var seen = new CommandRecorder();
        await using var db = NewContext(host, seen);
        var source = new StudentSearchSource(db);

        await source.CountStudentsAsync("zzparam", ct);
        await source.GetStudentsAsync("zzparam", 20, ct);

        Assert.Equal(2, seen.Commands.Count);
        Assert.All(seen.Commands, command => Assert.DoesNotContain("zzparam", command, StringComparison.Ordinal));
    }

    /// <summary>Section 9: words change a query's conditions, never the number of commands.</summary>
    [Fact]
    public async Task TheNumberOfWords_DoesNotChangeTheNumberOfCommands()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        await StudentAsync(host, 1, "Абвгдеж", "Анна", null, ct);
        var seen = new CommandRecorder();
        await using var db = NewContext(host, seen);
        var source = new StudentSearchSource(db);

        Assert.Single(await source.GetStudentsAsync("абв", 20, ct));
        var one = seen.Commands.Count;
        Assert.Single(await source.GetStudentsAsync("абв анн где еж", 20, ct));

        Assert.Equal(one, seen.Commands.Count - one);
    }

    /// <summary>AC-006: an erased person is found before the erasure and by no text after it, and their row is gone.</summary>
    [Fact]
    public async Task AnErasedPerson_IsNotFound_ByNameOrEmail()
    {
        var ct = TestContext.Current.CancellationToken;

        // The erasure use case is guarded: it needs an installation with a succeeded legitimacy check, as ParticipantErasureTests.
        var (started, client) = await JournalHostExtensions.StartAsync(database, JournalHostExtensions.Actor.Anonymous, ct);
        client.Dispose();
        await using var host = started;
        await host.WaitForPurgeRunsAsync(1, ct);
        var email = Mc.Email("erased.student");
        var erased = await StudentAsync(host, 1, "Стертий", "Анна", email, ct);
        var survivor = await StudentAsync(host, 2, "Стертюк", "Олена", Mc.Email("survivor.student"), ct);
        var admin = await host.InsertAdminAsync(ct);
        Assert.Equal([erased], await host.StudentsFoundAsync("стертий", ct));
        Assert.Equal([erased], await host.StudentsFoundAsync("erased.student", ct));

        await host.EraseAsync(admin, erased, ct);

        Assert.Empty(await host.StudentsFoundAsync("стертий", ct));
        Assert.Empty(await host.StudentsFoundAsync("erased.student", ct));
        Assert.Equal([survivor], await host.StudentsFoundAsync("стертюк", ct));
        Assert.Equal(0, await host.ScalarAsync<long>("SELECT count(*) FROM classroom_participant WHERE id = @id", ct, ("id", erased)));
    }

    /// <summary>AC-006: an exclusion row alone gives the search nothing to find. That no participant is stored for the person is proved by ErasureExclusionInAdvanceSyncTests.</summary>
    [Fact]
    public async Task AnExcludedPerson_IsNotFound_ByTheirEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var email = Mc.Email("excluded.student");
        await host.SeedExclusionInAdvanceAsync(email, ct);
        var control = await StudentAsync(host, 1, "Контрольний", null, Mc.Email("excluded.control"), ct);

        Assert.Empty(await host.StudentsFoundAsync("excluded.student", ct));
        Assert.Equal([control], await host.StudentsFoundAsync("excluded.control", ct));
    }

    /// <summary>AC-006, BR-085: only the current stored name is searched: after a change the former surname finds nothing.</summary>
    [Fact]
    public async Task AFormerSurname_IsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var person = await StudentAsync(host, 1, "Давнє", "Анна", Mc.Email("old.name"), ct);
        Assert.Equal([person], await host.StudentsFoundAsync("давнє", ct));

        await host.ExecuteAsync(
            "UPDATE classroom_participant SET surname = @s, email = @e WHERE id = @id",
            ct,
            ("s", "Нове"),
            ("e", Mc.Email("new.name")),
            ("id", person));

        Assert.Empty(await host.StudentsFoundAsync("давнє", ct));
        Assert.Empty(await host.StudentsFoundAsync("old.name", ct));
        Assert.Equal([person], await host.StudentsFoundAsync("нове", ct));
    }

    /// <summary>FR-014: reading writes nothing.</summary>
    [Fact]
    public async Task Searching_WritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var person = await StudentAsync(host, 1, "Коваль", "Анна", null, ct);
        var before = await host.CourseListFingerprintAsync(ct);

        await host.StudentsFoundAsync("коваль", ct);
        await host.StudentCoursesAsync([person], ct);

        Assert.Equal(before, await host.CourseListFingerprintAsync(ct));
    }

    /// <summary>FR-006, db-design §4: the first matches across the three lists, each with its list, by the code in ordinal order.</summary>
    [Fact]
    public async Task TheFirstMeetMatches_AreMergedAcrossTheLists_ByTheCode()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        await host.SeedSampleCodesAsync(2, 2, 2, ct);

        var matches = await host.CodesMatchedAsync("zzm", 4, ct);

        Assert.Equal(
            [
                new MeetCodeMatch("zzm-0001-aaa", MeetCodeList.Unassigned),
                new MeetCodeMatch("zzm-0002-aaa", MeetCodeList.Unassigned),
                new MeetCodeMatch("zzm-0003-aaa", MeetCodeList.Linked),
                new MeetCodeMatch("zzm-0004-aaa", MeetCodeList.Linked),
            ],
            matches);
    }

    /// <summary>FR-007, db-design §4: a list's codes past another list's limit still merge in order; the limit bounds the merge.</summary>
    [Fact]
    public async Task TheMerge_TakesTheLimit_InOrderAcrossTheLists()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        await host.SeedSampleCodesAsync(1, 1, 3, ct);
        await host.SeedMeetingAsync("zzm-0000-aaa", Mc.Email("org.one"), At, ct);

        var matches = await host.CodesMatchedAsync("zzm", 3, ct);

        Assert.Equal(["zzm-0000-aaa", "zzm-0001-aaa", "zzm-0002-aaa"], matches.Select(m => m.Code));
        Assert.Equal([MeetCodeList.Unassigned, MeetCodeList.Unassigned, MeetCodeList.Linked], matches.Select(m => m.List));
    }

    /// <summary>FR-006: the same match as the screen: code with its hyphens ignored, linked course name and section, organizer emails.</summary>
    [Theory]
    [InlineData("zzmatchaaa", "zzm-atch-aaa")]
    [InlineData("zzm-atch", "zzm-atch-aaa")]
    [InlineData("organizer.x", "zzm-atch-aaa")]
    [InlineData("линкований", "lnk-mmmm-nnn")]
    [InlineData("10-в", "lnk-mmmm-nnn")]
    public async Task AMeetCode_IsMatched_ByEveryFieldOfTheScreen(string text, string code)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        await host.InsertAppUserAsync(ct, email: Mc.Email("code.maker"));
        var maker = await host.AccountIdAsync(Mc.Email("code.maker"), ct);
        var course = await host.SeedRosterCourseAsync("Линкований предмет", [Mc.Email("links.teacher")], [], ct, section: "10-В");
        await host.SeedMeetingAsync("zzm-atch-aaa", Mc.Email("organizer.x"), At, ct);
        await host.SeedMeetingAsync("lnk-mmmm-nnn", Mc.Email("organizer.y"), At, ct);
        await host.SeedPersonLinkAsync("lnk-mmmm-nnn", course, maker, At, ct);

        var match = Assert.Single(await host.CodesMatchedAsync(text, 20, ct));

        Assert.Equal(code, match.Code);
    }

    /// <summary>AC-006: a meeting whose organizer was erased, stored as null, is not found by that email; a survivor still finds it.</summary>
    [Fact]
    public async Task AnErasedOrganizer_IsNotFound_AndASurvivorStillFinds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var erased = Mc.Email("erased.organizer");
        await host.SeedMeetingAsync("ers-unas-001", erased, At, ct);
        await host.SeedMeetingAsync("ers-unas-001", Mc.Email("survivor.organizer"), At.AddDays(1), ct);
        Assert.Single(await host.CodesMatchedAsync("erased.organizer", 20, ct));

        await host.ExecuteAsync("UPDATE meet_session SET organizer_email = NULL WHERE organizer_email = @e", ct, ("e", erased));

        Assert.Empty(await host.CodesMatchedAsync("erased.organizer", 20, ct));
        Assert.Single(await host.CodesMatchedAsync("survivor.organizer", 20, ct));
    }

    /// <summary>Section 9, db-design §5: one command per list, whatever the words; the text is a bound parameter.</summary>
    [Fact]
    public async Task TheFirstMatches_TakeThreeCommands_WithTheWordsBound()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        await host.SeedSampleCodesAsync(1, 1, 1, ct);
        var seen = new CommandRecorder();
        await using var db = NewContext(host, seen);
        var source = new MeetCodesReadSource(db);

        Assert.Equal(3, (await source.GetFirstMatchesAsync("zzm", 20, ct)).Count);
        Assert.Equal(3, seen.Commands.Count);
        Assert.Equal(3, (await source.GetFirstMatchesAsync("zzm aaa 000", 20, ct)).Count);

        Assert.Equal(6, seen.Commands.Count);
        Assert.All(seen.Commands, command => Assert.DoesNotContain("zzm", command, StringComparison.Ordinal));
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
