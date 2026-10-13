using ClassroomAgent.Application.Ports;
using ClassroomAgent.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Cl = ClassroomAgent.Tests.TestInfrastructure.CourseListHostExtensions;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-024 host seeding: a school inserted by SQL through the US-020, US-015 and US-032 helpers, so the tests do not
/// depend on the code under test (TC-2), and a <c>sync_state</c> row with every column the page reads. Every name,
/// email and code is invented (TC-4).
/// </summary>
public static class DatabaseStatisticsHostExtensions
{
    /// <summary>A code with two stored meetings and no link row — counted once (AC-018).</summary>
    public const string TwiceMetCode = "aaa-bbbb-ccc";

    /// <summary>A code with one stored meeting and no link row.</summary>
    public const string OnceMetCode = "ddd-eeee-fff";

    /// <summary>A code linked to a course automatically — not awaiting a decision.</summary>
    public const string AutoLinkedCode = "ggg-hhhh-iii";

    /// <summary>A code linked to a course by a person — not awaiting a decision.</summary>
    public const string PersonLinkedCode = "mmm-nnnn-ooo";

    /// <summary>A code marked "not a course" — not awaiting a decision.</summary>
    public const string MarkedCode = "jjj-kkkk-lll";

    /// <summary>The diagnosis text a failed row stores (AC-013): a declared name, so the US-017 page would show it.</summary>
    public const string StoredDiagnosis = "TechnicalAccountCannotRead";

    /// <summary>Every course name, person name, email and meeting code the world holds — none may reach the page or a log.</summary>
    /// <remarks>
    /// "Архів " keeps its trailing space: the archived courses are named "Архів 1" … "Архів 4", and without the space the
    /// marker would match the reused status label "Архівний" the page must show (spec FR-012). Changed at IMPLEMENTATION
    /// with the Owner's approval, 2026-10-13.
    /// </remarks>
    public static IReadOnlyList<string> SensitiveValues { get; } =
    [
        "Фізика", "Архів ", "Шевченко", "Українка", "Коваль", "Франко", "Мельник", "Бондар",
        "t.shevchenko@", "l.ukrainka@", "o.koval@", "i.franko@", "p.melnyk@", "a.bondar@",
        TwiceMetCode, OnceMetCode, AutoLinkedCode, PersonLinkedCode, MarkedCode,
    ];

    /// <summary>The counters <see cref="SeedStatisticsWorldAsync"/> yields (strategy §3.1).</summary>
    public static class Expected
    {
        public const int Active = 5;
        public const int Archived = 4;
        public const int Provisioned = 3;
        public const int Declined = 2;
        public const int Suspended = 1;
        public const int Total = 15;
        public const int Teachers = 4;
        public const int Students = 3;
        public const int CourseWorkItems = 5;
        public const int Submissions = 7;
        public const int MeetMeetings = 6;
        public const int MeetCodesAwaitingDecision = 2;
    }

    /// <summary>The ids the world holds.</summary>
    public sealed record StatisticsWorld(
        long FirstActiveCourse,
        long FirstArchivedCourse,
        long Teacher,
        long Student,
        long TeacherAndStudent,
        long Leaver,
        long TwoTeacherOneStudent,
        long ArchivedOnlyTeacher);

