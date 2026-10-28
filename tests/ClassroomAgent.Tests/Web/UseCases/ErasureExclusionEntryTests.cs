using ClassroomAgent.Application.Models;
using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureHostExtensions;
using Cl = ClassroomAgent.Tests.TestInfrastructure.CourseListHostExtensions;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-065 AC-001, AC-002, AC-006, AC-007, AC-012, AC-014, AC-021 and FR-002 … FR-005, FR-011: the entry use case resolved
/// from the real host's container over PostgreSQL (TC-2). Rows are seeded by SQL, so the tests do not depend on the code
/// under test; every name, email and Google id is invented (TC-4).
/// </summary>
public sealed class ErasureExclusionEntryTests(PostgreSqlFixture database)
{
    /// <summary>A started installation outside read-only mode with no Google connection saved, its Admin's id, the purge done.</summary>
    private static async Task<(InstallationTestHost Host, long AdminId)> StartAsync(PostgreSqlFixture database, CancellationToken ct)
    {
        var host = await InstallationTestHost.CreateAsync(database, ct);
        await ReadOnlyModeHost.SeedAsync(host, ReadOnlyModeHost.Cause.NotReadOnly, ct);
        host.ControlPlaneHandler = WorkspaceConnectionHostExtensions.ApprovingChannel();
        host.Start();
        // The retention purge runs at every host start and deletes a participant that has no membership yet.
        await host.WaitForPurgeRunsAsync(1, ct);
        return (host, await host.InsertAdminAsync(ct));
    }

    private static Task<string> AuditRowsTextAsync(InstallationTestHost host, CancellationToken ct) =>
        host.ScalarAsync<string>(
            "SELECT coalesce(string_agg(row_to_json(a)::text, '|'), '') FROM audit_event a WHERE action <> 'retention_purge_run'",
            ct)!;

