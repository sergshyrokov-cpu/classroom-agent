using ClassroomAgent.Application.Models;
using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureHostExtensions;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-038 AC-005, AC-010, AC-012 and FR-011, FR-012, OD-001 a: the Meet load through the real host and PostgreSQL, the
/// Meet port substituted (TC-4). A participation whose email's fingerprint is excluded is not stored, in any letter case;
/// a meeting organized by an excluded person is stored without the organizer email and never regains it.
/// </summary>
public sealed class ErasureExclusionMeetTests(PostgreSqlFixture database)
{
    private static readonly DateTimeOffset LeftAt = InstallationTestHost.DefaultStart - TimeSpan.FromDays(1);

    /// <summary>
    /// Conference 1, organized by the teacher: the target twice (the second in another letter case), the other student, the
    /// teacher. Conference 2, organized by the target: the other student.
    /// </summary>
    private static void MeetInGoogle(FakeMeetReportsReader meet) =>
        meet.WithPage(
            MeetTestData.Event(1, 1, LeftAt, 600, TeacherEmail, TargetEmail),
            MeetTestData.Event(1, 2, LeftAt, 300, TeacherEmail, TargetEmailOtherCase),
            MeetTestData.Event(1, 3, LeftAt, 600, TeacherEmail, OtherEmail),
            MeetTestData.Event(1, 4, LeftAt, 900, TeacherEmail, TeacherEmail),
            MeetTestData.Event(2, 5, LeftAt, 600, TargetEmail, OtherEmail));

    private static Task<long> TargetParticipationsAsync(InstallationTestHost host, CancellationToken ct) =>
        host.ScalarAsync<long>("SELECT count(*) FROM meet_participation WHERE lower(email) = @email", ct, ("email", TargetEmail));

    private static Task<long> ParticipationsOfAsync(InstallationTestHost host, string email, CancellationToken ct) =>
        host.ScalarAsync<long>("SELECT count(*) FROM meet_participation WHERE email = @email", ct, ("email", email));

    private static Task<string?> OrganizerOfAsync(InstallationTestHost host, int conference, CancellationToken ct) =>
        host.ScalarAsync<string>(
            "SELECT organizer_email FROM meet_session WHERE conference_id = @id", ct, ("id", MeetTestData.ConferenceId(conference)));

    private static Task<long> SessionsOfAsync(InstallationTestHost host, int conference, CancellationToken ct) =>
        host.ScalarAsync<long>(
            "SELECT count(*) FROM meet_session WHERE conference_id = @id", ct, ("id", MeetTestData.ConferenceId(conference)));

