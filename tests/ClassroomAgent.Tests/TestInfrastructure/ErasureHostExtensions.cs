using System.Net;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Cl = ClassroomAgent.Tests.TestInfrastructure.CourseListHostExtensions;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-038 host seeding and driving: a school inserted by SQL through the US-014, US-015, US-020 and US-032 helpers, so the
/// tests do not depend on the code under test (TC-2); the use cases and queries resolved from the host's own container,
/// as a request would resolve them; and the HTTP journeys of the five operations. Every name, email and Google id is
/// invented (TC-4).
/// </summary>
public static class ErasureHostExtensions
{
    /// <summary>The person to erase (spec AC-001): a student in two courses.</summary>
    public const string TargetSurname = "Коваль";

    public const string TargetGivenName = "Олена";

    public const string TargetFullName = TargetGivenName + " " + TargetSurname;

    public static readonly string TargetEmail = CourseTestData.Email("o.koval");

    /// <summary>The same address as Google may return it in another letter case (spec AC-012, VR-004).</summary>
    public static readonly string TargetEmailOtherCase = "O.Koval@" + InstallationTestData.Domain.ToUpperInvariant();

    public static readonly string TargetGoogleUserId = CourseTestData.UserId(1);

    /// <summary>Another student of the first course: nothing of theirs may change (spec AC-002).</summary>
    public const string OtherFullName = "Петро Мельник";

    public static readonly string OtherEmail = CourseTestData.Email("p.melnyk");

    public static readonly string OtherGoogleUserId = CourseTestData.UserId(2);

    /// <summary>The teacher of the first course.</summary>
    public const string TeacherFullName = "Тарас Шевченко";

    public static readonly string TeacherEmail = CourseTestData.Email("t.shevchenko");

    public static readonly string TeacherGoogleUserId = CourseTestData.UserId(3);

    public const string PhysicsName = "Фізика 9-А";

    public const string ChemistryName = "Хімія 9-А";

    public const string HistoryName = "Історія 9-А";

    /// <summary>Every personal value of the target — none may reach an audit row or a log line (spec AC-009, S-06).</summary>
    public static IReadOnlyList<string> TargetPersonalValues { get; } =
        [TargetSurname, TargetGivenName, "o.koval", "O.Koval", CourseTestData.UserId(1)];

    /// <summary>What <see cref="SeedErasureWorldAsync"/> yields for the target (strategy §3.1).</summary>
    public static class Expected
    {
        public const int Courses = 2;
        public const int Submissions = 3;
        public const int Grades = 2;
        public const int MeetParticipations = 3;
        public const int OrganizedMeetings = 1;
        public const int OwnedCourses = 1;

        public const int ErasedMemberships = 2;
        public const int ErasedSubmissions = 3;
        public const int ErasedMeetParticipations = 3;
        public const int ClearedMeetOrganizers = 1;
        public const int ClearedCourseOwners = 1;

        public const int OtherMeetParticipations = 2;
    }

    /// <summary>The ids the world holds.</summary>
    public sealed record ErasureWorld(
        long Target,
        long Other,
        long Teacher,
        long Physics,
        long Chemistry,
        long History,
        long MeetingByTeacher,
        long MeetingByTarget,
        long MeetingByOther,
        long OtherSubmission);