    /// <summary>
    /// Courses: 5 active, 4 archived, 3 provisioned, 2 declined, 1 suspended (AC-002). People (AC-003, AC-015): Шевченко
    /// teaches active 1; Мельник studies active 1; Українка teaches active 2 and studies active 1; Франко left the
    /// rosters of active 1 (teacher) and active 2 (student); Коваль teaches active 1 and archived 1 and studies active 3;
    /// Бондар teaches only archived 1; a participant with no membership. Teachers = Шевченко, Українка, Коваль, Бондар (4);
    /// students = Мельник, Українка, Коваль (3). Coursework: 3 in active 1, 2 in archived 1 (I-4) = 5. Submissions: 7, one
    /// of them by Франко, who left (I-4). Meetings: <see cref="TwiceMetCode"/> ×2, <see cref="OnceMetCode"/>,
    /// <see cref="AutoLinkedCode"/>, <see cref="PersonLinkedCode"/>, <see cref="MarkedCode"/> = 6; awaiting a decision: 2.
    /// </summary>
    public static async Task<StatisticsWorld> SeedStatisticsWorldAsync(this InstallationTestHost host, CancellationToken cancellationToken)
    {
        // The retention purge runs at every host start (US-037) and deletes a participant that has no membership yet.
        await host.WaitForPurgeRunsAsync(1, cancellationToken);

        var ordinal = 0;
        async Task<long> Course(string name, string state) => await host.InsertCourseAsync(++ordinal, name, cancellationToken, state);

        var active = new List<long>();
        for (var i = 1; i <= Expected.Active; i++)
        {
            active.Add(await Course($"Фізика {i}", CourseTestData.States.Active));
        }

        var archived = new List<long>();
        for (var i = 1; i <= Expected.Archived; i++)
        {
            archived.Add(await Course($"Архів {i}", CourseTestData.States.Archived));
        }

        for (var i = 1; i <= Expected.Provisioned; i++)
        {
            await Course($"Новий {i}", CourseTestData.States.Provisioned);
        }

        for (var i = 1; i <= Expected.Declined; i++)
        {
            await Course($"Відхилений {i}", CourseTestData.States.Declined);
        }

        for (var i = 1; i <= Expected.Suspended; i++)
        {
            await Course($"Призупинений {i}", CourseTestData.States.Suspended);
        }

        var teacher = await host.InsertPersonAsync(1, "Шевченко", "Тарас", CourseTestData.Email("t.shevchenko"), cancellationToken);
        var student = await host.InsertPersonAsync(2, "Мельник", "Петро", CourseTestData.Email("p.melnyk"), cancellationToken);
        var both = await host.InsertPersonAsync(3, "Українка", "Леся", CourseTestData.Email("l.ukrainka"), cancellationToken);
        var leaver = await host.InsertPersonAsync(4, "Франко", "Іван", CourseTestData.Email("i.franko"), cancellationToken);
        var many = await host.InsertPersonAsync(5, "Коваль", "Олена", CourseTestData.Email("o.koval"), cancellationToken);
        var archivedOnly = await host.InsertPersonAsync(6, "Бондар", "Анна", CourseTestData.Email("a.bondar"), cancellationToken);

        await Cl.AddTeacherAsync(host, active[0], teacher, cancellationToken);
        await Cl.AddStudentAsync(host, active[0], student, cancellationToken);
        await Cl.AddTeacherAsync(host, active[1], both, cancellationToken);
        await Cl.AddStudentAsync(host, active[0], both, cancellationToken);
        await Cl.AddTeacherAsync(host, active[0], leaver, cancellationToken, onRoster: false);
        await Cl.AddStudentAsync(host, active[1], leaver, cancellationToken, onRoster: false);
        await Cl.AddTeacherAsync(host, active[0], many, cancellationToken);
        await Cl.AddTeacherAsync(host, archived[0], many, cancellationToken);
        await Cl.AddStudentAsync(host, active[2], many, cancellationToken);
        await Cl.AddTeacherAsync(host, archived[0], archivedOnly, cancellationToken);

        var items = new List<long>();
        for (var i = 1; i <= 3; i++)
        {
            items.Add(await CourseWorkRows.InsertCourseWorkAsync(
                host, active[0], cancellationToken, googleId: CourseWorkTestData.ItemId(i), title: CourseWorkTestData.Title(i)));
        }

        for (var i = 4; i <= 5; i++)
        {
            items.Add(await CourseWorkRows.InsertCourseWorkAsync(
                host, archived[0], cancellationToken, googleId: CourseWorkTestData.ItemId(i), title: CourseWorkTestData.Title(i)));
        }

        var submitters = new[] { (items[0], student), (items[0], both), (items[0], leaver), (items[1], student), (items[1], both), (items[2], many), (items[3], many) };
        for (var i = 0; i < submitters.Length; i++)
        {
            await CourseWorkRows.InsertSubmissionAsync(
                host, submitters[i].Item1, submitters[i].Item2, cancellationToken, googleId: CourseWorkTestData.SubmissionId(i + 1));
        }

        var at = new DateTimeOffset(2026, 9, 7, 8, 0, 0, TimeSpan.Zero);
        var organizer = CourseTestData.Email("t.shevchenko");
        await Mc.SeedMeetingAsync(host, TwiceMetCode, organizer, at, cancellationToken);
        await Mc.SeedMeetingAsync(host, TwiceMetCode, organizer, at.AddDays(1), cancellationToken);
        await Mc.SeedMeetingAsync(host, OnceMetCode, organizer, at, cancellationToken);
        await Mc.SeedMeetingAsync(host, AutoLinkedCode, organizer, at, cancellationToken);
        await Mc.SeedMeetingAsync(host, PersonLinkedCode, organizer, at, cancellationToken);
        await Mc.SeedMeetingAsync(host, MarkedCode, organizer, at, cancellationToken);
        await Mc.SeedAutoLinkAsync(host, AutoLinkedCode, active[0], at, cancellationToken);
        await Mc.SeedPersonLinkAsync(host, PersonLinkedCode, active[1], 1, at, cancellationToken);
        await Mc.SeedMarkAsync(host, MarkedCode, 1, at, cancellationToken);

        // A link row whose code has no stored meeting: it changes no counter.
        await Mc.SeedMarkAsync(host, "zzz-zzzz-zzz", 1, at, cancellationToken);

        return new StatisticsWorld(active[0], archived[0], teacher, student, both, leaver, many, archivedOnly);
    }

