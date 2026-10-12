using System.Data.Common;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Tl = ClassroomAgent.Tests.TestInfrastructure.TeacherListHostExtensions;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-022 db-design §2 … §4, T1 and T2 over real PostgreSQL with the migrations applied (TC-2): who is listed under each
/// status (current teacher on the roster of a course of that status), the course count of that status, the minimal
/// projection, the Ukrainian-aware label-key order shared with the course list's teacher choice, database paging, the
/// count agreeing with the course list's teacher filter, one database command per call and nothing written
/// (AC-001, AC-002, AC-003, AC-005, AC-014, AC-015, AC-016). Rows are inserted by SQL.
/// </summary>
public sealed class TeacherListSourceTests(PostgreSqlFixture database)
{
    private async Task<InstallationTestHost> StartEmptyAsync(CancellationToken ct)
    {
        var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct);
        return host;
    }

    private static async Task<IReadOnlyList<(long Id, int Count)>> ListAsync(
        ITeacherListSource source, CourseState? state, CancellationToken ct) =>
        (await source.GetTeacherPageAsync(state, 0, 50, ct)).Select(r => (r.ParticipantId, r.CourseCount)).ToList();

    /// <summary>
    /// AC-001, AC-002, AC-016, FR-002, FR-003: ACTIVE lists the current teachers of active courses with their active
    /// counts — not the archived-only teacher, the former teacher or the student; ARCHIVED and "all" likewise.
    /// </summary>
    [Fact]
    public async Task EachStatus_ListsItsCurrentTeachers_WithTheirCountsOfThatStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var w = await host.SeedTeacherListAsync(ct);

        await host.WithTeacherListSourceAsync(async source =>
        {
            Assert.Equal([(w.Ukrainka, 1), (w.Shevchenko, 2)], await ListAsync(source, CourseState.Active, ct));
            Assert.Equal([(w.Koval, 1), (w.Ukrainka, 1)], await ListAsync(source, CourseState.Archived, ct));
            Assert.Equal([(w.Koval, 1), (w.Ukrainka, 2), (w.Shevchenko, 2)], await ListAsync(source, null, ct));
            Assert.Empty(await ListAsync(source, CourseState.Declined, ct));
            Assert.Equal(2, await source.CountTeachersAsync(CourseState.Active, ct));
            Assert.Equal(2, await source.CountTeachersAsync(CourseState.Archived, ct));
            Assert.Equal(3, await source.CountTeachersAsync(null, ct));
            Assert.Equal(0, await source.CountTeachersAsync(CourseState.Declined, ct));
        });
    }

    /// <summary>I-7: a teacher of courses in several states — "all" is the sum of the five states.</summary>
    [Fact]
    public async Task UnderAll_TheCount_IsTheSumOverTheStates()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var teacher = await host.InsertPersonAsync(1, "Бондар", "Анна", null, ct);
        var states = new[]
        {
            CourseTestData.States.Active, CourseTestData.States.Archived, CourseTestData.States.Provisioned,
            CourseTestData.States.Declined, CourseTestData.States.Suspended, CourseTestData.States.Active,
        };
        for (var i = 0; i < states.Length; i++)
        {
            await host.AddTeacherAsync(await host.InsertCourseAsync(i + 1, $"Курс {i}", ct, states[i]), teacher, ct);
        }

        await host.WithTeacherListSourceAsync(async source =>
        {
            Assert.Equal([(teacher, 6)], await ListAsync(source, null, ct));
            Assert.Equal([(teacher, 2)], await ListAsync(source, CourseState.Active, ct));
            Assert.Equal([(teacher, 1)], await ListAsync(source, CourseState.Suspended, ct));
            Assert.Equal([(teacher, 1)], await ListAsync(source, CourseState.Provisioned, ct));
        });
    }

    /// <summary>AC-003, db-design §5: the record carries the id, the name parts and the email as stored, and the count.</summary>
    [Fact]
    public async Task TheRecord_CarriesTheStoredValues()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var w = await host.SeedTeacherListAsync(ct);

        await host.WithTeacherListSourceAsync(async source =>
        {
            var page = await source.GetTeacherPageAsync(CourseState.Active, 0, 50, ct);

            Assert.Equal(
                new TeacherListRecord(w.Shevchenko, "Шевченко", "Тарас", Tl.ShevchenkoEmail, 2),
                Assert.Single(page, r => r.ParticipantId == w.Shevchenko));
        });
    }

    /// <summary>
    /// AC-014, FR-006, db-design §3.3: the label key in the Ukrainian alphabet (Г, Ґ, І in their places), the email for a
    /// missing name, Cyrillic before Latin, neither name nor email last, equal labels by id.
    /// </summary>
    [Fact]
    public async Task TheOrder_IsTheLabelKey_InTheUkrainianAlphabet()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var course = await host.InsertCourseAsync(1, "Курс", ct);
        var nobody = await host.InsertPersonAsync(1, null, null, null, ct);
        var zoe = await host.InsertPersonAsync(2, null, null, "zoe@school.example", ct);
        var ivanenko = await host.InsertPersonAsync(3, "Іваненко", "Ірина", null, ct);
        var gudz = await host.InsertPersonAsync(4, "Ґудзь", "Анна", null, ct);
        var hnatiuk = await host.InsertPersonAsync(5, "Гнатюк", "Олег", null, ct);
        var andriienkoLowerId = await host.InsertPersonAsync(6, "Андрієнко", "Петро", null, ct);
        var andriienkoHigherId = await host.InsertPersonAsync(7, "Андрієнко", "Петро", null, ct);
        var emptyParts = await host.InsertPersonAsync(8, "", "", null, ct);
        foreach (var person in new[] { nobody, zoe, ivanenko, gudz, hnatiuk, andriienkoHigherId, andriienkoLowerId, emptyParts })
        {
            await host.AddTeacherAsync(course, person, ct);
        }

        await host.WithTeacherListSourceAsync(async source =>
        {
            var order = (await source.GetTeacherPageAsync(CourseState.Active, 0, 50, ct)).Select(r => r.ParticipantId);

            Assert.Equal(
                [andriienkoLowerId, andriienkoHigherId, hnatiuk, gudz, ivanenko, zoe, nobody, emptyParts],
                order);
        });
    }

    /// <summary>
    /// AC-014, db-design §3.3: the teacher list's order equals the course list's teacher choice for the same people, so
    /// both pages read the names in one order.
    /// </summary>
    [Fact]
    public async Task TheOrder_EqualsTheCourseListsTeacherChoice()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var course = await host.InsertCourseAsync(1, "Курс", ct);
        var people = new[]
        {
            await host.InsertPersonAsync(1, "Smith", "John", null, ct),
            await host.InsertPersonAsync(2, null, "Яна", "a@school.example", ct),
            await host.InsertPersonAsync(3, "Єрмак", null, null, ct),
            await host.InsertPersonAsync(4, null, null, "b.zz@school.example", ct),
            await host.InsertPersonAsync(5, "Їжак", "Ігор", null, ct),
            await host.InsertPersonAsync(6, "Ельф", "Ганна", null, ct),
            await host.InsertPersonAsync(7, null, null, null, ct),
            await host.InsertPersonAsync(8, "smith", "anna", null, ct),
        };
        foreach (var person in people)
        {
            await host.AddTeacherAsync(course, person, ct);
        }

        IReadOnlyList<long> choice = [];
        await host.WithCourseListSourceAsync(async source =>
            choice = (await source.GetTeacherChoiceAsync(ct)).Select(p => p.ParticipantId).ToList());
        IReadOnlyList<long> teachers = [];
        await host.WithTeacherListSourceAsync(async source =>
            teachers = (await source.GetTeacherPageAsync(null, 0, 50, ct)).Select(r => r.ParticipantId).ToList());

        Assert.Equal(8, choice.Count);
        Assert.Equal(choice, teachers);
    }

    /// <summary>AC-005, AC-015, FR-006: 120 teachers — the database pages them by label: 1–50, 51–100, 101–120.</summary>
    [Fact]
    public async Task Paging_HappensInTheDatabase_InLabelOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var ids = await host.SeedNumberedTeachersAsync(120, ct);

        await host.WithTeacherListSourceAsync(async source =>
        {
            Assert.Equal(120, await source.CountTeachersAsync(CourseState.Active, ct));
            Assert.Equal(ids.Take(50), (await source.GetTeacherPageAsync(CourseState.Active, 0, 50, ct)).Select(r => r.ParticipantId));
            Assert.Equal(ids.Skip(50).Take(50), (await source.GetTeacherPageAsync(CourseState.Active, 50, 50, ct)).Select(r => r.ParticipantId));
            Assert.Equal(ids.Skip(100), (await source.GetTeacherPageAsync(CourseState.Active, 100, 50, ct)).Select(r => r.ParticipantId));
            Assert.Empty(await source.GetTeacherPageAsync(CourseState.Active, 150, 50, ct));
        });
    }

    /// <summary>
    /// AC-016, FR-007, db-design §2: the teacher who teaches two active courses, left a third's roster and studies a
    /// fourth counts 2 — and the course list's filter for that teacher and status returns exactly those two courses.
    /// </summary>
    [Fact]
    public async Task TheCount_AgreesWithTheCourseListsTeacherFilter()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var w = await host.SeedTeacherListAsync(ct);

        var counts = new Dictionary<(long, CourseState?), int>();
        await host.WithTeacherListSourceAsync(async source =>
        {
            foreach (var state in new CourseState?[] { CourseState.Active, CourseState.Archived, null })
            {
                foreach (var r in await source.GetTeacherPageAsync(state, 0, 50, ct))
                {
                    counts[(r.ParticipantId, state)] = r.CourseCount;
                }
            }
        });

        await host.WithCourseListSourceAsync(async source =>
        {
            var shevchenko = await source.GetCoursePageAsync(new CourseListFilter(CourseState.Active, w.Shevchenko, null), 0, 50, ct);
            Assert.Equal([w.Physics, w.Chemistry], shevchenko.Select(c => c.CourseId));
            Assert.Equal(2, counts[(w.Shevchenko, CourseState.Active)]);

            foreach (var ((participant, state), count) in counts)
            {
                Assert.Equal(count, await source.CountCoursesAsync(new CourseListFilter(state, participant, null), ct));
            }

            var choice = (await source.GetTeacherChoiceAsync(ct)).Select(p => p.ParticipantId).ToHashSet();
            Assert.All(counts.Keys, k => Assert.Contains(k.Item1, choice));
        });
    }

    /// <summary>AC-008: an empty school — nobody is counted or listed.</summary>
    [Fact]
    public async Task AnEmptySchool_ListsNobody()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);

        await host.WithTeacherListSourceAsync(async source =>
        {
            Assert.Equal(0, await source.CountTeachersAsync(null, ct));
            Assert.Empty(await source.GetTeacherPageAsync(null, 0, 50, ct));
        });
    }

    /// <summary>
    /// AC-015, FR-014: each call is one database command, whatever the size of the school and the page asked for — so a
    /// request makes at most two round trips.
    /// </summary>
    [Fact]
    public async Task EveryRead_IsOneDatabaseCommand()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.SeedNumberedTeachersAsync(120, ct);

        var counter = new CommandCounter();
        var options = ClassroomAgentDbContextOptions
            .Configure(new DbContextOptionsBuilder<ClassroomAgentDbContext>(), host.ConnectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .AddInterceptors(counter)
            .Options;
        await using var db = new ClassroomAgentDbContext((DbContextOptions<ClassroomAgentDbContext>)options);
        var source = new TeacherListSource(db);

        var commands = new List<int>();
        async Task Count(Func<Task> read)
        {
            var before = counter.Count;
            await read();
            commands.Add(counter.Count - before);
        }

        await Count(() => source.CountTeachersAsync(CourseState.Active, ct));
        await Count(() => source.CountTeachersAsync(null, ct));
        await Count(() => source.GetTeacherPageAsync(CourseState.Active, 0, 50, ct));
        await Count(() => source.GetTeacherPageAsync(null, 100, 50, ct));

        Assert.Equal([1, 1, 1, 1], commands);
    }

    /// <summary>FR-013, FR-014, S-12: reading writes nothing.</summary>
    [Fact]
    public async Task Reading_WritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.SeedTeacherListAsync(ct);
        var before = await host.CourseListFingerprintAsync(ct);

        await host.WithTeacherListSourceAsync(async source =>
        {
            await source.CountTeachersAsync(CourseState.Active, ct);
            await source.GetTeacherPageAsync(null, 0, 50, ct);
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