    /// <summary>
    /// AC-001, AC-002, FR-002, FR-003, FR-013: on an installation with no Google connection and no salt, one entry creates
    /// the salt and stores one exclusion — the fingerprint keyed with that salt, no Google user id, the entry date — and
    /// one audit row naming the exclusion and no personal value.
    /// </summary>
    [Fact]
    public async Task OnAnEmptyInstallation_TheEntryStoresOneExclusion_WithTheFingerprint_AndCreatesTheSalt()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, adminId) = await StartAsync(database, ct);
        await using var _host = host;
        Assert.Equal(0L, await host.CountAsync("workspace_connection", ct));
        Assert.Empty(await host.SaltRowsAsync(ct));

        var result = await host.EnterExclusionAsync(adminId, "  " + TargetEmail.ToUpperInvariant() + " ", ct);

        Assert.Equal(ExclusionEntryOutcome.Entered, result.Outcome);
        var salt = Assert.Single(await host.SaltRowsAsync(ct));
        var row = Assert.Single(await host.ExclusionRowsAsync(ct));
        Assert.Null(row.GoogleUserId);
        Assert.Equal(ErasureTestData.Fingerprint(TargetEmail, salt), row.EmailFingerprint);
        Assert.Equal(host.Time.GetUtcNow(), row.ErasedAt);
        Assert.Equal(row.CreatedAt, row.UpdatedAt);
        var audit = Assert.Single(await host.ErasureAuditRowsAsync(ct));
        Assert.Equal(ErasureTestData.Codes.EnteredAction, audit.Action);
        Assert.Equal(ErasureTestData.Codes.AppUserActor, audit.ActorType);
        Assert.Equal(adminId, audit.ActorId);
        Assert.Equal(ErasureTestData.Codes.AdminRole, audit.ActorRole);
        Assert.Equal(ErasureTestData.Codes.ExclusionTarget, audit.TargetType);
        Assert.Equal(row.Id, audit.TargetId);
        Assert.Equal(ErasureTestData.Codes.Succeeded, audit.Outcome);
        Assert.Null(audit.ErasedMemberships);
        var auditText = await AuditRowsTextAsync(host, ct);
        Assert.DoesNotContain("koval", auditText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Convert.ToHexString(row.EmailFingerprint!), auditText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC-006, AC-014, FR-004: participants whose stored email equals the entered one in any letter case — on a roster or
    /// not — are "already loaded", listed exactly as the US-038 search lists them; nothing is written, no salt created.
    /// </summary>
    [Fact]
    public async Task LoadedParticipantsWithTheEmail_AreAlreadyLoaded_ListedAsTheSearchListsThem_AndNothingChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, adminId) = await StartAsync(database, ct);
        await using var _host = host;
        var course = await CourseRows.InsertCourseAsync(host, ct, googleId: CourseTestData.CourseId(1), name: PhysicsName);
        var onRoster = await host.InsertPersonAsync(1, TargetSurname, TargetGivenName, TargetEmailOtherCase, ct);
        await Cl.AddStudentAsync(host, course, onRoster, ct);
        var offRoster = await host.InsertPersonAsync(2, TargetSurname, TargetGivenName, TargetEmail, ct);
        await host.InsertPersonAsync(3, "Мельник", "Петро", OtherEmail, ct);
        var before = await host.ErasureFingerprintAsync(ct);

        var result = await host.EnterExclusionAsync(adminId, TargetEmail, ct);

        Assert.Equal(ExclusionEntryOutcome.AlreadyLoaded, result.Outcome);
        Assert.Equal([onRoster, offRoster], result.LoadedParticipants.Select(p => p.ParticipantId).Order());
        Assert.False(result.LoadedTruncated);
        var search = await host.SearchAsync(TargetEmail, ct);
        Assert.Equal(
            search.Results.Where(r => r.ParticipantId == onRoster),
            result.LoadedParticipants.Where(r => r.ParticipantId == onRoster));
        Assert.Equal(1, result.LoadedParticipants.Single(p => p.ParticipantId == onRoster).StudentCourses);
        Assert.Equal(before, await host.ErasureFingerprintAsync(ct));
        Assert.Empty(await host.SaltRowsAsync(ct));
    }

    /// <summary>FR-004: a participant whose email only contains the entered one is not "loaded" with it.</summary>
    [Fact]
    public async Task AParticipantWithALongerEmail_IsNotAlreadyLoaded()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, adminId) = await StartAsync(database, ct);
        await using var _host = host;
        await host.InsertPersonAsync(1, TargetSurname, TargetGivenName, "x" + TargetEmail, ct);

        var result = await host.EnterExclusionAsync(adminId, TargetEmail, ct);

        Assert.Equal(ExclusionEntryOutcome.Entered, result.Outcome);
    }

    /// <summary>
    /// AC-007, AC-014, FR-005: an exclusion with that fingerprint — left by an erasure or entered in advance — answers
    /// "already excluded" for the email in any case; nothing is written.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AnExistingExclusionOfTheEmail_IsAlreadyExcluded_AndNothingChanges(bool fromAnErasure)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, adminId) = await StartAsync(database, ct);
        await using var _host = host;
        if (fromAnErasure)
        {
            await host.SeedExclusionAsync(TargetGoogleUserId, TargetEmail, ct);
        }
        else
        {
            await host.SeedExclusionInAdvanceAsync(TargetEmail, ct);
        }

        var before = await host.ErasureFingerprintAsync(ct);

        var result = await host.EnterExclusionAsync(adminId, TargetEmailOtherCase, ct);

        Assert.Equal(ExclusionEntryOutcome.AlreadyExcluded, result.Outcome);
        Assert.Empty(result.LoadedParticipants);
        Assert.Equal(before, await host.ErasureFingerprintAsync(ct));
    }

    /// <summary>
    /// AC-021, AC-008, FR-010: the exclusion entered on an installation with no salt is found by a lifting of the same
    /// email — the salt the entry created is the one the lifting uses; both are audited.
    /// </summary>
    [Fact]
    public async Task AfterTheFirstEntry_ALiftingOfTheSameEmail_RemovesIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, adminId) = await StartAsync(database, ct);
        await using var _host = host;
        var entered = await host.EnterExclusionAsync(adminId, TargetEmail, ct);
        Assert.Equal(ExclusionEntryOutcome.Entered, entered.Outcome);

        var lifted = await host.LiftAsync(adminId, TargetEmail.ToUpperInvariant(), ct);

        Assert.Equal(new ExclusionLiftingResult(ExclusionLiftingOutcome.Lifted, null, 1), lifted);
        Assert.Empty(await host.ExclusionRowsAsync(ct));
        Assert.Equal(
            [ErasureTestData.Codes.EnteredAction, ErasureTestData.Codes.LiftedAction],
            (await host.ErasureAuditRowsAsync(ct)).Select(a => a.Action));
    }

    /// <summary>
    /// AC-012, FR-011, I-6: an entry arriving while a synchronization course transaction holds the shared fence and has
    /// stored the person waits for it, then sees the person and answers "already loaded" — nothing excluded. Without the
    /// exclusive fence the entry would not wait and would store an exclusion over a loaded person.
    /// </summary>
    [Fact]
    public async Task AnEntryDuringACourseWrite_WaitsForIt_AndAnswersAlreadyLoaded()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, adminId) = await StartAsync(database, ct);
        await using var _host = host;

        await using var run = new NpgsqlConnection(host.ConnectionString);
        await run.OpenAsync(ct);
        await using var transaction = await run.BeginTransactionAsync(ct);
        await using (var fence = new NpgsqlCommand("SELECT pg_advisory_xact_lock_shared(@key)", run, transaction))
        {
            fence.Parameters.AddWithValue("key", ErasureTestData.FenceKey);
            await fence.ExecuteNonQueryAsync(ct);
        }

        await using (var person = new NpgsqlCommand(
            """
            INSERT INTO classroom_participant (google_user_id, email, full_name, created_at, updated_at)
            VALUES (@id, @email, @name, @at, @at)
            """,
            run,
            transaction))
        {
            person.Parameters.AddWithValue("id", TargetGoogleUserId);
            person.Parameters.AddWithValue("email", TargetEmail);
            person.Parameters.AddWithValue("name", TargetFullName);
            person.Parameters.AddWithValue("at", host.Time.GetUtcNow());
            await person.ExecuteNonQueryAsync(ct);
        }

        var entry = host.EnterExclusionAsync(adminId, TargetEmail, ct);
        await host.Time.WaitUntilAsync(
            () => entry.IsCompleted || host.ScalarAsync<long>(
                "SELECT count(*) FROM pg_locks WHERE locktype = 'advisory' AND NOT granted", ct).GetAwaiter().GetResult() > 0,
            "the entry waiting on the synchronization's shared fence",
            ct);
        Assert.False(entry.IsCompleted, "The entry did not wait for the course transaction holding the shared fence.");
        await transaction.CommitAsync(ct);

        var result = await entry;

        Assert.Equal(ExclusionEntryOutcome.AlreadyLoaded, result.Outcome);
        Assert.Empty(await host.ExclusionRowsAsync(ct));
        Assert.Empty(await host.ErasureAuditRowsAsync(ct));
    }
}