    /// <summary>
    /// Courses: Фізика (owner the teacher), Хімія (owner the target), Історія (owner the other student). The target is a
    /// student in Фізика and Хімія with three submissions — an assigned grade, a draft grade, no grade; the other student
    /// is in Фізика with one graded submission; the teacher teaches Фізика. Meetings: one by the teacher with the target
    /// connected twice (once in another letter case), the other student and the teacher; one organized by the target
    /// (organizer stored in another letter case) with the target and the other student; one by the other student with
    /// the teacher.
    /// </summary>
    public static async Task<ErasureWorld> SeedErasureWorldAsync(this InstallationTestHost host, CancellationToken cancellationToken)
    {
        // The retention purge runs at every host start (US-037) and deletes a participant that has no membership yet.
        await host.WaitForPurgeRunsAsync(1, cancellationToken);

        var physics = await CourseRows.InsertCourseAsync(
            host, cancellationToken, googleId: CourseTestData.CourseId(1), name: PhysicsName, ownerGoogleId: TeacherGoogleUserId);
        var chemistry = await CourseRows.InsertCourseAsync(
            host, cancellationToken, googleId: CourseTestData.CourseId(2), name: ChemistryName, ownerGoogleId: TargetGoogleUserId);
        var history = await CourseRows.InsertCourseAsync(
            host, cancellationToken, googleId: CourseTestData.CourseId(3), name: HistoryName, ownerGoogleId: OtherGoogleUserId);

        var target = await host.InsertPersonAsync(1, TargetSurname, TargetGivenName, TargetEmail, cancellationToken);
        var other = await host.InsertPersonAsync(2, "Мельник", "Петро", OtherEmail, cancellationToken);
        var teacher = await host.InsertPersonAsync(3, "Шевченко", "Тарас", TeacherEmail, cancellationToken);

        await Cl.AddStudentAsync(host, physics, target, cancellationToken);
        await Cl.AddStudentAsync(host, chemistry, target, cancellationToken);
        await Cl.AddStudentAsync(host, physics, other, cancellationToken);
        await Cl.AddTeacherAsync(host, physics, teacher, cancellationToken);

        var physicsItem = await CourseWorkRows.InsertCourseWorkAsync(
            host, physics, cancellationToken, googleId: CourseWorkTestData.ItemId(1), title: CourseWorkTestData.Title(1));
        var chemistryItem = await CourseWorkRows.InsertCourseWorkAsync(
            host, chemistry, cancellationToken, googleId: CourseWorkTestData.ItemId(2), title: CourseWorkTestData.Title(2));

        await CourseWorkRows.InsertSubmissionAsync(
            host, physicsItem, target, cancellationToken, googleId: CourseWorkTestData.SubmissionId(1), assignedGrade: 10m);
        await CourseWorkRows.InsertSubmissionAsync(
            host, chemistryItem, target, cancellationToken, googleId: CourseWorkTestData.SubmissionId(2), draftGrade: 7m);
        await CourseWorkRows.InsertSubmissionAsync(
            host, chemistryItem, target, cancellationToken, googleId: CourseWorkTestData.SubmissionId(3));
        var otherSubmission = await CourseWorkRows.InsertSubmissionAsync(
            host, physicsItem, other, cancellationToken, googleId: CourseWorkTestData.SubmissionId(4), assignedGrade: 8m);

        var at = new DateTimeOffset(2026, 9, 7, 8, 0, 0, TimeSpan.Zero);
        var byTeacher = await Mc.SeedMeetingAsync(
            host, MeetTestData.MeetingCode(1), TeacherEmail, at, cancellationToken,
            [TargetEmail, TargetEmailOtherCase, OtherEmail, TeacherEmail]);
        var byTarget = await Mc.SeedMeetingAsync(
            host, MeetTestData.MeetingCode(2), TargetEmail.ToUpperInvariant(), at.AddDays(1), cancellationToken,
            [TargetEmail, OtherEmail]);
        var byOther = await Mc.SeedMeetingAsync(
            host, MeetTestData.MeetingCode(3), OtherEmail, at.AddDays(2), cancellationToken,
            [TeacherEmail]);

        return new ErasureWorld(target, other, teacher, physics, chemistry, history, byTeacher, byTarget, byOther, otherSubmission);
    }

    /// <summary>An Admin account row, as the Google sign-in would have created it; returns its id.</summary>
    public static async Task<long> InsertAdminAsync(this InstallationTestHost host, CancellationToken cancellationToken)
    {
        await host.InsertAppUserAsync(cancellationToken);
        return await host.ScalarAsync<long>(
            "SELECT id FROM app_user WHERE normalized_email = @email",
            cancellationToken,
            ("email", SignInTestData.AdminEmail.ToLowerInvariant()));
    }

    /// <summary>Runs the erasure use case resolved from the host's container, as one request would.</summary>
    public static async Task<ParticipantErasureResult> EraseAsync(
        this InstallationTestHost host,
        long adminId,
        long participantId,
        CancellationToken cancellationToken,
        bool irreversibleConfirmed = true)
    {
        using var scope = host.CreateScope();
        var useCase = scope.ServiceProvider.GetRequiredService<EraseParticipantUseCase>();
        return await useCase.ExecuteAsync(adminId, participantId, irreversibleConfirmed, null, cancellationToken);
    }

