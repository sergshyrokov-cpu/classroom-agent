using System.Data.Common;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Cl = ClassroomAgent.Tests.TestInfrastructure.CourseListHostExtensions;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-020 db-design §2 … §4, AC-003, AC-004, AC-006, AC-007, AC-008, AC-011, AC-019, AC-020 and Q1–Q6 over real
/// PostgreSQL with the migrations applied (TC-2): the state, current-teacher and search filters; Ukrainian-aware,
/// case-insensitive, literal search; the Ukrainian-aware order with Cyrillic before Latin; database paging; current
/// teachers and students only; one database command per call; nothing written. Rows are inserted by SQL.
/// </summary>
public sealed class CourseListSourceTests(PostgreSqlFixture database)
{
    private static CourseListFilter All(long? teacher = null, string? search = null) => new(null, teacher, search);

    private async Task<InstallationTestHost> StartEmptyAsync(CancellationToken ct)
    {
        var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct);
        return host;
    }

    private static async Task<IReadOnlyList<string>> NamesAsync(
        ICourseListSource source, CourseListFilter filter, CancellationToken ct) =>
        (await source.GetCoursePageAsync(filter, 0, 50, ct)).Select(c => c.Name).ToList();

    /// <summary>AC-004, Q1, FR-005: current teachers of any course, whatever its state, each once; no former teacher, no student.</summary>
    [Fact]
    public async Task TheTeacherChoice_IsTheCurrentTeachersOfAnyCourse_EachOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var world = await host.SeedCourseListAsync(ct);

        await host.WithCourseListSourceAsync(async source =>
        {
            var choice = await source.GetTeacherChoiceAsync(ct);

            Assert.Equal([world.Koval, world.Ukrainka, world.Shevchenko], choice.Select(p => p.ParticipantId));
            var shevchenko = choice.Single(p => p.ParticipantId == world.Shevchenko);
            Assert.Equal(new CourseListPersonRecord(world.Shevchenko, "Шевченко", "Тарас", Cl.ShevchenkoEmail), shevchenko);
        });
    }

    /// <summary>
    /// FR-005, FR-008, db-design §2.2, §3.4: the choice is ordered by the label key in the Ukrainian alphabet (ґ after г,
    /// і after и), the email standing in for a missing name, an empty part absent, Cyrillic before Latin, no label last,
    /// equal labels by id.
    /// </summary>
    [Fact]
    public async Task TheTeacherChoice_IsOrderedByTheLabelKey()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var course = await host.InsertCourseAsync(1, "Курс", ct);
        var noName = await host.InsertPersonAsync(1, null, null, null, ct);
        var smith = await host.InsertPersonAsync(2, "Smith", "John", null, ct);
        var email = await host.InsertPersonAsync(3, null, null, "a.zz@school.example", ct);
        var yana = await host.InsertPersonAsync(4, "", "Яна", null, ct);
        var gudz = await host.InsertPersonAsync(5, "Ґудзь", "Ірина", null, ct);
        var hnatiuk = await host.InsertPersonAsync(6, "Гнатюк", "Іван", null, ct);
        var bondarFirst = await host.InsertPersonAsync(7, "Бондар", "Анна", null, ct);
        var bondarSecond = await host.InsertPersonAsync(8, "Бондар", "Анна", null, ct);
        foreach (var person in new[] { noName, smith, email, yana, gudz, hnatiuk, bondarSecond, bondarFirst })
        {
            await host.AddTeacherAsync(course, person, ct);
        }

        await host.WithCourseListSourceAsync(async source =>
        {
            var choice = await source.GetTeacherChoiceAsync(ct);

            Assert.Equal(
                [bondarFirst, bondarSecond, hnatiuk, gudz, yana, email, smith, noName],
                choice.Select(p => p.ParticipantId));
        });
    }

    /// <summary>AC-002, AC-003, Q2, Q4: the state filter, and no state for "all".</summary>
    [Fact]
    public async Task TheStateFilter_KeepsThatState_AndNullKeepsAll()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var world = await host.SeedCourseListAsync(ct);

        await host.WithCourseListSourceAsync(async source =>
        {
            Assert.Equal(2, await source.CountCoursesAsync(new CourseListFilter(CourseState.Active, null, null), ct));
            Assert.Equal(1, await source.CountCoursesAsync(new CourseListFilter(CourseState.Archived, null, null), ct));
            Assert.Equal(1, await source.CountCoursesAsync(new CourseListFilter(CourseState.Provisioned, null, null), ct));
            Assert.Equal(0, await source.CountCoursesAsync(new CourseListFilter(CourseState.Declined, null, null), ct));
            Assert.Equal(4, await source.CountCoursesAsync(All(), ct));
            Assert.Equal(
                [world.Physics, world.Chemistry],
                (await source.GetCoursePageAsync(new CourseListFilter(CourseState.Active, null, null), 0, 50, ct)).Select(c => c.CourseId));
        });
    }

    /// <summary>AC-004, Q2, Q4, FR-005: a teacher's courses are those they teach on the roster now — not as a former teacher or a student.</summary>
    [Fact]
    public async Task TheTeacherFilter_KeepsTheCoursesTheTeacherTeachesNow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var world = await host.SeedCourseListAsync(ct);
        await host.AddStudentAsync(world.Chemistry, world.Ukrainka, ct);

        await host.WithCourseListSourceAsync(async source =>
        {
            Assert.Equal([Cl.HistoryName, Cl.PhysicsName], await NamesAsync(source, All(world.Shevchenko), ct));
            Assert.Equal([Cl.PhysicsName], await NamesAsync(source, All(world.Ukrainka), ct));
            Assert.Empty(await NamesAsync(source, All(world.Franko), ct));
            Assert.Equal(0, await source.CountCoursesAsync(All(world.Franko), ct));
            Assert.Equal(1, await source.CountCoursesAsync(All(world.Ukrainka), ct));
        });
    }

    /// <summary>
    /// AC-006, AC-019, FR-006, db-design §3.2: a substring of the name or the section, in any letter case — Ukrainian
    /// letters included — and nothing else folded.
    /// </summary>
    [Fact]
    public async Task TheSearch_IsACaseInsensitiveSubstringOfTheNameOrTheSection()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        await host.InsertCourseAsync(1, "Історія", ct);
        await host.InsertCourseAsync(2, "Фізика", ct, section: "9-Б клас");
        await host.InsertCourseAsync(3, "Їжак і Ґанок", ct);
        await host.InsertCourseAsync(4, "Єдність", ct);
        await host.InsertCourseAsync(5, "Physics", ct, section: null);

        await host.WithCourseListSourceAsync(async source =>
        {
            Assert.Equal(["Історія"], await NamesAsync(source, All(search: "ІСТ"), ct));
            Assert.Equal(["Історія"], await NamesAsync(source, All(search: "істор"), ct));
            Assert.Equal(["Фізика"], await NamesAsync(source, All(search: "9-б"), ct));
            Assert.Equal(["Фізика"], await NamesAsync(source, All(search: "КЛАС"), ct));
            Assert.Equal(["Їжак і Ґанок"], await NamesAsync(source, All(search: "їЖАК"), ct));
            Assert.Equal(["Їжак і Ґанок"], await NamesAsync(source, All(search: "ґанок"), ct));
            Assert.Equal(["Єдність"], await NamesAsync(source, All(search: "єДН"), ct));
            Assert.Equal(["Physics"], await NamesAsync(source, All(search: "PHYS"), ct));

            // FR-006: no other folding — е is not є, и is not і.
            Assert.Empty(await NamesAsync(source, All(search: "едн"), ct));
            Assert.Empty(await NamesAsync(source, All(search: "истор"), ct));
            Assert.Equal(0, await source.CountCoursesAsync(All(search: "едн"), ct));
            Assert.Equal(1, await source.CountCoursesAsync(All(search: "ІСТ"), ct));
        });
    }

    /// <summary>AC-019, FR-006, I-4, S-09: the text is matched literally — %, _ and \ are themselves, not patterns.</summary>
    [Fact]
    public async Task TheSearch_IsLiteral()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        await host.InsertCourseAsync(1, "Знижка 100% курс", ct);
        await host.InsertCourseAsync(2, "a_b", ct);
        await host.InsertCourseAsync(3, @"c\d", ct);
        await host.InsertCourseAsync(4, "axb", ct);
        await host.InsertCourseAsync(5, "Хімія", ct, section: "50% групи");

        await host.WithCourseListSourceAsync(async source =>
        {
            Assert.Equal(["Знижка 100% курс", "Хімія"], await NamesAsync(source, All(search: "%"), ct));
            Assert.Equal(["a_b"], await NamesAsync(source, All(search: "_"), ct));
            Assert.Equal(["a_b"], await NamesAsync(source, All(search: "a_b"), ct));
            Assert.Equal([@"c\d"], await NamesAsync(source, All(search: @"\"), ct));
            Assert.Empty(await NamesAsync(source, All(search: "'; DROP TABLE course; --"), ct));
            Assert.Equal(5, await source.CountCoursesAsync(All(), ct));
        });
    }

    /// <summary>AC-007: state, teacher and search combine with AND.</summary>
    [Fact]
    public async Task TheFilters_Combine()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var teacher = await host.InsertPersonAsync(1, "Шевченко", "Тарас", null, ct);
        var match = await host.InsertCourseAsync(1, "Фізика 9", ct, CourseTestData.States.Archived);
        var wrongState = await host.InsertCourseAsync(2, "Фізика 10", ct);
        await host.InsertCourseAsync(3, "Фізика 11", ct, CourseTestData.States.Archived);
        var wrongName = await host.InsertCourseAsync(4, "Хімія", ct, CourseTestData.States.Archived);
        foreach (var course in new[] { match, wrongState, wrongName })
        {
            await host.AddTeacherAsync(course, teacher, ct);
        }

        await host.WithCourseListSourceAsync(async source =>
        {
            var filter = new CourseListFilter(CourseState.Archived, teacher, "ФІЗ");

            Assert.Equal(1, await source.CountCoursesAsync(filter, ct));
            Assert.Equal([match], (await source.GetCoursePageAsync(filter, 0, 50, ct)).Select(c => c.CourseId));
        });
    }

    /// <summary>
    /// AC-019, FR-008, I-7, db-design §2.2, §3.3: Ukrainian order (ґ after г, і after и), digits first, Cyrillic before
    /// Latin, Latin case-insensitive; then the section with none first; then the id.
    /// </summary>
    [Fact]
    public async Task ThePage_IsInUkrainianOrder_ThenSection_ThenId()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var ordinal = 0;
        async Task<long> Add(string name, string? section = null) =>
            await host.InsertCourseAsync(++ordinal, name, ct, section: section);

        var physicsB2 = await Add("Фізика", "9-Б");
        var physicsB1 = await Add("Фізика", "9-Б");
        var latinUpper = await Add("Physics");
        var history = await Add("Історія");
        var physicsA = await Add("Фізика", "10-А");
        var informatics = await Add("Інформатика");
        var latinLower = await Add("algebra");
        var geography = await Add("Географія");
        var soil = await Add("Ґрунтознавство");
        var physicsNone = await Add("Фізика");
        var algebra = await Add("Алгебра");
        var digits = await Add("10-А");

        await host.WithCourseListSourceAsync(async source =>
        {
            var page = await source.GetCoursePageAsync(All(), 0, 50, ct);

            Assert.Equal(
                [
                    digits, algebra, geography, soil, informatics, history, physicsNone, physicsA,
                    Math.Min(physicsB1, physicsB2), Math.Max(physicsB1, physicsB2), latinLower, latinUpper,
                ],
                page.Select(c => c.CourseId));
        });
    }

    /// <summary>AC-008, AC-020, Q4: paging in the database — 120 courses, offset 50 gives 51–100, offset 100 gives 101–120.</summary>
    [Fact]
    public async Task ThePage_SkipsAndTakesInTheDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var ids = new List<long>();
        for (var i = 1; i <= 120; i++)
        {
            ids.Add(await host.InsertCourseAsync(i, $"Курс {i:D3}", ct));
        }

        await host.WithCourseListSourceAsync(async source =>
        {
            Assert.Equal(120, await source.CountCoursesAsync(All(), ct));
            Assert.Equal(ids.Skip(50).Take(50), (await source.GetCoursePageAsync(All(), 50, 50, ct)).Select(c => c.CourseId));
            Assert.Equal(ids.Skip(100), (await source.GetCoursePageAsync(All(), 100, 50, ct)).Select(c => c.CourseId));
        });
    }

    /// <summary>AC-001, Q4, OD-008: the record carries the name, section, state and update time as stored.</summary>
    [Fact]
    public async Task ThePageRecord_CarriesTheStoredValues()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var world = await host.SeedCourseListAsync(ct);

        await host.WithCourseListSourceAsync(async source =>
        {
            var page = await source.GetCoursePageAsync(All(), 0, 50, ct);

            var physics = Assert.Single(page, c => c.CourseId == world.Physics);
            Assert.Equal(
                new CourseListCourseRecord(world.Physics, Cl.PhysicsName, Cl.PhysicsSection, CourseState.Active, Cl.PhysicsUpdateTime),
                physics);
            Assert.Null(Assert.Single(page, c => c.CourseId == world.Algebra).UpdateTime);
        });
    }

    /// <summary>AC-011, Q3: whether any course is stored at all.</summary>
    [Fact]
    public async Task AnyCourseStored_IsFalseOnAnEmptyDatabase_AndTrueWithOneCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);

        var before = false;
        await host.WithCourseListSourceAsync(async source => before = await source.AnyCourseStoredAsync(ct));
        await host.InsertCourseAsync(1, "Архівний", ct, CourseTestData.States.Archived);
        var after = false;
        await host.WithCourseListSourceAsync(async source => after = await source.AnyCourseStoredAsync(ct));

        Assert.False(before);
        Assert.True(after);
    }

    /// <summary>AC-001, Q5, FR-003: the current teachers of the given courses only, by course then label; no former teacher.</summary>
    [Fact]
    public async Task CurrentTeachers_AreOfTheGivenCoursesOnly_InOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var world = await host.SeedCourseListAsync(ct);

        await host.WithCourseListSourceAsync(async source =>
        {
            var teachers = await source.GetCurrentTeachersAsync([world.Algebra, world.Chemistry, world.Physics], ct);
            var none = await source.GetCurrentTeachersAsync([], ct);

            var expected = new[]
                {
                    (world.Physics, world.Ukrainka), (world.Physics, world.Shevchenko), (world.Chemistry, world.Koval),
                }
                .OrderBy(t => t.Item1)
                .ToList();
            Assert.Equal(expected, teachers.Select(t => (t.CourseId, t.Teacher.ParticipantId)));
            Assert.Empty(none);
        });
    }

    /// <summary>AC-001, Q6, FR-003: current students per course; a leaver and a teacher do not count; a course with none is absent.</summary>
    [Fact]
    public async Task StudentCounts_AreTheCurrentStudentsOfTheGivenCourses()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var world = await host.SeedCourseListAsync(ct);

        await host.WithCourseListSourceAsync(async source =>
        {
            var counts = await source.GetStudentCountsAsync([world.Physics, world.Chemistry, world.Algebra], ct);

            Assert.Equal(
                new Dictionary<long, int> { [world.Physics] = 3, [world.Chemistry] = 1 },
                counts.ToDictionary(c => c.CourseId, c => c.StudentCount));
        });
    }

    /// <summary>
    /// AC-020, FR-016: every read is one database command, whatever the data size — 50 courses' teachers and counts
    /// included — so a request makes a fixed number of round trips.
    /// </summary>
    [Fact]
    public async Task EveryRead_IsOneDatabaseCommand()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var ids = new List<long>();
        for (var i = 1; i <= 60; i++)
        {
            var course = await host.InsertCourseAsync(i, $"Курс {i:D3}", ct);
            ids.Add(course);
            await host.AddTeacherAsync(course, await host.InsertPersonAsync(i, $"Вчитель{i:D3}", "Ім'я", null, ct), ct);
            await host.AddStudentAsync(course, await host.InsertPersonAsync(1000 + i, null, null, CourseTestData.Email($"s{i}"), ct), ct);
        }

        var counter = new CommandCounter();
        var options = ClassroomAgentDbContextOptions
            .Configure(new DbContextOptionsBuilder<ClassroomAgentDbContext>(), host.ConnectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .AddInterceptors(counter)
            .Options;
        await using var db = new ClassroomAgentDbContext((DbContextOptions<ClassroomAgentDbContext>)options);
        var source = new CourseListSource(db);
        var page = ids.Take(50).ToList();

        var commands = new List<int>();
        async Task Count(Func<Task> read)
        {
            var before = counter.Count;
            await read();
            commands.Add(counter.Count - before);
        }

        await Count(() => source.GetTeacherChoiceAsync(ct));
        await Count(() => source.CountCoursesAsync(new CourseListFilter(CourseState.Active, null, "курс"), ct));
        await Count(() => source.AnyCourseStoredAsync(ct));
        await Count(() => source.GetCoursePageAsync(All(), 0, 50, ct));
        await Count(() => source.GetCurrentTeachersAsync(page, ct));
        await Count(() => source.GetStudentCountsAsync(page, ct));

        Assert.Equal([1, 1, 1, 1, 1, 1], commands);
    }

    /// <summary>FR-015, FR-016, S-12: reading writes nothing.</summary>
    [Fact]
    public async Task Reading_WritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var world = await host.SeedCourseListAsync(ct);
        var before = await host.CourseListFingerprintAsync(ct);

        await host.WithCourseListSourceAsync(async source =>
        {
            await source.GetTeacherChoiceAsync(ct);
            await source.CountCoursesAsync(All(world.Shevchenko, "ф"), ct);
            await source.AnyCourseStoredAsync(ct);
            await source.GetCoursePageAsync(All(), 0, 50, ct);
            await source.GetCurrentTeachersAsync([world.Physics], ct);
            await source.GetStudentCountsAsync([world.Physics], ct);
        });

        Assert.Equal(before, await host.CourseListFingerprintAsync(ct));
    }

    /// <summary>Counts the database commands a context executes.</summary>
    private sealed class CommandCounter : DbCommandInterceptor
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