    /// <summary>AC-005, AC-012, FR-012: none of the person's participations is stored; everyone else's is.</summary>
    [Fact]
    public async Task AnExcludedEmail_HasNoParticipationStored_InAnyLetterCase()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            seed: async (h, token) =>
            {
                MeetInGoogle(h.Meet);
                await h.SeedExclusionAsync(TargetGoogleUserId, TargetEmail, token);
            });

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        Assert.Equal(0, await TargetParticipationsAsync(host, ct));
        Assert.Equal(2, await ParticipationsOfAsync(host, OtherEmail, ct));
        Assert.Equal(1, await ParticipationsOfAsync(host, TeacherEmail, ct));
    }

    /// <summary>AC-005, FR-012: the meeting the excluded person organized is stored, without the organizer email.</summary>
    [Fact]
    public async Task AMeetingOrganizedByAnExcludedPerson_IsStoredWithoutTheOrganizer()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            seed: async (h, token) =>
            {
                MeetInGoogle(h.Meet);
                await h.SeedExclusionAsync(TargetGoogleUserId, TargetEmail, token);
            });

        await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(1, await SessionsOfAsync(host, 2, ct));
        Assert.Null(await OrganizerOfAsync(host, 2, ct));
        Assert.Equal(TeacherEmail, await OrganizerOfAsync(host, 1, ct));
    }

    /// <summary>
    /// AC-005, FR-012 "including the 3 days it re-reads": after an erasure the next run re-reads the same events and stores
    /// none of the person's participations; the meeting they organized stays without the organizer email.
    /// </summary>
    [Fact]
    public async Task AfterTheErasure_TheOverlapReRead_BringsNothingBack()
    {
        var ct = TestContext.Current.CancellationToken;
        long target = 0;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            intervalMinutes: 1,
            seed: async (h, token) =>
            {
                MeetInGoogle(h.Meet);
                var course = await CourseRows.InsertCourseAsync(h, token, googleId: CourseTestData.CourseId(1), name: PhysicsName);
                target = await h.InsertPersonAsync(1, TargetSurname, TargetGivenName, TargetEmail, token);
                await h.AddStudentAsync(course, target, token);
            });
        var first = await host.WaitForFinishedRunAsync(ct);
        Assert.Equal(2, await TargetParticipationsAsync(host, ct));

        var erased = await host.EraseAsync(await host.InsertAdminAsync(ct), target, ct);
        Assert.Equal(ParticipantErasureOutcome.Erased, erased.Outcome);
        await host.Time.AdvanceWhenDueAsync(TimeSpan.FromMinutes(1), ct);
        await host.Time.WaitUntilAsync(
            () => host.SyncStatesAsync(ct).GetAwaiter().GetResult() is [{ } row] && row.RunId != first.RunId && row.Status != SyncTestData.Status.Running,
            "a second finished synchronization run",
            ct);

        Assert.Equal(2, host.Meet.Calls.Count);
        Assert.True(host.Meet.Calls[1].From <= LeftAt, "The second run must re-read the overlap that holds the events.");
        Assert.Equal(0, await TargetParticipationsAsync(host, ct));
        Assert.Null(await OrganizerOfAsync(host, 2, ct));
        Assert.Equal(2, await ParticipationsOfAsync(host, OtherEmail, ct));
    }

    /// <summary>OD-001 a, FR-012: an exclusion without fingerprint affects synchronization only — the Meet load stores the email.</summary>
    [Fact]
    public async Task AnExclusionWithoutFingerprint_DoesNotAffectTheMeetLoad()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            seed: async (h, token) =>
            {
                MeetInGoogle(h.Meet);
                await h.SeedExclusionAsync(TargetGoogleUserId, null, token);
            });

        await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(2, await TargetParticipationsAsync(host, ct));
        Assert.Equal(TargetEmail, await OrganizerOfAsync(host, 2, ct));
    }

    /// <summary>
    /// AC-010, FR-011: a Meet page transaction that reaches its write while an erasure holds the fence waits for it, and
    /// the exclusion committed meanwhile keeps the person's participations out.
    /// </summary>
    [Fact]
    public async Task AMeetPageThatReachesItsWriteDuringAnErasure_WaitsForIt_AndStoresNothingOfThePerson()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await InstallationTestHost.CreateAsync(database, ct);
        await using var _host = host;
        await ReadOnlyModeHost.SeedAsync(host, ReadOnlyModeHost.Cause.NotReadOnly, ct);
        await AccessCheckHostExtensions.SeedConnectionAsync(host, SeededConnection.Usable, ct);
        host.ControlPlane.ReplySuccess().ReplySuccess().ReplySuccess();
        MeetInGoogle(host.Meet);
        await host.ExecuteAsync(
            "INSERT INTO erasure_salt (singleton, salt, created_at, updated_at) VALUES (true, @salt, @at, @at)",
            ct,
            ("salt", ErasureTestData.KnownSalt()),
            ("at", host.Time.GetUtcNow()));

        await using var erasure = new NpgsqlConnection(host.ConnectionString);
        await erasure.OpenAsync(ct);
        await using var transaction = await erasure.BeginTransactionAsync(ct);
        await using (var fence = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@key)", erasure, transaction))
        {
            fence.Parameters.AddWithValue("key", ErasureTestData.FenceKey);
            await fence.ExecuteNonQueryAsync(ct);
        }

        host.Start();
        await host.Time.WaitUntilAsync(
            () => host.ScalarAsync<long>(
                "SELECT count(*) FROM pg_locks WHERE locktype = 'advisory' AND NOT granted", ct).GetAwaiter().GetResult() > 0,
            "the Meet page transaction waiting on the erasure fence",
            ct);
        await using (var exclusion = new NpgsqlCommand(
            "INSERT INTO erased_participant (google_user_id, email_fingerprint, erased_at, created_at, updated_at) VALUES (@id, @fp, @at, @at, @at)",
            erasure,
            transaction))
        {
            exclusion.Parameters.AddWithValue("id", TargetGoogleUserId);
            exclusion.Parameters.AddWithValue("fp", ErasureTestData.Fingerprint(TargetEmail, ErasureTestData.KnownSalt()));
            exclusion.Parameters.AddWithValue("at", host.Time.GetUtcNow());
            await exclusion.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        Assert.Equal(0, await TargetParticipationsAsync(host, ct));
        Assert.Null(await OrganizerOfAsync(host, 2, ct));
        Assert.Equal(2, await ParticipationsOfAsync(host, OtherEmail, ct));
    }
}
