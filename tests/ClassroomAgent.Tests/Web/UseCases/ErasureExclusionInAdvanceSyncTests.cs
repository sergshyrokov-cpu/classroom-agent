using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureHostExtensions;
using Cl = ClassroomAgent.Tests.TestInfrastructure.CourseListHostExtensions;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-065 AC-003 … AC-005, AC-008, AC-012 … AC-017 and FR-006 … FR-009, FR-011, I-1, I-2: synchronization and the Meet
/// load through the real host, its background service and PostgreSQL, the Google ports substituted (TC-4). An exclusion
/// entered in advance is seeded by SQL (fingerprint keyed with the known salt, no Google user id), so these tests do not
/// depend on the entry under test.
/// </summary>
public sealed class ErasureExclusionInAdvanceSyncTests(PostgreSqlFixture database)
{
    private static readonly string CourseGoogleId = CourseTestData.CourseId(1);

    /// <summary>The address the target has in Google after the Admin renamed it (AC-004).</summary>
    private static readonly string RenamedEmail = CourseTestData.Email("olena.koval.new");

    /// <summary>The address the target was loaded under before Google's one changed (AC-017).</summary>
    private static readonly string OldEmail = CourseTestData.Email("o.koval.old");

    private static readonly DateTimeOffset LeftAt = InstallationTestHost.DefaultStart - TimeSpan.FromDays(1);

    private static RosterEntry TargetEntry => new(TargetGoogleUserId, TargetEmail, TargetFullName);

    private static RosterEntry OtherEntry => new(OtherGoogleUserId, OtherEmail, OtherFullName);

    private static RosterEntry TeacherEntry => new(TeacherGoogleUserId, TeacherEmail, TeacherFullName);