    /// <summary>Runs the lifting use case resolved from the host's container.</summary>
    public static async Task<ExclusionLiftingResult> LiftAsync(
        this InstallationTestHost host,
        long adminId,
        string? email,
        CancellationToken cancellationToken)
    {
        using var scope = host.CreateScope();
        var useCase = scope.ServiceProvider.GetRequiredService<LiftErasureExclusionUseCase>();
        return await useCase.ExecuteAsync(adminId, email, null, cancellationToken);
    }

    /// <summary>Runs the search query resolved from the host's container, over the real read source.</summary>
    public static async Task<SearchBlock> SearchAsync(this InstallationTestHost host, string? term, CancellationToken cancellationToken)
    {
        using var scope = host.CreateScope();
        var query = scope.ServiceProvider.GetRequiredService<SearchParticipantsForErasureQuery>();
        return await query.ExecuteAsync(term, cancellationToken);
    }

    /// <summary>Runs the confirmation query resolved from the host's container, over the real read source.</summary>
    public static async Task<ErasureConfirmationPageModel?> ConfirmationAsync(
        this InstallationTestHost host,
        long participantId,
        CancellationToken cancellationToken)
    {
        using var scope = host.CreateScope();
        var query = scope.ServiceProvider.GetRequiredService<GetErasureConfirmationQuery>();
        return await query.ExecuteAsync(participantId, cancellationToken);
    }

    /// <summary>
    /// Seeds an exclusion by SQL — the known salt (<see cref="ErasureTestData.KnownSalt"/>) first when none is stored —
    /// with the fingerprint of <paramref name="email"/>, or none; returns its id. Used where a test needs an exclusion
    /// without going through the erasure under test.
    /// </summary>
    public static async Task<long> SeedExclusionAsync(
        this InstallationTestHost host,
        string googleUserId,
        string? email,
        CancellationToken cancellationToken,
        DateTimeOffset? at = null)
    {
        // US-065: an exclusion can no longer be aged by an UPDATE (tg_erased_participant_fill_only), so an old one is
        // inserted with its age.
        var stamp = at ?? host.Time.GetUtcNow();
        await host.ExecuteAsync(
            """
            INSERT INTO erasure_salt (singleton, salt, created_at, updated_at)
            VALUES (true, @salt, @stamp, @stamp)
            ON CONFLICT (singleton) DO NOTHING
            """,
            cancellationToken,
            ("salt", ErasureTestData.KnownSalt()),
            ("stamp", stamp));
        var salt = await host.ScalarAsync<byte[]>("SELECT salt FROM erasure_salt", cancellationToken);
        return await host.ScalarAsync<long>(
            """
            INSERT INTO erased_participant (google_user_id, email_fingerprint, erased_at, created_at, updated_at)
            VALUES (@googleUserId, @fingerprint, @stamp, @stamp, @stamp)
            RETURNING id
            """,
            cancellationToken,
            ("googleUserId", googleUserId),
            ("fingerprint", email is null ? null : ErasureTestData.Fingerprint(email, salt!)),
            ("stamp", stamp));
    }

    /// <summary>
    /// US-065: seeds by SQL an exclusion entered in advance — the known salt first when none is stored, the fingerprint of
    /// <paramref name="email"/>, no Google user id — without going through the entry under test; returns its id.
    /// </summary>
    public static async Task<long> SeedExclusionInAdvanceAsync(
        this InstallationTestHost host,
        string email,
        CancellationToken cancellationToken)
    {
        var stamp = host.Time.GetUtcNow();
        await host.ExecuteAsync(
            """
            INSERT INTO erasure_salt (singleton, salt, created_at, updated_at)
            VALUES (true, @salt, @stamp, @stamp)
            ON CONFLICT (singleton) DO NOTHING
            """,
            cancellationToken,
            ("salt", ErasureTestData.KnownSalt()),
            ("stamp", stamp));
        var salt = await host.ScalarAsync<byte[]>("SELECT salt FROM erasure_salt", cancellationToken);
        return await host.ScalarAsync<long>(
            """
            INSERT INTO erased_participant (google_user_id, email_fingerprint, erased_at, created_at, updated_at)
            VALUES (NULL, @fingerprint, @stamp, @stamp, @stamp)
            RETURNING id
            """,
            cancellationToken,
            ("fingerprint", ErasureTestData.Fingerprint(email, salt!)),
            ("stamp", stamp));
    }

