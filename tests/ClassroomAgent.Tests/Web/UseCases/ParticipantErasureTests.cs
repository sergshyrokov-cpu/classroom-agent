using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureHostExtensions;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-038 AC-002, AC-006, AC-009, AC-012, AC-015, AC-018, AC-019 and FR-005 … FR-008, FR-016, FR-017 through the host's
/// composition root and real PostgreSQL (TC-2): the erasure use case resolved from the container, the world seeded by
/// SQL, every effect read back by SQL.
/// </summary>
public sealed class ParticipantErasureTests(PostgreSqlFixture database)
{
    private async Task<InstallationTestHost> StartAsync(CancellationToken ct, ReadOnlyModeHost.Cause cause = ReadOnlyModeHost.Cause.NotReadOnly)
    {
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Anonymous, ct, cause);
        client.Dispose();
        return host;
    }

    private static Task<long> CountAsync(InstallationTestHost host, string sql, CancellationToken ct, params (string, object?)[] parameters) =>
        host.ScalarAsync<long>(sql, ct, parameters);

    /// <summary>AC-002, FR-005: the participant, their memberships, submissions and Meet participations are gone.</summary>
    [Fact]
    public async Task TheErasure_DeletesTheParticipant_TheirMemberships_SubmissionsAndMeetParticipations()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var world = await host.SeedErasureWorldAsync(ct);
        var admin = await host.InsertAdminAsync(ct);

        var result = await host.EraseAsync(admin, world.Target, ct);

        Assert.Equal(ParticipantErasureOutcome.Erased, result.Outcome);
        Assert.Equal(0, await CountAsync(host, "SELECT count(*) FROM classroom_participant WHERE id = @id", ct, ("id", world.Target)));
        Assert.Equal(0, await CountAsync(host, "SELECT count(*) FROM course_membership WHERE participant_id = @id", ct, ("id", world.Target)));
        Assert.Equal(0, await CountAsync(host, "SELECT count(*) FROM submission WHERE participant_id = @id", ct, ("id", world.Target)));
        Assert.Equal(0, await CountAsync(
            host, "SELECT count(*) FROM meet_participation WHERE lower(email) = @email", ct, ("email", TargetEmail)));
    }

    /// <summary>AC-002, AC-012, FR-006: a Meet participation stored in another letter case is erased too.</summary>
    [Fact]
    public async Task AMeetParticipationInAnotherLetterCase_IsErased()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var world = await host.SeedErasureWorldAsync(ct);

        await host.EraseAsync(await host.InsertAdminAsync(ct), world.Target, ct);

        Assert.Equal(0, await CountAsync(
            host, "SELECT count(*) FROM meet_participation WHERE email = @email", ct, ("email", TargetEmailOtherCase)));
    }

    /// <summary>AC-002, FR-007: the meeting they organized remains, without the organizer email; its other connection stays.</summary>
    [Fact]
    public async Task TheMeetingTheyOrganized_RemainsWithoutTheOrganizerEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var world = await host.SeedErasureWorldAsync(ct);

        await host.EraseAsync(await host.InsertAdminAsync(ct), world.Target, ct);

        Assert.Equal(1, await CountAsync(host, "SELECT count(*) FROM meet_session WHERE id = @id AND organizer_email IS NULL", ct, ("id", world.MeetingByTarget)));
        Assert.Equal(1, await CountAsync(
            host, "SELECT count(*) FROM meet_participation WHERE meet_session_id = @id AND email = @email", ct,
            ("id", world.MeetingByTarget), ("email", OtherEmail)));
    }

    /// <summary>AC-002, FR-007: the course they owned remains, without the owner; the other courses keep theirs.</summary>
    [Fact]
    public async Task TheCourseTheyOwned_RemainsWithoutTheOwner()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var world = await host.SeedErasureWorldAsync(ct);

        await host.EraseAsync(await host.InsertAdminAsync(ct), world.Target, ct);

        Assert.Null(await host.ScalarAsync<string>("SELECT owner_google_id FROM course WHERE id = @id", ct, ("id", world.Chemistry)));
        Assert.Equal(TeacherGoogleUserId, await host.ScalarAsync<string>("SELECT owner_google_id FROM course WHERE id = @id", ct, ("id", world.Physics)));
        Assert.Equal(OtherGoogleUserId, await host.ScalarAsync<string>("SELECT owner_google_id FROM course WHERE id = @id", ct, ("id", world.History)));
        Assert.Equal(3, await CountAsync(host, "SELECT count(*) FROM course", ct));
        Assert.Equal(2, await CountAsync(host, "SELECT count(*) FROM course_work", ct));
    }

    /// <summary>AC-002, FR-005: other people's data is unchanged, row by row.</summary>
    [Fact]
    public async Task OtherPeoplesData_IsUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var world = await host.SeedErasureWorldAsync(ct);
        const string others = """
            SELECT md5(string_agg(x, '|' ORDER BY x)) FROM (
              SELECT row_to_json(p)::text AS x FROM classroom_participant p WHERE p.id <> @target
              UNION ALL SELECT row_to_json(m)::text FROM course_membership m WHERE m.participant_id <> @target
              UNION ALL SELECT row_to_json(s)::text FROM submission s WHERE s.participant_id <> @target
              UNION ALL SELECT row_to_json(q)::text FROM meet_participation q WHERE q.email IS NULL OR lower(q.email) <> @email
              UNION ALL SELECT row_to_json(t)::text FROM meet_session t WHERE t.id <> @organized
            ) rows
            """;
        (string, object?)[] parameters = [("target", world.Target), ("email", TargetEmail), ("organized", world.MeetingByTarget)];
        var before = await host.ScalarAsync<string>(others, ct, parameters);

        await host.EraseAsync(await host.InsertAdminAsync(ct), world.Target, ct);

        Assert.Equal(before, await host.ScalarAsync<string>(others, ct, parameters));
        Assert.Equal(Expected.OtherMeetParticipations, await CountAsync(
            host, "SELECT count(*) FROM meet_participation WHERE email = @email", ct, ("email", OtherEmail)));
    }

    /// <summary>AC-002, FR-004: a failure inside the transaction erases nothing and leaves no exclusion and no audit row.</summary>
    [Fact]
    public async Task AFailureInsideTheTransaction_ErasesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var world = await host.SeedErasureWorldAsync(ct);
        var admin = await host.InsertAdminAsync(ct);
        var before = await host.ErasureFingerprintAsync(ct);
        await host.FailDeletesAsync("classroom_participant", ct);

        await Assert.ThrowsAnyAsync<Exception>(() => host.EraseAsync(admin, world.Target, ct));

        Assert.Equal(before, await host.ErasureFingerprintAsync(ct));
        Assert.Empty(await host.ExclusionRowsAsync(ct));
        Assert.Empty(await host.ErasureAuditRowsAsync(ct));
    }

    /// <summary>AC-006, FR-008: one exclusion — the Google user id, the fingerprint with the stored salt, the date.</summary>
    [Fact]
    public async Task TheExclusion_HoldsTheGoogleUserId_TheFingerprint_AndTheErasureDate()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var world = await host.SeedErasureWorldAsync(ct);

        await host.EraseAsync(await host.InsertAdminAsync(ct), world.Target, ct);

        var exclusion = Assert.Single(await host.ExclusionRowsAsync(ct));
        var salt = Assert.Single(await host.SaltRowsAsync(ct));
        Assert.Equal(32, salt.Length);
        Assert.Equal(TargetGoogleUserId, exclusion.GoogleUserId);
        Assert.Equal(ErasureTestData.Fingerprint(TargetEmail, salt), exclusion.EmailFingerprint);
        Assert.Equal(host.Time.GetUtcNow(), exclusion.ErasedAt);
    }

    /// <summary>AC-006, S-05: the exclusion table has no column for a name or an email, and no row carries the email.</summary>
    [Fact]
    public async Task TheExclusionTable_HoldsNoNameAndNoEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var world = await host.SeedErasureWorldAsync(ct);

        await host.EraseAsync(await host.InsertAdminAsync(ct), world.Target, ct);

        Assert.Equal(
            ["id", "google_user_id", "email_fingerprint", "erased_at", "created_at", "updated_at"],
            await host.ColumnNamesAsync(ErasureTestData.Names.ExclusionTable, ct));
        var text = Assert.Single(await host.QueryAsync("SELECT row_to_json(e)::text FROM erased_participant e", r => r.GetString(0), ct));
        Assert.DoesNotContain("koval", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(TargetSurname, text, StringComparison.Ordinal);
    }

    /// <summary>FR-010, db-design §3.3: the first erasure creates the salt; the next one reuses it.</summary>
    [Fact]
    public async Task TheSalt_IsCreatedOnce_AndReused()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var world = await host.SeedErasureWorldAsync(ct);
        var admin = await host.InsertAdminAsync(ct);
        Assert.Empty(await host.SaltRowsAsync(ct));

        await host.EraseAsync(admin, world.Target, ct);
        var first = Assert.Single(await host.SaltRowsAsync(ct));
        await host.EraseAsync(admin, world.Other, ct);

        Assert.Equal(first, Assert.Single(await host.SaltRowsAsync(ct)));
        Assert.All(await host.ExclusionRowsAsync(ct), e => Assert.Equal(32, e.EmailFingerprint!.Length));
        var other = (await host.ExclusionRowsAsync(ct)).Single(e => e.GoogleUserId == OtherGoogleUserId);
        Assert.Equal(ErasureTestData.Fingerprint(OtherEmail, first), other.EmailFingerprint);
    }

    /// <summary>AC-006: two installations — the same email, different fingerprints.</summary>
    [Fact]
    public async Task TwoInstallations_GiveTheSameEmailDifferentFingerprints()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var first = await StartAsync(ct);
        await using var second = await StartAsync(ct);
        var firstWorld = await first.SeedErasureWorldAsync(ct);
        var secondWorld = await second.SeedErasureWorldAsync(ct);

        await first.EraseAsync(await first.InsertAdminAsync(ct), firstWorld.Target, ct);
        await second.EraseAsync(await second.InsertAdminAsync(ct), secondWorld.Target, ct);

        Assert.NotEqual(
            Assert.Single(await first.ExclusionRowsAsync(ct)).EmailFingerprint,
            Assert.Single(await second.ExclusionRowsAsync(ct)).EmailFingerprint);
    }

    /// <summary>AC-009, FR-013: one succeeded audit row with the internal id and the count of each kind.</summary>
    [Fact]
    public async Task TheErasure_WritesOneAuditRow_WithTheInternalIdAndTheCounts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var world = await host.SeedErasureWorldAsync(ct);
        var admin = await host.InsertAdminAsync(ct);

        var result = await host.EraseAsync(admin, world.Target, ct);

        var row = Assert.Single(await host.ErasureAuditRowsAsync(ct));
        Assert.Equal(
            new ErasureAuditRow(
                ErasureTestData.Codes.ErasedAction,
                ErasureTestData.Codes.AppUserActor,
                admin,
                ErasureTestData.Codes.AdminRole,
                ErasureTestData.Codes.ParticipantTarget,
                world.Target,
                ErasureTestData.Codes.Succeeded,
                null,
                Expected.ErasedMemberships,
                Expected.ErasedSubmissions,
                Expected.ErasedMeetParticipations,
                Expected.ClearedMeetOrganizers,
                Expected.ClearedCourseOwners),
            row);
        Assert.NotNull(result.Counts);
        Assert.Equal(
            (Expected.ErasedMemberships, Expected.ErasedSubmissions, Expected.ErasedMeetParticipations, Expected.ClearedMeetOrganizers, Expected.ClearedCourseOwners),
            (result.Counts.Memberships, result.Counts.Submissions, result.Counts.MeetParticipations, result.Counts.ClearedMeetOrganizers, result.Counts.ClearedCourseOwners));
    }

    /// <summary>AC-009, S-06: no audit row carries a name, an email, the Google user id or the fingerprint.</summary>
    [Fact]
    public async Task NoAuditRow_CarriesPersonalData()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var world = await host.SeedErasureWorldAsync(ct);

        await host.EraseAsync(await host.InsertAdminAsync(ct), world.Target, ct);

        var fingerprint = Convert.ToHexString(Assert.Single(await host.ExclusionRowsAsync(ct)).EmailFingerprint!);
        foreach (var json in await host.AuditRowsAsJsonAsync(ct))
        {
            foreach (var value in TargetPersonalValues)
            {
                Assert.DoesNotContain(value, json, StringComparison.OrdinalIgnoreCase);
            }

            Assert.DoesNotContain(fingerprint, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>AC-019, OD-001 a: no email — erased, no Meet row deleted or cleared, exclusion without fingerprint.</summary>
    [Fact]
    public async Task AParticipantWithNoEmail_IsErased_AndNoMeetRowIsTouched()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var world = await host.SeedErasureWorldAsync(ct);
        var nameless = await host.InsertPersonAsync(9, null, null, null, ct);
        await host.AddStudentAsync(world.Physics, nameless, ct);
        const string meet = "SELECT md5(string_agg(row_to_json(t)::text, '|' ORDER BY t.id)) FROM (SELECT id, organizer_email AS x FROM meet_session UNION ALL SELECT id, email FROM meet_participation) t";
        var before = await host.ScalarAsync<string>(meet, ct);

        var result = await host.EraseAsync(await host.InsertAdminAsync(ct), nameless, ct);

        Assert.Equal(ParticipantErasureOutcome.Erased, result.Outcome);
        Assert.Equal(before, await host.ScalarAsync<string>(meet, ct));
        var exclusion = Assert.Single(await host.ExclusionRowsAsync(ct));
        Assert.Equal(CourseTestData.UserId(9), exclusion.GoogleUserId);
        Assert.Null(exclusion.EmailFingerprint);
        Assert.Equal((0, 0), (result.Counts!.MeetParticipations, result.Counts.ClearedMeetOrganizers));
    }

    /// <summary>AC-015, FR-016: an unknown id — not found, nothing changes, no audit row.</summary>
    [Fact]
    public async Task AnUnknownId_IsNotFound_AndNothingChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        await host.SeedErasureWorldAsync(ct);
        var admin = await host.InsertAdminAsync(ct);
        var before = await host.ErasureFingerprintAsync(ct);

        var result = await host.EraseAsync(admin, 987_654, ct);

        Assert.Equal(ParticipantErasureOutcome.ParticipantNotFound, result.Outcome);
        Assert.Equal(before, await host.ErasureFingerprintAsync(ct));
    }

    /// <summary>FR-016: an id erased already — not found the second time; still one exclusion and one audit row.</summary>
    [Fact]
    public async Task ASecondErasureOfTheSameId_IsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var world = await host.SeedErasureWorldAsync(ct);
        var admin = await host.InsertAdminAsync(ct);
        await host.EraseAsync(admin, world.Target, ct);

        var second = await host.EraseAsync(admin, world.Target, ct);

        Assert.Equal(ParticipantErasureOutcome.ParticipantNotFound, second.Outcome);
        Assert.Single(await host.ExclusionRowsAsync(ct));
        Assert.Single(await host.ErasureAuditRowsAsync(ct));
    }

    /// <summary>FR-016, db-design §7: two concurrent erasures of one participant — one succeeds, the other is not found.</summary>
    [Fact]
    public async Task TwoConcurrentErasures_OneSucceeds_TheOtherIsNotFound_AndOneExclusionExists()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var world = await host.SeedErasureWorldAsync(ct);
        var admin = await host.InsertAdminAsync(ct);

        var results = await Task.WhenAll(
            Task.Run(() => host.EraseAsync(admin, world.Target, ct), ct),
            Task.Run(() => host.EraseAsync(admin, world.Target, ct), ct));

        Assert.Equal(
            [ParticipantErasureOutcome.Erased, ParticipantErasureOutcome.ParticipantNotFound],
            results.Select(r => r.Outcome).Order());
        Assert.Single(await host.ExclusionRowsAsync(ct));
        Assert.Single(await host.ErasureAuditRowsAsync(ct));
    }

    /// <summary>AC-008, AC-017, FR-014: in read-only mode the erasure is refused, nothing changes but one refused audit row.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheErasureIsRefused_AndOnlyTheRefusalIsAudited(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct, cause);
        var world = await host.SeedErasureWorldAsync(ct);
        var admin = await host.InsertAdminAsync(ct);
        var before = await host.ErasureFingerprintAsync(ct);

        await Assert.ThrowsAsync<ReadOnlyModeException>(() => host.EraseAsync(admin, world.Target, ct));

        var row = Assert.Single(await host.ErasureAuditRowsAsync(ct));
        Assert.Equal(
            (ErasureTestData.Codes.ErasedAction, ErasureTestData.Codes.Refused, ErasureTestData.Codes.ReadOnlyCategory, (long?)world.Target),
            (row.Action, row.Outcome, row.RefusalCategory, row.TargetId));
        Assert.Null(row.ErasedMemberships);
        var after = await host.ErasureFingerprintAsync(ct);
        Assert.Equal(Without(before, "audit"), Without(after, "audit"));
    }

    /// <summary>AC-018, FR-017: the retention purge leaves exclusions untouched, however old.</summary>
    [Fact]
    public async Task TheRetentionPurge_LeavesExclusionsUntouched()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        await host.WaitForPurgeRunsAsync(1, ct);
        await host.SeedExclusionAsync(TargetGoogleUserId, TargetEmail, ct, RetentionPurgeTestData.Old.AddYears(-10));
        var before = await host.ExclusionRowsAsync(ct);
        var salts = await host.SaltRowsAsync(ct);

        await host.RunPurgeAsync(ct);

        Assert.Equal(before.Select(e => (e.Id, e.GoogleUserId, Convert.ToHexString(e.EmailFingerprint!))), (await host.ExclusionRowsAsync(ct)).Select(e => (e.Id, e.GoogleUserId, Convert.ToHexString(e.EmailFingerprint!))));
        Assert.Equal(salts, await host.SaltRowsAsync(ct));
    }

    private static string Without(string fingerprint, string part) =>
        string.Join(';', fingerprint.Split(';').Where(p => !p.StartsWith(part + "=", StringComparison.Ordinal)));
}