    /// <summary>
    /// One course owned by the teacher: the teacher, the target and the other student; one item with a graded submission
    /// of each student.
    /// </summary>
    private static Action<FakeClassroomReader> SchoolInGoogle(RosterEntry? target = null, string? owner = null) =>
        reader => reader
            .WithCourse(
                CourseGoogleId,
                PhysicsName,
                teachers: [TeacherEntry],
                students: [target ?? TargetEntry, OtherEntry],
                ownerGoogleId: owner ?? TeacherGoogleUserId)
            .WithCourseWork(CourseGoogleId, CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), InstallationTestHost.DefaultStart, maxPoints: 12m)
            .WithSubmission(CourseGoogleId, CourseWorkTestData.ItemId(1), (target ?? TargetEntry).GoogleUserId, CourseWorkTestData.SubmissionId(1), CourseWorkTestData.RawStates.TurnedIn, assignedGrade: 10m)
            .WithSubmission(CourseGoogleId, CourseWorkTestData.ItemId(1), OtherGoogleUserId, CourseWorkTestData.SubmissionId(2), CourseWorkTestData.RawStates.TurnedIn, assignedGrade: 9m);

    private static Task<long> ParticipantsWithAsync(InstallationTestHost host, string googleUserId, CancellationToken ct) =>
        host.ScalarAsync<long>("SELECT count(*) FROM classroom_participant WHERE google_user_id = @id", ct, ("id", googleUserId));

    private static Task<long> MembershipsOfAsync(InstallationTestHost host, string googleUserId, CancellationToken ct) =>
        host.ScalarAsync<long>(
            "SELECT count(*) FROM course_membership m JOIN classroom_participant p ON p.id = m.participant_id WHERE p.google_user_id = @id",
            ct,
            ("id", googleUserId));

    private static Task<long> SubmissionsOfAsync(InstallationTestHost host, string googleUserId, CancellationToken ct) =>
        host.ScalarAsync<long>(
            "SELECT count(*) FROM submission s JOIN classroom_participant p ON p.id = s.participant_id WHERE p.google_user_id = @id",
            ct,
            ("id", googleUserId));

    private static Task<long> ParticipantsWithEmailAsync(InstallationTestHost host, string email, CancellationToken ct) =>
        host.ScalarAsync<long>("SELECT count(*) FROM classroom_participant WHERE lower(email) = lower(@email)", ct, ("email", email));

    private static Task<string?> OwnerAsync(InstallationTestHost host, CancellationToken ct) =>
        host.ScalarAsync<string>("SELECT owner_google_id FROM course WHERE google_id = @id", ct, ("id", CourseGoogleId));

    private static Task<long> SeedInAdvanceAsync(InstallationTestHost host, CancellationToken ct) =>
        host.SeedExclusionInAdvanceAsync(TargetEmail, ct);

    /// <summary>Waits for the next finished run after <paramref name="previous"/>, advancing the clock by the one-minute interval.</summary>
    private static async Task<SyncHostExtensions.SyncStateRow> NextRunAsync(InstallationTestHost host, Guid previous, CancellationToken ct)
    {
        await host.Time.AdvanceWhenDueAsync(TimeSpan.FromMinutes(1), ct);
        await host.Time.WaitUntilAsync(
            () => host.SyncStatesAsync(ct).GetAwaiter().GetResult() is [{ } row] && row.RunId != previous && row.Status != SyncTestData.Status.Running,
            "a further finished synchronization run",
            ct);
        return (await host.SyncStatesAsync(ct))[0];
    }

    /// <summary>Every column equal (the fingerprint by its bytes).</summary>
    private static void AssertSameRow(ExclusionRow expected, ExclusionRow actual)
    {
        Assert.Equal(expected with { EmailFingerprint = null }, actual with { EmailFingerprint = null });
        Assert.Equal(expected.EmailFingerprint, actual.EmailFingerprint);
    }

    private static void AssertCompleted(SyncHostExtensions.SyncStateRow run)
    {
        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        Assert.Null(run.LastError);
    }

    // ---------------------------------------------------------------- the match and the fill (FR-006, FR-007)

    /// <summary>
    /// AC-003, FR-006, FR-007: the first run stores no participant, membership or submission of the student whose email
    /// matches the exclusion entered in advance; everyone else is stored; the exclusion now holds their Google user id and
    /// nothing else changed; the skip is not a failure.
    /// </summary>
    [Fact]
    public async Task TheFirstRun_StoresNothingOfTheExcludedStudent_AndFillsTheExclusionWithTheirGoogleUserId()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct, classroom: SchoolInGoogle(), seed: SeedInAdvanceAsync);
        var before = Assert.Single(await host.ExclusionRowsAsync(ct));

        var run = await host.WaitForFinishedRunAsync(ct);

        AssertCompleted(run);
        Assert.Equal(0, await ParticipantsWithAsync(host, TargetGoogleUserId, ct));
        Assert.Equal(0, await ParticipantsWithEmailAsync(host, TargetEmail, ct));
        Assert.Equal(0, await SubmissionsOfAsync(host, TargetGoogleUserId, ct));
        Assert.Equal(1, await MembershipsOfAsync(host, OtherGoogleUserId, ct));
        Assert.Equal(1, await SubmissionsOfAsync(host, OtherGoogleUserId, ct));
        Assert.Equal(1, await MembershipsOfAsync(host, TeacherGoogleUserId, ct));
        Assert.Equal(TeacherGoogleUserId, await OwnerAsync(host, ct));
        var after = Assert.Single(await host.ExclusionRowsAsync(ct));
        Assert.Equal(TargetGoogleUserId, after.GoogleUserId);
        Assert.Equal(before.Id, after.Id);
        Assert.Equal(before.EmailFingerprint, after.EmailFingerprint);
        Assert.Equal(before.ErasedAt, after.ErasedAt);
        Assert.Equal(before.CreatedAt, after.CreatedAt);
    }

    /// <summary>AC-014, VR-002: Google's address in another letter case still matches.</summary>
    [Fact]
    public async Task GooglesAddressInAnotherLetterCase_StillMatches()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: SchoolInGoogle(TargetEntry with { Email = "  " + TargetEmailOtherCase }),
            seed: SeedInAdvanceAsync);

        AssertCompleted(await host.WaitForFinishedRunAsync(ct));

        Assert.Equal(0, await ParticipantsWithAsync(host, TargetGoogleUserId, ct));
        Assert.Equal(TargetGoogleUserId, Assert.Single(await host.ExclusionRowsAsync(ct)).GoogleUserId);
    }

    /// <summary>AC-013, FR-006: the course's owner on its roster matched by fingerprint — the course is stored without owner.</summary>
    [Fact]
    public async Task TheCoursesOwner_MatchedOnItsRoster_LeavesTheCourseWithoutOwner()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: reader => reader.WithCourse(
                CourseGoogleId,
                PhysicsName,
                teachers: [TargetEntry, TeacherEntry],
                students: [OtherEntry],
                ownerGoogleId: TargetGoogleUserId),
            seed: SeedInAdvanceAsync);

        AssertCompleted(await host.WaitForFinishedRunAsync(ct));

        Assert.Equal(1L, await host.CountAsync("course", ct));
        Assert.Null(await OwnerAsync(host, ct));
        Assert.Equal(0, await ParticipantsWithAsync(host, TargetGoogleUserId, ct));
        Assert.Equal(1, await MembershipsOfAsync(host, TeacherGoogleUserId, ct));
    }

    /// <summary>VR-004: a roster entry with no email in Google's answer is never matched — it is stored as usual.</summary>
    [Fact]
    public async Task ARosterEntryWithoutEmail_IsNotMatched()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: SchoolInGoogle(TargetEntry with { Email = null }),
            seed: SeedInAdvanceAsync);

        AssertCompleted(await host.WaitForFinishedRunAsync(ct));

        Assert.Equal(1, await ParticipantsWithAsync(host, TargetGoogleUserId, ct));
        Assert.Null(Assert.Single(await host.ExclusionRowsAsync(ct)).GoogleUserId);
    }

    /// <summary>
    /// AC-004, AC-015, FR-007: after the match the exclusion works by id — the next run, with the student's address
    /// renamed in Google, still stores nothing of them, and the filled exclusion is not changed again.
    /// </summary>
    [Fact]
    public async Task AfterTheMatch_ARenamedAddress_DoesNotBringTheStudentBack_AndTheExclusionStaysAsFilled()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database, ct, intervalMinutes: 1, classroom: SchoolInGoogle(), seed: SeedInAdvanceAsync);
        var first = await host.WaitForFinishedRunAsync(ct);
        AssertCompleted(first);
        var filled = Assert.Single(await host.ExclusionRowsAsync(ct));
        Assert.Equal(TargetGoogleUserId, filled.GoogleUserId);

        host.Classroom.WithRoster(CourseGoogleId, teachers: [TeacherEntry], students: [TargetEntry with { Email = RenamedEmail }, OtherEntry]);
        var second = await NextRunAsync(host, first.RunId, ct);

        AssertCompleted(second);
        Assert.Equal(0, await ParticipantsWithAsync(host, TargetGoogleUserId, ct));
        Assert.Equal(0, await SubmissionsOfAsync(host, TargetGoogleUserId, ct));
        Assert.Equal(1, await MembershipsOfAsync(host, OtherGoogleUserId, ct));
        AssertSameRow(filled, Assert.Single(await host.ExclusionRowsAsync(ct)));
    }

    // ---------------------------------------------------------------- exclusions that are not filled (I-1, I-2, FR-008)

    /// <summary>
    /// AC-015, I-1: an exclusion left by an erasure keeps working by id only — a roster entry with another Google id whose
    /// email has its fingerprint is loaded, and the exclusion is not changed.
    /// </summary>
    [Fact]
    public async Task AnExclusionFromAnErasure_IsNotMatchedByFingerprint_NorChanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var formerAccount = CourseTestData.UserId(7);
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: SchoolInGoogle(),
            seed: (h, token) => h.SeedExclusionAsync(formerAccount, TargetEmail, token));
        var before = Assert.Single(await host.ExclusionRowsAsync(ct));

        AssertCompleted(await host.WaitForFinishedRunAsync(ct));

        Assert.Equal(1, await ParticipantsWithAsync(host, TargetGoogleUserId, ct));
        Assert.Equal(1, await SubmissionsOfAsync(host, TargetGoogleUserId, ct));
        AssertSameRow(before, Assert.Single(await host.ExclusionRowsAsync(ct)));
    }

    /// <summary>
    /// AC-016, FR-007, I-2: the matched Google id is already held by another exclusion — the person is skipped, the
    /// exclusion entered in advance stays without id, and the run does not fail.
    /// </summary>
    [Fact]
    public async Task AMatchedIdAlreadyHeldByAnotherExclusion_SkipsThePerson_AndLeavesTheEntryWithoutId()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: SchoolInGoogle(),
            seed: async (h, token) =>
            {
                await h.SeedExclusionAsync(TargetGoogleUserId, OldEmail, token);
                await h.SeedExclusionInAdvanceAsync(TargetEmail, token);
            });

        AssertCompleted(await host.WaitForFinishedRunAsync(ct));

        Assert.Equal(0, await ParticipantsWithAsync(host, TargetGoogleUserId, ct));
        var rows = await host.ExclusionRowsAsync(ct);
        Assert.Equal(2, rows.Count);
        Assert.Equal(TargetGoogleUserId, rows[0].GoogleUserId);
        Assert.Null(rows[1].GoogleUserId);
    }

    /// <summary>
    /// FR-007, I-2: two roster entries with different Google ids and one fingerprint — both skipped in the course, the
    /// exclusion takes the Google id that sorts first ordinally.
    /// </summary>
    [Fact]
    public async Task TwoEntriesWithOneFingerprint_AreBothSkipped_AndTheOrdinallyFirstIdFillsTheExclusion()
    {
        var ct = TestContext.Current.CancellationToken;
        var twin = new RosterEntry(CourseTestData.UserId(9), TargetEmailOtherCase, "Олена Коваль (2)");
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: reader => reader.WithCourse(
                CourseGoogleId,
                PhysicsName,
                teachers: [TeacherEntry],
                students: [twin, TargetEntry, OtherEntry],
                ownerGoogleId: TeacherGoogleUserId),
            seed: SeedInAdvanceAsync);

        AssertCompleted(await host.WaitForFinishedRunAsync(ct));

        Assert.Equal(0, await ParticipantsWithAsync(host, TargetGoogleUserId, ct));
        Assert.Equal(0, await ParticipantsWithAsync(host, twin.GoogleUserId, ct));
        Assert.Equal(1, await ParticipantsWithAsync(host, OtherGoogleUserId, ct));
        var first = new[] { TargetGoogleUserId, twin.GoogleUserId }.Order(StringComparer.Ordinal).First();
        Assert.Equal(first, Assert.Single(await host.ExclusionRowsAsync(ct)).GoogleUserId);
    }

    /// <summary>
    /// AC-017, FR-008, OD-003 a: the matched Google id is already a stored participant (loaded under an older address) —
    /// the entry is synchronized as usual (the stored email becomes Google's), the exclusion keeps no id, and entering the
    /// address again answers "already loaded".
    /// </summary>
    [Fact]
    public async Task AMatchedIdAlreadyStoredAsAParticipant_IsSynchronizedAsUsual_AndTheExclusionStaysWithoutId()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: SchoolInGoogle(),
            seed: async (h, token) =>
            {
                var course = await CourseRows.InsertCourseAsync(h, token, googleId: CourseGoogleId, name: PhysicsName, ownerGoogleId: TeacherGoogleUserId);
                var stored = await h.InsertPersonAsync(1, TargetSurname, TargetGivenName, OldEmail, token);
                await Cl.AddStudentAsync(h, course, stored, token);
                await h.SeedExclusionInAdvanceAsync(TargetEmail, token);
            });

        AssertCompleted(await host.WaitForFinishedRunAsync(ct));

        Assert.Equal(1, await ParticipantsWithAsync(host, TargetGoogleUserId, ct));
        Assert.Equal(1, await ParticipantsWithEmailAsync(host, TargetEmail, ct));
        Assert.Equal(1, await MembershipsOfAsync(host, TargetGoogleUserId, ct));
        Assert.Equal(1, await SubmissionsOfAsync(host, TargetGoogleUserId, ct));
        Assert.Null(Assert.Single(await host.ExclusionRowsAsync(ct)).GoogleUserId);

        var again = await host.EnterExclusionAsync(await host.InsertAdminAsync(ct), TargetEmail, ct);

        Assert.Equal(ExclusionEntryOutcome.AlreadyLoaded, again.Outcome);
        Assert.Single(await host.ExclusionRowsAsync(ct));
    }

    // ---------------------------------------------------------------- lifting (FR-010, AC-008)

    /// <summary>AC-008, FR-010: a lifting after the match removes the filled exclusion; the next run loads the person as new.</summary>
    [Fact]
    public async Task ALiftingAfterTheMatch_RemovesTheExclusion_AndTheNextRunLoadsThePerson()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database, ct, intervalMinutes: 1, classroom: SchoolInGoogle(), seed: SeedInAdvanceAsync);
        var first = await host.WaitForFinishedRunAsync(ct);
        Assert.Equal(TargetGoogleUserId, Assert.Single(await host.ExclusionRowsAsync(ct)).GoogleUserId);

        var lifted = await host.LiftAsync(await host.InsertAdminAsync(ct), TargetEmailOtherCase, ct);
        Assert.Equal(ExclusionLiftingOutcome.Lifted, lifted.Outcome);
        AssertCompleted(await NextRunAsync(host, first.RunId, ct));

        Assert.Empty(await host.ExclusionRowsAsync(ct));
        Assert.Equal(1, await ParticipantsWithAsync(host, TargetGoogleUserId, ct));
        Assert.Equal(1, await MembershipsOfAsync(host, TargetGoogleUserId, ct));
        Assert.Equal(1, await SubmissionsOfAsync(host, TargetGoogleUserId, ct));
    }

    // ---------------------------------------------------------------- serialization with the entry (FR-011, AC-012)

    /// <summary>
    /// AC-012, FR-011: an entry committing while the run's course transaction waits on the fence — after the run read the
    /// roster — is seen at the write; the run stores nothing of the person and fills the exclusion. With no salt before
    /// the entry, the run must still use the salt the entry created (it is read where the course is written).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AnEntryCommittedWhileTheRunWaitsOnTheFence_IsSeenAtTheWrite(bool saltBefore)
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await InstallationTestHost.CreateAsync(database, ct);
        await using var _host = host;
        await ReadOnlyModeHost.SeedAsync(host, ReadOnlyModeHost.Cause.NotReadOnly, ct);
        await AccessCheckHostExtensions.SeedConnectionAsync(host, SeededConnection.Usable, ct);
        if (saltBefore)
        {
            await host.ExecuteAsync(
                "INSERT INTO erasure_salt (singleton, salt, created_at, updated_at) VALUES (true, @salt, @at, @at)",
                ct,
                ("salt", ErasureTestData.KnownSalt()),
                ("at", host.Time.GetUtcNow()));
        }

        host.ControlPlane.ReplySuccess().ReplySuccess().ReplySuccess();
        SchoolInGoogle()(host.Classroom);

        await using var entry = new NpgsqlConnection(host.ConnectionString);
        await entry.OpenAsync(ct);
        await using var transaction = await entry.BeginTransactionAsync(ct);
        await using (var fence = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@key)", entry, transaction))
        {
            fence.Parameters.AddWithValue("key", ErasureTestData.FenceKey);
            await fence.ExecuteNonQueryAsync(ct);
        }

        host.Start();
        await host.Time.WaitUntilAsync(
            () => host.ScalarAsync<long>(
                "SELECT count(*) FROM pg_locks WHERE locktype = 'advisory' AND NOT granted", ct).GetAwaiter().GetResult() > 0,
            "the synchronization's course transaction waiting on the fence",
            ct);
        await using (var write = new NpgsqlCommand(
            """
            INSERT INTO erasure_salt (singleton, salt, created_at, updated_at) VALUES (true, @salt, @at, @at)
            ON CONFLICT (singleton) DO NOTHING;
            INSERT INTO erased_participant (google_user_id, email_fingerprint, erased_at, created_at, updated_at)
            VALUES (NULL, @fingerprint, @at, @at, @at);
            """,
            entry,
            transaction))
        {
            write.Parameters.AddWithValue("salt", ErasureTestData.KnownSalt());
            write.Parameters.AddWithValue("fingerprint", ErasureTestData.Fingerprint(TargetEmail, ErasureTestData.KnownSalt()));
            write.Parameters.AddWithValue("at", host.Time.GetUtcNow());
            await write.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
        var run = await host.WaitForFinishedRunAsync(ct);

        AssertCompleted(run);
        Assert.Equal(0, await ParticipantsWithAsync(host, TargetGoogleUserId, ct));
        Assert.Equal(1, await ParticipantsWithAsync(host, OtherGoogleUserId, ct));
        Assert.Equal(TargetGoogleUserId, Assert.Single(await host.ExclusionRowsAsync(ct)).GoogleUserId);
    }

    // ---------------------------------------------------------------- the Meet load (FR-009, AC-005)

    /// <summary>
    /// AC-005, FR-009: an exclusion entered in advance hides the person's Meet connections and organizer email from the
    /// first load — before its match (no Classroom roster names them) and after it (the roster does).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheMeetLoad_SkipsTheConnections_AndClearsTheOrganizer_OfAnEmailEnteredInAdvance(bool onARoster)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: onARoster ? SchoolInGoogle() : null,
            seed: async (h, token) =>
            {
                h.Meet.WithPage(
                    MeetTestData.Event(1, 1, LeftAt, 600, TeacherEmail, TargetEmail),
                    MeetTestData.Event(1, 2, LeftAt, 300, TeacherEmail, TargetEmailOtherCase),
                    MeetTestData.Event(1, 3, LeftAt, 600, TeacherEmail, OtherEmail),
                    MeetTestData.Event(2, 4, LeftAt, 600, TargetEmail, OtherEmail));
                await h.SeedExclusionInAdvanceAsync(TargetEmail, token);
            });

        AssertCompleted(await host.WaitForFinishedRunAsync(ct));

        Assert.Equal(0L, await host.ScalarAsync<long>(
            "SELECT count(*) FROM meet_participation WHERE lower(email) = @email", ct, ("email", TargetEmail)));
        Assert.Equal(2L, await host.ScalarAsync<long>(
            "SELECT count(*) FROM meet_participation WHERE email = @email", ct, ("email", OtherEmail)));
        Assert.Equal(1L, await host.ScalarAsync<long>(
            "SELECT count(*) FROM meet_session WHERE conference_id = @id", ct, ("id", MeetTestData.ConferenceId(2))));
        Assert.Null(await host.ScalarAsync<string>(
            "SELECT organizer_email FROM meet_session WHERE conference_id = @id", ct, ("id", MeetTestData.ConferenceId(2))));
        Assert.Equal(onARoster ? TargetGoogleUserId : null, Assert.Single(await host.ExclusionRowsAsync(ct)).GoogleUserId);
    }

    // ---------------------------------------------------------------- logging (FR-007, SC-10, S-06)

    /// <summary>FR-007, SC-10, S-06: after a run that matched and filled, no log line carries the email, the id or the fingerprint.</summary>
    [Fact]
    public async Task AfterTheFill_NoLogLine_CarriesThePersonsEmail_GoogleUserId_OrFingerprint()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct, classroom: SchoolInGoogle(), seed: SeedInAdvanceAsync);
        AssertCompleted(await host.WaitForFinishedRunAsync(ct));
        var row = Assert.Single(await host.ExclusionRowsAsync(ct));
        Assert.Equal(TargetGoogleUserId, row.GoogleUserId);

        var text = string.Join("\n", await host.ReadLogFilesWhileRunningAsync(ct));

        foreach (var value in TargetPersonalValues)
        {
            Assert.DoesNotContain(value, text, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(Convert.ToHexString(row.EmailFingerprint!), text, StringComparison.OrdinalIgnoreCase);
    }
}
