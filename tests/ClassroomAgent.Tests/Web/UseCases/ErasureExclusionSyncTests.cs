using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureHostExtensions;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-038 AC-004, AC-007, AC-010, AC-011, AC-019 and FR-011, FR-012: synchronization through the real host, its
/// background service and PostgreSQL, the Classroom port substituted (TC-4). The exclusion is consulted where the run
/// writes, under the shared side of the write fence, so even an erasure committed while the run is in flight holds.
/// </summary>
public sealed class ErasureExclusionSyncTests(PostgreSqlFixture database)
{
    private static readonly string CourseGoogleId = CourseTestData.CourseId(1);

    private static RosterEntry TargetEntry => new(TargetGoogleUserId, TargetEmail, TargetFullName);

    private static RosterEntry OtherEntry => new(OtherGoogleUserId, OtherEmail, OtherFullName);

    private static RosterEntry TeacherEntry => new(TeacherGoogleUserId, TeacherEmail, TeacherFullName);

    /// <summary>One course owned by the target: the teacher, the target and the other student, one item, one submission each.</summary>
    private static void SchoolInGoogle(FakeClassroomReader reader) =>
        reader
            .WithCourse(CourseGoogleId, PhysicsName, teachers: [TeacherEntry], students: [TargetEntry, OtherEntry], ownerGoogleId: TargetGoogleUserId)
            .WithCourseWork(CourseGoogleId, CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), InstallationTestHost.DefaultStart, maxPoints: 12m)
            .WithSubmission(CourseGoogleId, CourseWorkTestData.ItemId(1), TargetGoogleUserId, CourseWorkTestData.SubmissionId(1), CourseWorkTestData.RawStates.TurnedIn, assignedGrade: 10m)
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

    private static Task<string?> OwnerAsync(InstallationTestHost host, CancellationToken ct) =>
        host.ScalarAsync<string>("SELECT owner_google_id FROM course WHERE google_id = @id", ct, ("id", CourseGoogleId));

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

    /// <summary>AC-004, FR-012: after an erasure the next run does not recreate the person, their membership, submission or ownership.</summary>
    [Fact]
    public async Task AfterTheErasure_TheNextRunDoesNotBringThePersonBack_AndSynchronizesEveryoneElse()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct, intervalMinutes: 1, classroom: SchoolInGoogle);
        var first = await host.WaitForFinishedRunAsync(ct);
        Assert.Equal(SyncTestData.Status.Completed, first.Status);
        var target = await host.ScalarAsync<long>("SELECT id FROM classroom_participant WHERE google_user_id = @id", ct, ("id", TargetGoogleUserId));

        var erased = await host.EraseAsync(await host.InsertAdminAsync(ct), target, ct);
        Assert.Equal(ParticipantErasureOutcome.Erased, erased.Outcome);
        var second = await NextRunAsync(host, first.RunId, ct);

        Assert.Equal(SyncTestData.Status.Completed, second.Status);
        Assert.Equal(0, await ParticipantsWithAsync(host, TargetGoogleUserId, ct));
        Assert.Equal(0, await MembershipsOfAsync(host, TargetGoogleUserId, ct));
        Assert.Equal(0, await SubmissionsOfAsync(host, TargetGoogleUserId, ct));
        Assert.Null(await OwnerAsync(host, ct));
        Assert.Equal(1, await MembershipsOfAsync(host, OtherGoogleUserId, ct));
        Assert.Equal(1, await SubmissionsOfAsync(host, OtherGoogleUserId, ct));
        Assert.Equal(1, await MembershipsOfAsync(host, TeacherGoogleUserId, ct));
    }

    /// <summary>AC-004, FR-012: an exclusion present before the first run — the person is never created; the course has no owner.</summary>
    [Fact]
    public async Task AnExcludedPerson_IsNeverCreated_AndTheirCourseIsStoredWithoutOwner()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: SchoolInGoogle,
            seed: (h, token) => h.SeedExclusionAsync(TargetGoogleUserId, TargetEmail, token));

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        Assert.Equal(0, await ParticipantsWithAsync(host, TargetGoogleUserId, ct));
        Assert.Equal(0, await SubmissionsOfAsync(host, TargetGoogleUserId, ct));
        Assert.Null(await OwnerAsync(host, ct));
        Assert.Equal(1, await ParticipantsWithAsync(host, OtherGoogleUserId, ct));
        Assert.Equal(1, await SubmissionsOfAsync(host, OtherGoogleUserId, ct));
        Assert.Equal(1, await ParticipantsWithAsync(host, TeacherGoogleUserId, ct));
    }

    /// <summary>FR-012: a skipped person is not a failure — the run completes and nothing is reported as failed.</summary>
    [Fact]
    public async Task ASkippedPerson_IsNotAFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: SchoolInGoogle,
            seed: (h, token) => h.SeedExclusionAsync(TargetGoogleUserId, TargetEmail, token));

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        Assert.Null(run.LastError);
        Assert.Equal(0, await ParticipantsWithAsync(host, TargetGoogleUserId, ct));
    }

    /// <summary>AC-011, FR-012: a submitter not on the roster with an excluded Google user id is not created; another one is.</summary>
    [Fact]
    public async Task AnExcludedSubmitterOffTheRoster_IsNotCreated()
    {
        var ct = TestContext.Current.CancellationToken;
        var stranger = CourseTestData.UserId(8);
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: reader => reader
                .WithCourse(CourseGoogleId, PhysicsName, teachers: [TeacherEntry], students: [OtherEntry])
                .WithCourseWork(CourseGoogleId, CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), InstallationTestHost.DefaultStart, maxPoints: 12m)
                .WithSubmission(CourseGoogleId, CourseWorkTestData.ItemId(1), TargetGoogleUserId, CourseWorkTestData.SubmissionId(1), CourseWorkTestData.RawStates.TurnedIn)
                .WithSubmission(CourseGoogleId, CourseWorkTestData.ItemId(1), stranger, CourseWorkTestData.SubmissionId(2), CourseWorkTestData.RawStates.TurnedIn),
            seed: (h, token) => h.SeedExclusionAsync(TargetGoogleUserId, TargetEmail, token));

        await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(0, await ParticipantsWithAsync(host, TargetGoogleUserId, ct));
        Assert.Equal(0, await SubmissionsOfAsync(host, TargetGoogleUserId, ct));
        Assert.Equal(1, await ParticipantsWithAsync(host, stranger, ct));
        Assert.Equal(1, await SubmissionsOfAsync(host, stranger, ct));
    }

    /// <summary>AC-019, OD-001 a: an exclusion without fingerprint still keeps the Google user id out.</summary>
    [Fact]
    public async Task AnExclusionWithoutFingerprint_StillSkipsTheGoogleUserId()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: SchoolInGoogle,
            seed: (h, token) => h.SeedExclusionAsync(TargetGoogleUserId, null, token));

        await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(0, await ParticipantsWithAsync(host, TargetGoogleUserId, ct));
        Assert.Equal(1, await ParticipantsWithAsync(host, OtherGoogleUserId, ct));
    }

    /// <summary>AC-007, FR-009: after the lifting the next run loads the person as new, with membership and submission.</summary>
    [Fact]
    public async Task AfterTheLifting_TheNextRunLoadsThePersonAsNew()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            intervalMinutes: 1,
            classroom: SchoolInGoogle,
            seed: (h, token) => h.SeedExclusionAsync(TargetGoogleUserId, TargetEmail, token));
        var first = await host.WaitForFinishedRunAsync(ct);
        Assert.Equal(0, await ParticipantsWithAsync(host, TargetGoogleUserId, ct));

        var lifted = await host.LiftAsync(await host.InsertAdminAsync(ct), TargetEmail.ToUpperInvariant(), ct);
        Assert.Equal(ExclusionLiftingOutcome.Lifted, lifted.Outcome);
        await NextRunAsync(host, first.RunId, ct);

        Assert.Empty(await host.ExclusionRowsAsync(ct));
        Assert.Equal(1, await ParticipantsWithAsync(host, TargetGoogleUserId, ct));
        Assert.Equal(1, await MembershipsOfAsync(host, TargetGoogleUserId, ct));
        Assert.Equal(1, await SubmissionsOfAsync(host, TargetGoogleUserId, ct));
        Assert.Equal(TargetGoogleUserId, await OwnerAsync(host, ct));
    }

    /// <summary>
    /// AC-010, FR-011, db-design §7.3: an erasure that holds the fence while the run reaches its course transaction makes
    /// the run wait; the exclusion it commits is seen, so the run stores nothing of the person. Without the fence the run
    /// would never wait, and the wait below times out.
    /// </summary>
    [Fact]
    public async Task ARunThatReachesItsWriteDuringAnErasure_WaitsForIt_AndStoresNothingOfThePerson()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await InstallationTestHost.CreateAsync(database, ct);
        await using var _host = host;
        await ReadOnlyModeHost.SeedAsync(host, ReadOnlyModeHost.Cause.NotReadOnly, ct);
        await AccessCheckHostExtensions.SeedConnectionAsync(host, SeededConnection.Usable, ct);
        host.ControlPlane.ReplySuccess().ReplySuccess().ReplySuccess();
        SchoolInGoogle(host.Classroom);

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
            "the synchronization's course transaction waiting on the erasure fence",
            ct);
        await using (var exclusion = new NpgsqlCommand(
            "INSERT INTO erased_participant (google_user_id, email_fingerprint, erased_at, created_at, updated_at) VALUES (@id, NULL, @at, @at, @at)",
            erasure,
            transaction))
        {
            exclusion.Parameters.AddWithValue("id", TargetGoogleUserId);
            exclusion.Parameters.AddWithValue("at", host.Time.GetUtcNow());
            await exclusion.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        Assert.Equal(0, await ParticipantsWithAsync(host, TargetGoogleUserId, ct));
        Assert.Equal(1, await ParticipantsWithAsync(host, OtherGoogleUserId, ct));
    }
}
