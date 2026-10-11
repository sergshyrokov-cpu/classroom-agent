using System.Data.Common;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Cc = ClassroomAgent.Tests.TestInfrastructure.CourseCardHostExtensions;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-021 db-design §2 … §6, AC-002 … AC-006, AC-015, AC-017 over real PostgreSQL with the migrations applied (TC-2):
/// Q1 returns the displayable values or null; Q2 returns every membership of one course and none of another, in the
/// Ukrainian-aware order with nameless rows last and the email and id as tie-breakers; the owner is decided in the
/// database; a submitter-only student comes back off the roster; each read is one command whatever the roster size;
/// nothing is written. Rows are inserted by SQL.
/// </summary>
public sealed class CourseCardSourceTests(PostgreSqlFixture database)
{
    private async Task<InstallationTestHost> StartEmptyAsync(CancellationToken ct)
    {
        var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct);
        return host;
    }

    /// <summary>AC-001, Q1, FR-002: the record carries the stored displayable values and the stored instants.</summary>
    [Fact]
    public async Task TheCourse_IsReadWithItsDisplayableValues()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var world = await host.SeedCourseCardAsync(ct);

        await host.WithCourseCardSourceAsync(async source =>
        {
            var course = await source.GetCourseAsync(world.Physics, ct);

            Assert.NotNull(course);
            Assert.Equal(
                new CourseCardCourseRecord(
                    world.Physics,
                    Cc.PhysicsName,
                    Cc.PhysicsSection,
                    CourseState.Active,
                    Cc.PhysicsRoom,
                    Cc.PhysicsHeading,
                    Cc.PhysicsDescription,
                    Cc.PhysicsCreationTime,
                    Cc.PhysicsUpdateTime),
                course with
                {
                    CreationTime = course.CreationTime?.ToUniversalTime(),
                    UpdateTime = course.UpdateTime?.ToUniversalTime(),
                });
        });
    }

    /// <summary>AC-001, FR-002: a course stored without the optional values comes back with nulls.</summary>
    [Fact]
    public async Task ACourseWithoutOptionalValues_HasNulls()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var course = await host.InsertEmptyCourseAsync(1, "Алгебра", ct);

        await host.WithCourseCardSourceAsync(async source =>
        {
            Assert.Equal(
                new CourseCardCourseRecord(course, "Алгебра", null, CourseState.Active, null, null, null, null, null),
                await source.GetCourseAsync(course, ct));
        });
    }

    /// <summary>AC-008, FR-011, I-9: no course has the id — null.</summary>
    [Fact]
    public async Task NoCourse_IsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var world = await host.SeedCourseCardAsync(ct);

        await host.WithCourseCardSourceAsync(async source =>
        {
            Assert.Null(await source.GetCourseAsync(world.Chemistry + 1000, ct));
            Assert.Null(await source.GetCourseAsync(long.MaxValue, ct));
        });
    }

    /// <summary>
    /// AC-002, AC-004, Q2, FR-003: every membership of the course with its role, roster flag and last seen; none of the
    /// other course.
    /// </summary>
    [Fact]
    public async Task TheMembers_AreEveryMembershipOfTheCourse_AndNoneOfAnother()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var world = await host.SeedCourseCardAsync(ct);

        await host.WithCourseCardSourceAsync(async source =>
        {
            var members = await source.GetMembersAsync(world.Physics, ct);

            Assert.Equal(12, members.Count);
            Assert.Equal(members.Count, members.Select(m => m.ParticipantId).Distinct().Count());
            Assert.Equal(2, members.Count(m => m.Role == ClassroomRole.Teacher && m.OnRoster));
            Assert.Equal(7, members.Count(m => m.Role == ClassroomRole.Student && m.OnRoster));
            Assert.Equal(3, members.Count(m => !m.OnRoster));
            var franko = members.Single(m => m.ParticipantId == world.Franko);
            Assert.Equal(
                new CourseCardMemberRecord(
                    world.Franko, "Франко", "Іван", Cc.FrankoEmail, ClassroomRole.Teacher, false, Cc.FrankoLastSeen, false),
                franko with { LastSeenAt = franko.LastSeenAt.ToUniversalTime() });
            Assert.DoesNotContain(members, m => m.Surname is "Коваль" or "Бондаренко");

            var chemistry = await source.GetMembersAsync(world.Chemistry, ct);
            Assert.Equal(["Бондаренко", "Коваль"], chemistry.Select(m => m.Surname));
        });
    }

    /// <summary>
    /// AC-006, AC-015, FR-007, I-3: the label in the Ukrainian alphabet (Г before Ґ, І after И, Іва before Ігн),
    /// Cyrillic before Latin, the email standing in for a missing name, nameless rows last by id.
    /// </summary>
    [Fact]
    public async Task TheMembers_AreInTheUkrainianOrder_NamelessLast()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var course = await host.InsertEmptyCourseAsync(1, "Курс", ct);
        var nameless2 = await host.InsertPersonAsync(1, null, null, null, ct);
        var smith = await host.InsertPersonAsync(2, "Smith", "John", null, ct);
        var ivanenko = await host.InsertPersonAsync(3, "Іваненко", "Олег", null, ct);
        var nameless1 = await host.InsertPersonAsync(4, null, null, null, ct);
        var ihnatenko = await host.InsertPersonAsync(5, "Ігнатенко", "Анна", null, ct);
        var email = await host.InsertPersonAsync(6, null, null, "a.zz@school.example", ct);
        var hnatiuk = await host.InsertPersonAsync(7, "Гнатюк", "Ірина", null, ct);
        var gudz = await host.InsertPersonAsync(8, "Ґудзь", "Петро", null, ct);
        var andriienko = await host.InsertPersonAsync(9, "Андрієнко", "Марія", null, ct);
        foreach (var person in new[] { nameless2, smith, ivanenko, nameless1, ihnatenko, email, hnatiuk, gudz, andriienko })
        {
            await host.AddStudentAsync(course, person, ct);
        }

        await host.WithCourseCardSourceAsync(async source =>
        {
            var members = await source.GetMembersAsync(course, ct);

            Assert.Equal(
                [andriienko, hnatiuk, gudz, ivanenko, ihnatenko, email, smith, Math.Min(nameless1, nameless2), Math.Max(nameless1, nameless2)],
                members.Select(m => m.ParticipantId));
        });
    }

    /// <summary>FR-007: equal labels are ordered by the email, a row without email first, then by participant id.</summary>
    [Fact]
    public async Task EqualNames_AreOrderedByEmail_NoneFirst_ThenById()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var course = await host.InsertEmptyCourseAsync(1, "Курс", ct);
        var withB = await host.InsertPersonAsync(1, "Бондар", "Анна", "b.bondar@school.example", ct);
        var secondWithout = await host.InsertPersonAsync(2, "Бондар", "Анна", null, ct);
        var withA = await host.InsertPersonAsync(3, "Бондар", "Анна", "a.bondar@school.example", ct);
        var firstWithout = await host.InsertPersonAsync(4, "Бондар", "Анна", null, ct);
        foreach (var person in new[] { withB, secondWithout, withA, firstWithout })
        {
            await host.AddStudentAsync(course, person, ct);
        }

        await host.WithCourseCardSourceAsync(async source =>
        {
            var members = await source.GetMembersAsync(course, ct);

            Assert.Equal(
                [Math.Min(secondWithout, firstWithout), Math.Max(secondWithout, firstWithout), withA, withB],
                members.Select(m => m.ParticipantId));
        });
    }

    /// <summary>
    /// AC-003, FR-004, db-design §2: is_owner is true only for the participant whose Google id equals the course's
    /// owner id, whatever their membership; the owner of another course is not the owner here.
    /// </summary>
    [Fact]
    public async Task TheOwner_IsTheParticipantWithTheCoursesOwnerId()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var world = await host.SeedCourseCardAsync(ct);

        await host.WithCourseCardSourceAsync(async source =>
        {
            var members = await source.GetMembersAsync(world.Physics, ct);

            Assert.Equal([world.Shevchenko], members.Where(m => m.IsOwner).Select(m => m.ParticipantId));
            Assert.All(await source.GetMembersAsync(world.Chemistry, ct), m => Assert.False(m.IsOwner));
        });
    }

    /// <summary>AC-003, FR-004: a course whose owner id is null, or names nobody in the roster, marks no one.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("199999999999999999")]
    public async Task ACourseWhoseOwnerIsNotInTheRoster_MarksNoOne(string? ownerGoogleId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var course = await CourseRows.InsertCourseAsync(
            host, ct, googleId: CourseTestData.CourseId(1), name: "Курс", ownerGoogleId: ownerGoogleId);
        await host.AddTeacherAsync(course, await host.InsertPersonAsync(1, "Шевченко", "Тарас", null, ct), ct);
        await host.AddStudentAsync(course, await host.InsertPersonAsync(2, "Мельник", "Петро", null, ct), ct);

        await host.WithCourseCardSourceAsync(async source =>
        {
            var members = await source.GetMembersAsync(course, ct);

            Assert.Equal(2, members.Count);
            Assert.All(members, m => Assert.False(m.IsOwner));
        });
    }

    /// <summary>AC-004, FR-005, FR-006: a student created from submissions comes back off the roster, nameless, with its instant.</summary>
    [Fact]
    public async Task ASubmitterOnlyStudent_ComesBackOffTheRoster_WithItsLastSeen()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var world = await host.SeedCourseCardAsync(ct);

        await host.WithCourseCardSourceAsync(async source =>
        {
            var submitter = (await source.GetMembersAsync(world.Physics, ct)).Single(m => m.ParticipantId == world.Submitter);

            Assert.Equal(
                new CourseCardMemberRecord(world.Submitter, null, null, null, ClassroomRole.Student, false, Cc.SubmitterSeen, false),
                submitter with { LastSeenAt = submitter.LastSeenAt.ToUniversalTime() });
        });
    }

    /// <summary>
    /// AC-017, FR-015, db-design §8: each read is one database command, for a course with 3 members and for one with 300
    /// members and 50 leavers alike; an unknown id is one command.
    /// </summary>
    [Fact]
    public async Task EachRead_IsOneDatabaseCommand_WhateverTheRosterSize()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartEmptyAsync(ct);
        var small = await host.InsertEmptyCourseAsync(1, "Малий", ct);
        var large = await host.InsertEmptyCourseAsync(2, "Великий", ct);
        for (var i = 1; i <= 3; i++)
        {
            await host.AddStudentAsync(small, await host.InsertPersonAsync(i, $"Учень{i:D3}", "Ім'я", null, ct), ct);
        }

        await host.ExecuteAsync(
            """
            INSERT INTO classroom_participant (google_user_id, email, full_name, surname, given_name, created_at, updated_at)
            SELECT '2' || lpad(g::text, 17, '0'), NULL, NULL, 'Учень' || g, 'Ім''я', now(), now()
            FROM generate_series(1, 350) AS g;
            INSERT INTO course_membership (course_id, participant_id, role, first_seen_at, last_seen_at, on_roster, created_at, updated_at)
            SELECT @course, p.id, 'student', now(), now(), (row_number() OVER (ORDER BY p.id)) <= 300, now(), now()
            FROM classroom_participant p
            WHERE p.google_user_id LIKE '2%';
            """,
            ct,
            ("course", large));

        var counter = new CommandCounter();
        var options = ClassroomAgentDbContextOptions
            .Configure(new DbContextOptionsBuilder<ClassroomAgentDbContext>(), host.ConnectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .AddInterceptors(counter)
            .Options;
        await using var db = new ClassroomAgentDbContext((DbContextOptions<ClassroomAgentDbContext>)options);
        var source = new CourseCardSource(db);

        var commands = new List<int>();
        async Task Count(Func<Task> read)
        {
            var before = counter.Count;
            await read();
            commands.Add(counter.Count - before);
        }

        await Count(() => source.GetCourseAsync(small, ct));
        await Count(() => source.GetMembersAsync(small, ct));
        await Count(() => source.GetCourseAsync(large, ct));
        await Count(async () => Assert.Equal(350, (await source.GetMembersAsync(large, ct)).Count));
        await Count(() => source.GetCourseAsync(large + 1000, ct));

        Assert.Equal([1, 1, 1, 1, 1], commands);
    }

    /// <summary>FR-014, FR-015, S-04, S-11: reading writes nothing.</summary>
    [Fact]
    public async Task Reading_WritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var world = await host.SeedCourseCardAsync(ct);
        var before = await host.CourseListFingerprintAsync(ct);

        await host.WithCourseCardSourceAsync(async source =>
        {
            await source.GetCourseAsync(world.Physics, ct);
            await source.GetMembersAsync(world.Physics, ct);
            await source.GetCourseAsync(world.Chemistry + 1000, ct);
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