    /// <summary>US-065: runs the entry use case resolved from the host's container, as one request would.</summary>
    public static async Task<ExclusionEntryResult> EnterExclusionAsync(
        this InstallationTestHost host,
        long adminId,
        string? email,
        CancellationToken cancellationToken)
    {
        using var scope = host.CreateScope();
        var useCase = scope.ServiceProvider.GetRequiredService<EnterErasureExclusionUseCase>();
        return await useCase.ExecuteAsync(adminId, email, null, cancellationToken);
    }

    /// <summary>A stored exclusion, every column; the Google user id is null for an exclusion entered in advance (US-065).</summary>
    public sealed record ExclusionRow(
        long Id,
        string? GoogleUserId,
        byte[]? EmailFingerprint,
        DateTimeOffset ErasedAt,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    public static Task<IReadOnlyList<ExclusionRow>> ExclusionRowsAsync(this InstallationTestHost host, CancellationToken cancellationToken) =>
        host.QueryAsync(
            "SELECT id, google_user_id, email_fingerprint, erased_at, created_at, updated_at FROM erased_participant ORDER BY id",
            r => new ExclusionRow(
                r.GetInt64(0),
                r.IsDBNull(1) ? null : r.GetString(1),
                r.IsDBNull(2) ? null : r.GetFieldValue<byte[]>(2),
                r.GetFieldValue<DateTimeOffset>(3),
                r.GetFieldValue<DateTimeOffset>(4),
                r.GetFieldValue<DateTimeOffset>(5)),
            cancellationToken);

    /// <summary>Every column name of a table, for the "no name, no email" checks (spec AC-006).</summary>
    public static Task<IReadOnlyList<string>> ColumnNamesAsync(this InstallationTestHost host, string table, CancellationToken cancellationToken) =>
        host.QueryAsync(
            "SELECT column_name FROM information_schema.columns WHERE table_name = @table ORDER BY ordinal_position",
            r => r.GetString(0),
            cancellationToken,
            ("table", table));

    /// <summary>The stored salt bytes, every row.</summary>
    public static Task<IReadOnlyList<byte[]>> SaltRowsAsync(this InstallationTestHost host, CancellationToken cancellationToken) =>
        host.QueryAsync("SELECT salt FROM erasure_salt", r => r.GetFieldValue<byte[]>(0), cancellationToken);

    /// <summary>A US-038 audit row with its erasure counts (db-design §9).</summary>
    public sealed record ErasureAuditRow(
        string Action,
        string ActorType,
        long? ActorId,
        string? ActorRole,
        string? TargetType,
        long? TargetId,
        string Outcome,
        string? RefusalCategory,
        int? ErasedMemberships,
        int? ErasedSubmissions,
        int? ErasedMeetParticipations,
        int? ClearedMeetOrganizers,
        int? ClearedCourseOwners);

    /// <summary>The audit rows of the two US-038 actions and the US-065 entry, in order.</summary>
    public static Task<IReadOnlyList<ErasureAuditRow>> ErasureAuditRowsAsync(this InstallationTestHost host, CancellationToken cancellationToken) =>
        host.QueryAsync(
            """
            SELECT action, actor_type, actor_id, actor_role, target_type, target_id, outcome, refusal_category,
                   erased_memberships, erased_submissions, erased_meet_participations, cleared_meet_organizers,
                   cleared_course_owners
            FROM audit_event
            WHERE action IN ('participant_erased', 'erasure_exclusion_lifted', 'erasure_exclusion_entered')
            ORDER BY id
            """,
            r => new ErasureAuditRow(
                r.GetString(0),
                r.GetString(1),
                r.IsDBNull(2) ? null : r.GetInt64(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetInt64(5),
                r.GetString(6),
                r.IsDBNull(7) ? null : r.GetString(7),
                r.IsDBNull(8) ? null : r.GetInt32(8),
                r.IsDBNull(9) ? null : r.GetInt32(9),
                r.IsDBNull(10) ? null : r.GetInt32(10),
                r.IsDBNull(11) ? null : r.GetInt32(11),
                r.IsDBNull(12) ? null : r.GetInt32(12)),
            cancellationToken);

    /// <summary>
    /// A fingerprint of every table an erasure or a lifting could write (spec AC-003, AC-008, AC-015): each row by row, a
    /// table that does not exist yet as "absent", and the audit count (the purge's rows excluded).
    /// </summary>
    public static async Task<string> ErasureFingerprintAsync(this InstallationTestHost host, CancellationToken cancellationToken)
    {
        var parts = new List<string>();
        foreach (var table in new[]
                 {
                     "course", "classroom_participant", "course_membership", "course_work", "submission", "meet_session",
                     "meet_participation", "erased_participant", "erasure_salt",
                 })
        {
            var exists = await host.ScalarAsync<bool>("SELECT to_regclass(@table) IS NOT NULL", cancellationToken, ("table", table));
            var digest = exists
                ? await host.ScalarAsync<string>(
                    $"SELECT coalesce(md5(string_agg(row_to_json(t)::text, '|' ORDER BY t.id)), '-') FROM {table} t",
                    cancellationToken)
                : "absent";
            parts.Add(table + "=" + digest);
        }

        parts.Add("audit=" + await host.ScalarAsync<long>(
            "SELECT count(*) FROM audit_event WHERE action <> 'retention_purge_run'", cancellationToken));
        return string.Join(';', parts);
    }

    /// <summary>A started installation in the given legitimacy state and UI language, with the actor signed in.</summary>
    public static async Task<(InstallationTestHost Host, FormClient Client)> StartAsync(
        PostgreSqlFixture database,
        Actor actor,
        CancellationToken cancellationToken,
        ReadOnlyModeHost.Cause cause = ReadOnlyModeHost.Cause.NotReadOnly,
        string language = UiLanguageTestData.Ukrainian) =>
        language == UiLanguageTestData.Ukrainian
            ? await JournalHostExtensions.StartAsync(database, actor, cancellationToken, cause)
            : await MeetReportsHostExtensions.StartAsync(database, actor, language, cancellationToken, cause);

    /// <summary>
    /// Opens <paramref name="fromPath"/> for its antiforgery token — the landing page while the page does not exist yet,
    /// so a red-phase POST fails on the behaviour under test, not on the fixture (the US-009 pattern) — and posts.
    /// </summary>
    public static async Task<PageResponse> PostFromAsync(
        this FormClient client,
        string fromPath,
        string path,
        IReadOnlyList<KeyValuePair<string, string>> fields,
        CancellationToken cancellationToken,
        bool withToken = true)
    {
        var page = await client.GetAsync(fromPath, cancellationToken);
        if (withToken && (page.Status != HttpStatusCode.OK || !Html.HasInput(page.Body, Html.AntiforgeryFieldName)))
        {
            await client.GetAsync(SignInTestData.LandingPath, cancellationToken);
        }

        return await client.PostFormAsync(path, fields, cancellationToken, withToken);
    }

    /// <summary>Posts the search form from the erasure page.</summary>
    public static Task<PageResponse> PostSearchAsync(
        this FormClient client,
        string term,
        CancellationToken cancellationToken,
        bool withToken = true) =>
        client.PostFromAsync(
            ErasureTestData.PagePath,
            ErasureTestData.SearchesPath,
            [new(ErasureTestData.Fields.Term, term)],
            cancellationToken,
            withToken);

    /// <summary>Posts the erasure from the participant's confirmation page; <paramref name="mark"/> null sends no mark.</summary>
    public static Task<PageResponse> PostErasureAsync(
        this FormClient client,
        long participantId,
        string? mark,
        CancellationToken cancellationToken,
        bool withToken = true) =>
        client.PostFromAsync(
            ErasureTestData.ConfirmationPath(participantId),
            ErasureTestData.ErasurePath(participantId),
            mark is null ? [] : [new(ErasureTestData.Fields.Mark, mark)],
            cancellationToken,
            withToken);

    /// <summary>US-065: posts the "Exclude by email" form from the erasure page.</summary>
    public static Task<PageResponse> PostEntryAsync(
        this FormClient client,
        string email,
        CancellationToken cancellationToken,
        bool withToken = true) =>
        client.PostFromAsync(
            ErasureTestData.PagePath,
            ErasureTestData.EntriesPath,
            [new(ErasureTestData.Fields.Email, email)],
            cancellationToken,
            withToken);

    /// <summary>Posts the "Lift an exclusion" form from the erasure page.</summary>
    public static Task<PageResponse> PostLiftingAsync(
        this FormClient client,
        string email,
        CancellationToken cancellationToken,
        bool withToken = true) =>
        client.PostFromAsync(
            ErasureTestData.PagePath,
            ErasureTestData.LiftingsPath,
            [new(ErasureTestData.Fields.Email, email)],
            cancellationToken,
            withToken);
}