    /// <summary>
    /// <paramref name="courses"/> active courses, each with one current teacher, one current student, one coursework
    /// item, one submission and one meeting of its own unlinked code — the AC-017 sizing case.
    /// </summary>
    public static async Task SeedSizedSchoolAsync(this InstallationTestHost host, int courses, CancellationToken cancellationToken)
    {
        await host.WaitForPurgeRunsAsync(1, cancellationToken);
        var at = new DateTimeOffset(2026, 9, 7, 8, 0, 0, TimeSpan.Zero);
        for (var i = 1; i <= courses; i++)
        {
            var course = await host.InsertCourseAsync(i, $"Курс {i:D3}", cancellationToken);
            var teacher = await host.InsertPersonAsync(2 * i, "Вчитель", $"{i:D3}", null, cancellationToken);
            var student = await host.InsertPersonAsync((2 * i) + 1, "Учень", $"{i:D3}", null, cancellationToken);
            await Cl.AddTeacherAsync(host, course, teacher, cancellationToken);
            await Cl.AddStudentAsync(host, course, student, cancellationToken);
            var item = await CourseWorkRows.InsertCourseWorkAsync(
                host, course, cancellationToken, googleId: CourseWorkTestData.ItemId(i), title: CourseWorkTestData.Title(i));
            await CourseWorkRows.InsertSubmissionAsync(host, item, student, cancellationToken, googleId: CourseWorkTestData.SubmissionId(i));
            await Mc.SeedMeetingAsync(host, MeetTestData.MeetingCode(i), CourseTestData.Email("organizer"), at, cancellationToken);
        }
    }

    /// <summary>
    /// A <c>sync_state</c> row with every column the page reads or must not show (US-024 spec FR-005, FR-006). The
    /// times are UTC instants; a failed row carries <see cref="StoredDiagnosis"/> and the given step.
    /// </summary>
    public static Task SeedSyncStateAsync(
        this InstallationTestHost host,
        string status,
        CancellationToken cancellationToken,
        DateTimeOffset? lastSuccessfulRunAt = null,
        DateTimeOffset? lastFullRunAt = null,
        DateTimeOffset? meetLoadedUpTo = null,
        string? failedStep = null,
        int processedCount = 0)
    {
        var started = new DateTimeOffset(2026, 3, 2, 6, 0, 0, TimeSpan.Zero);
        return host.ExecuteAsync(
            """
            INSERT INTO sync_state (singleton, status, run_id, started_at, finished_at, processed_count, last_error,
                                    last_successful_run_at, last_full_run_at, meet_loaded_up_to, failed_step,
                                    created_at, updated_at)
            VALUES (true, @status, @runId, @startedAt, @finishedAt, @processed, @lastError, @lastSuccess, @lastFull,
                    @meetLoadedUpTo, @failedStep, @stamp, @stamp)
            """,
            cancellationToken,
            ("status", status),
            ("runId", Guid.NewGuid()),
            ("startedAt", started),
            ("finishedAt", status == "running" ? null : started.AddMinutes(3)),
            ("processed", processedCount),
            ("lastError", status == "failed" ? StoredDiagnosis : null),
            ("lastSuccess", lastSuccessfulRunAt),
            ("lastFull", lastFullRunAt),
            ("meetLoadedUpTo", meetLoadedUpTo),
            ("failedStep", failedStep),
            ("stamp", host.Time.GetUtcNow()));
    }

    /// <summary>
    /// A fingerprint of every table the page reads and of the tables it could write (spec FR-008, S-04): each read table
    /// row by row, and the audit and sync-state rows.
    /// </summary>
    public static async Task<string> DatabaseStatisticsFingerprintAsync(this InstallationTestHost host, CancellationToken cancellationToken)
    {
        var parts = new List<string>();
        foreach (var table in new[]
                 {
                     "course", "classroom_participant", "course_membership", "course_work", "submission", "meet_session",
                     "meeting_code_link", "sync_state",
                 })
        {
            var digest = await host.ScalarAsync<string>(
                $"SELECT coalesce(md5(string_agg(row_to_json(t)::text, '|' ORDER BY t.id)), '-') FROM {table} t",
                cancellationToken);
            parts.Add(table + "=" + digest);
        }

        parts.Add("audit=" + await host.ScalarAsync<long>("SELECT count(*) FROM audit_event", cancellationToken));
        return string.Join(';', parts);
    }

    /// <summary>Runs <paramref name="body"/> with the real <see cref="DatabaseStatisticsReadSource"/> over the host's database.</summary>
    public static async Task WithDatabaseStatisticsSourceAsync(this InstallationTestHost host, Func<IDatabaseStatisticsSource, Task> body)
    {
        using var scope = host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClassroomAgentDbContext>();
        await body(new DatabaseStatisticsReadSource(db));
    }

    /// <summary>Runs <paramref name="body"/> with the real <see cref="MeetCodesReadSource"/> over the host's database.</summary>
    public static async Task WithMeetCodesReadSourceAsync(this InstallationTestHost host, Func<IMeetCodesReadSource, Task> body)
    {
        using var scope = host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClassroomAgentDbContext>();
        await body(new MeetCodesReadSource(db));
    }
}
