using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;
using static ClassroomAgent.Tests.TestInfrastructure.MeetLinkRows;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-032 db-design §2, §3, §4, §8 and §10 against the migrated database (TC-2): migration <c>AddMeetingCodeLinks</c>
/// exists; <c>meeting_code_link</c> holds exactly the columns of §2.1 and the keys, indexes and check constraints of
/// §2.2 and §2.3, each rejecting a violating row by its own name and accepting the legal shapes; the unique code and
/// the RESTRICT foreign key hold; <c>audit_event</c> gains three columns, six action codes and four constraints
/// (§3.2, §3.3) that accept the legal rows of the §3.2 table and reject each violation; <c>sync_state</c> accepts the
/// step <c>linking</c> on a failed row only; and <c>ix_classroom_participant_email_lower</c> is on <c>lower(email)</c>.
/// </summary>
public sealed class MeetingCodeLinkSchemaTests(PostgreSqlFixture database)
{
    private const string Table = "meeting_code_link";

    private const string Code = "abc-0001-xyz";

    private const string UniqueCode = "uq_meeting_code_link_meeting_code";

    private const string CourseForeignKey = "fk_meeting_code_link_course_id";

    private const string CourseIndex = "ix_meeting_code_link_course_id";

    private const string StateCheck = "ck_meeting_code_link_state";

    private const string MakerCheck = "ck_meeting_code_link_maker";

    private const string ConfirmationCheck = "ck_meeting_code_link_confirmation";

    private const string ValuesCheck = "ck_meeting_code_link_values";

    private const string AuditShape = "ck_audit_event_meet_code_shape";

    private const string AuditAbsent = "ck_audit_event_meet_code_absent";

    private const string AuditValues = "ck_audit_event_meet_code_values";

    private const string AuditPurgeLinks = "ck_audit_event_purge_meet_code_links";

    private const string SyncFailedStep = "ck_sync_state_failed_step";

    private const string InsertAuditRow =
        """
        INSERT INTO audit_event (occurred_at, actor_type, actor_id, actor_role, action, target_type, target_id,
                                 outcome, refusal_category, request_id, meet_code, meet_previous_course_id,
                                 purged_courses, purged_leaver_memberships, purged_participants, purged_accounts,
                                 purged_audit_rows, purged_meet_sessions, purged_meet_participations,
                                 purged_meet_code_links, created_at, updated_at)
        VALUES (now(), @actorType, @actorId, @actorRole, @action, @targetType, @targetId, @outcome, @refusal,
                @requestId, @meetCode, @previous, @counts, @counts, @counts, @counts, @counts, @meetCounts,
                @meetCounts, @purgedLinks, now(), now())
        """;

    private const string InsertSyncState =
        """
        INSERT INTO sync_state (singleton, status, run_id, started_at, finished_at, processed_count, last_error,
                                last_successful_run_at, meet_loaded_up_to, failed_step, created_at, updated_at)
        VALUES (true, @status, @runId, @at, @at, 0, @lastError, @lastSuccessfulRunAt, NULL, @failedStep, @at, @at)
        """;

    private static readonly DateTimeOffset At = new(2026, 9, 16, 9, 0, 0, TimeSpan.Zero);

    // ---------------------------------------------------------------- migration (db-design §8)

    [Fact]
    public async Task TheMigrationHistory_ContainsAddMeetingCodeLinks()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var migrations = await host.QueryAsync(
            "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\"",
            r => r.GetString(0),
            ct);

        Assert.Contains(migrations, m => m.EndsWith("_AddMeetingCodeLinks", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- columns (db-design §2.1)

    [Fact]
    public async Task MeetingCodeLink_HasExactlyTheDesignedColumns()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await ColumnsAsync(host, Table, ct);

        Assert.Equal(
            new[]
            {
                "id bigint - NO",
                "meeting_code character varying 64 NO",
                "course_id bigint - YES",
                "linked_automatically boolean - YES",
                "linked_by_app_user_id bigint - YES",
                "linked_at timestamp with time zone - YES",
                "confirmed_by_app_user_id bigint - YES",
                "confirmed_at timestamp with time zone - YES",
                "marked_by_app_user_id bigint - YES",
                "marked_at timestamp with time zone - YES",
                "concurrency_stamp character varying 64 NO",
                "created_at timestamp with time zone - NO",
                "updated_at timestamp with time zone - NO",
            }.Order(StringComparer.Ordinal),
            columns);
    }

    // ---------------------------------------------------------------- keys, indexes (db-design §2.2, §2.3)

    [Fact]
    public async Task MeetingCodeLink_HasItsPrimaryKeyUniqueForeignKeyAndCheckNames()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.Equal("pk_meeting_code_link", await InstallationSchemaQueries.PrimaryKeyAsync(host, Table, ct));
        Assert.Equal(new[] { CourseForeignKey }, await InstallationSchemaQueries.ForeignKeysAsync(host, Table, ct));
        Assert.Equal(
            new[] { ConfirmationCheck, MakerCheck, StateCheck, ValuesCheck },
            await InstallationSchemaQueries.CheckConstraintsAsync(host, Table, ct));
        Assert.Equal(
            new[] { UniqueCode },
            await host.QueryAsync(
                "SELECT conname::text FROM pg_constraint WHERE conrelid = 'meeting_code_link'::regclass AND contype = 'u'",
                r => r.GetString(0),
                ct));
    }

    [Fact]
    public async Task MeetingCodeLink_HasExactlyTheDesignedIndexes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.Equal(
            new[] { CourseIndex, "pk_meeting_code_link", UniqueCode }.Order(StringComparer.Ordinal),
            await InstallationSchemaQueries.IndexNamesAsync(host, Table, ct));
        Assert.EndsWith("(course_id)", await IndexDefinitionAsync(host, CourseIndex, ct), StringComparison.Ordinal);
        Assert.EndsWith("(meeting_code)", await IndexDefinitionAsync(host, UniqueCode, ct), StringComparison.Ordinal);
    }

    /// <summary>db-design §2.2, PC-8: the foreign key is RESTRICT; the purge deletes links before the course.</summary>
    [Fact]
    public async Task TheCourseForeignKey_IsRestrict()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var rule = await host.ScalarAsync<string>(
            "SELECT confdeltype::text FROM pg_constraint WHERE conname = @name",
            ct,
            ("name", CourseForeignKey));

        Assert.Equal("r", rule);
    }

    [Fact]
    public async Task ACourse_WithALink_CannotBeDeleted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        await InsertCourseLinkAsync(host, Code, course, At, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(
            () => host.ExecuteAsync("DELETE FROM course WHERE id = @id", ct, ("id", course)));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, failure.SqlState);
        Assert.Equal(CourseForeignKey, failure.ConstraintName);
    }

    [Fact]
    public async Task ALinkToAMissingCourse_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(
            () => InsertCourseLinkAsync(host, Code, 987_654, At, ct));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, failure.SqlState);
        Assert.Equal(CourseForeignKey, failure.ConstraintName);
    }

    // ---------------------------------------------------------------- uniqueness (db-design §2.2)

    [Fact]
    public async Task ASecondRowForTheSameCode_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        await InsertCourseLinkAsync(host, Code, course, At, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertMarkAsync(host, Code, At, ct));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, failure.SqlState);
        Assert.Equal(UniqueCode, failure.ConstraintName);
    }

    [Fact]
    public async Task TwoDifferentCodes_AreAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);

        await InsertCourseLinkAsync(host, Code, course, At, ct);
        await InsertMarkAsync(host, MeetTestData.MeetingCode(2), At, ct);

        Assert.Equal(2L, await host.CountAsync(Table, ct));
    }

    // ---------------------------------------------------------------- check constraints (db-design §2.2)

    /// <summary>The four legal shapes: an automatic link, a person's link, a confirmed automatic link, a mark.</summary>
    [Theory]
    [InlineData("automatic")]
    [InlineData("person")]
    [InlineData("confirmed_automatic")]
    [InlineData("mark")]
    public async Task ALegalShape_IsAccepted(string shape)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var row = shape switch
        {
            "automatic" => LinkRow.Automatic(Code, course, At),
            "person" => LinkRow.ByPerson(Code, course, At),
            "confirmed_automatic" => LinkRow.Automatic(Code, course, At) with { ConfirmedBy = Person, ConfirmedAt = At },
            "mark" => LinkRow.Marked(Code, At),
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };

        await InsertLinkAsync(host, row, ct);

        Assert.Equal(1L, await host.CountAsync(Table, ct));
    }

    /// <summary>One case per rule of the four check constraints; each row breaks that rule and no other.</summary>
    [Theory]
    [InlineData("state_linked_without_linked_at")]
    [InlineData("state_linked_without_flag")]
    [InlineData("state_linked_and_marked")]
    [InlineData("state_unlinked_with_flag")]
    [InlineData("state_marked_without_marker")]
    [InlineData("state_marked_with_confirmation")]
    [InlineData("state_marked_with_linked_by")]
    [InlineData("maker_automatic_with_person")]
    [InlineData("maker_person_without_person")]
    [InlineData("confirmation_by_without_at")]
    [InlineData("confirmation_at_without_by")]
    [InlineData("confirmation_on_persons_link")]
    [InlineData("values_blank_code")]
    [InlineData("values_blank_stamp")]
    public async Task AViolatingShape_IsRejectedByItsOwnConstraint(string violation)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var (row, constraint) = Violation(violation, course);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertLinkAsync(host, row, ct));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(constraint, failure.ConstraintName);
    }

    private static (LinkRow Row, string Constraint) Violation(string name, long course) => name switch
    {
        "state_linked_without_linked_at" => (LinkRow.Automatic(Code, course, At) with { LinkedAt = null }, StateCheck),
        "state_linked_without_flag" => (LinkRow.Automatic(Code, course, At) with { LinkedAutomatically = null }, StateCheck),
        "state_linked_and_marked" => (LinkRow.Automatic(Code, course, At) with { MarkedBy = Person, MarkedAt = At }, StateCheck),
        "state_unlinked_with_flag" => (LinkRow.Marked(Code, At) with { LinkedAutomatically = true }, StateCheck),
        "state_marked_without_marker" => (LinkRow.Marked(Code, At) with { MarkedBy = null, MarkedAt = null }, StateCheck),
        "state_marked_with_confirmation" => (LinkRow.Marked(Code, At) with { ConfirmedBy = Person, ConfirmedAt = At }, StateCheck),
        "state_marked_with_linked_by" => (LinkRow.Marked(Code, At) with { LinkedBy = Person }, StateCheck),
        "maker_automatic_with_person" => (LinkRow.Automatic(Code, course, At) with { LinkedBy = Person }, MakerCheck),
        "maker_person_without_person" => (LinkRow.ByPerson(Code, course, At) with { LinkedBy = null }, MakerCheck),
        "confirmation_by_without_at" => (LinkRow.Automatic(Code, course, At) with { ConfirmedBy = Person }, ConfirmationCheck),
        "confirmation_at_without_by" => (LinkRow.Automatic(Code, course, At) with { ConfirmedAt = At }, ConfirmationCheck),
        "confirmation_on_persons_link" => (LinkRow.ByPerson(Code, course, At) with { ConfirmedBy = Person, ConfirmedAt = At }, ConfirmationCheck),
        "values_blank_code" => (LinkRow.Automatic(string.Empty, course, At), ValuesCheck),
        "values_blank_stamp" => (LinkRow.Automatic(Code, course, At) with { Stamp = string.Empty }, ValuesCheck),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    // ---------------------------------------------------------------- classroom_participant (db-design §5.2)

    [Fact]
    public async Task TheLowerEmailIndex_IsOnLowerOfEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var definition = await IndexDefinitionAsync(host, "ix_classroom_participant_email_lower", ct);

        Assert.Contains("lower(", definition, StringComparison.Ordinal);
        Assert.Contains("email", definition, StringComparison.Ordinal);
        Assert.DoesNotContain("UNIQUE", definition, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- audit_event columns (db-design §3.2)

    [Fact]
    public async Task AuditEvent_GainsTheThreeNullableColumns()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await ColumnsAsync(host, "audit_event", ct);

        Assert.Contains("meet_code character varying 64 YES", columns);
        Assert.Contains("meet_previous_course_id bigint - YES", columns);
        Assert.Contains("purged_meet_code_links integer - YES", columns);
    }

    // ---------------------------------------------------------------- audit_event legal rows (db-design §3.1, §3.2)

    /// <summary>Each of the six new action codes is on <c>ck_audit_event_action</c>, in the legal shape of its table row.</summary>
    [Theory]
    [InlineData("meet_code_auto_linked")]
    [InlineData("meet_code_course_picked")]
    [InlineData("meet_code_link_confirmed")]
    [InlineData("meet_code_relinked")]
    [InlineData("meet_code_marked_not_a_course")]
    [InlineData("meet_code_mark_removed")]
    public async Task EachNewActionCode_IsAccepted_InItsLegalShape(string action)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertAuditAsync(host, LegalFor(action), ct);

        Assert.Equal(1L, await host.CountAsync("audit_event", ct, "action = @action", ("action", action)));
    }

    /// <summary>The other legal shapes of the §3.2 table: a mark on an unassigned code, and the read-only refusals.</summary>
    [Theory]
    [InlineData("mark_of_an_unassigned_code")]
    [InlineData("refused_with_code")]
    [InlineData("refused_without_code")]
    [InlineData("refused_relink_with_code")]
    [InlineData("refused_mark_with_code")]
    public async Task AnotherLegalAuditShape_IsAccepted(string shape)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var refused = new Audit("meet_code_course_picked")
        {
            Outcome = "refused",
            Refusal = "read_only_mode",
            TargetType = null,
            TargetId = null,
        };
        var row = shape switch
        {
            "mark_of_an_unassigned_code" => LegalFor("meet_code_marked_not_a_course") with { Previous = null },
            "refused_with_code" => refused,
            "refused_without_code" => refused with { MeetCode = null },
            "refused_relink_with_code" => refused with { Action = "meet_code_relinked" },
            "refused_mark_with_code" => refused with { Action = "meet_code_marked_not_a_course" },
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };

        await InsertAuditAsync(host, row, ct);

        Assert.Equal(1L, await host.CountAsync("audit_event", ct));
    }

    // ---------------------------------------------------------------- audit_event violations (db-design §3.3)

    /// <summary>One case per rule of <c>ck_audit_event_meet_code_shape</c>, in the order the design lists them.</summary>
    [Theory]
    [InlineData("system_actor_on_a_person_action")]
    [InlineData("person_actor_on_the_automatic_action")]
    [InlineData("automatic_with_a_request_id")]
    [InlineData("refusal_category_other_than_read_only")]
    [InlineData("target_type_other_than_course")]
    [InlineData("target_type_without_target_id")]
    [InlineData("refused_with_a_target")]
    [InlineData("refused_with_a_previous_course")]
    [InlineData("succeeded_without_a_code")]
    [InlineData("succeeded_pick_without_a_target")]
    [InlineData("succeeded_mark_with_a_target")]
    [InlineData("previous_course_on_another_action")]
    [InlineData("relink_without_a_previous_course")]
    public async Task AViolationOfTheShapeRule_IsRejected(string violation)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var picked = LegalFor("meet_code_course_picked");
        var refused = picked with { Outcome = "refused", Refusal = "read_only_mode", TargetType = null, TargetId = null };
        var row = violation switch
        {
            "system_actor_on_a_person_action" => picked with { ActorType = "system", ActorId = null, ActorRole = null },
            "person_actor_on_the_automatic_action" => LegalFor("meet_code_auto_linked") with
            {
                ActorType = "app_user",
                ActorId = 1,
                ActorRole = "dean",
            },
            "automatic_with_a_request_id" => LegalFor("meet_code_auto_linked") with { RequestId = "request-id" },
            "refusal_category_other_than_read_only" => refused with { Refusal = "connection_not_usable" },
            "target_type_other_than_course" => picked with { TargetType = "app_user" },
            "target_type_without_target_id" => refused with { TargetType = "course" },
            "refused_with_a_target" => refused with { TargetType = "course", TargetId = 5 },
            "refused_with_a_previous_course" => refused with { Action = "meet_code_relinked", Previous = 4 },
            "succeeded_without_a_code" => picked with { MeetCode = null },
            "succeeded_pick_without_a_target" => picked with { TargetType = null, TargetId = null },
            "succeeded_mark_with_a_target" => LegalFor("meet_code_marked_not_a_course") with { TargetType = "course", TargetId = 5 },
            "previous_course_on_another_action" => picked with { Previous = 4 },
            "relink_without_a_previous_course" => LegalFor("meet_code_relinked") with { Previous = null },
            _ => throw new ArgumentOutOfRangeException(nameof(violation)),
        };

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertAuditAsync(host, row, ct));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(AuditShape, failure.ConstraintName);
    }

    /// <summary>db-design §3.3: the code and the previous course belong to the six actions only.</summary>
    [Theory]
    [InlineData("code")]
    [InlineData("previous_course")]
    public async Task ACodeOrPreviousCourse_OnAnotherAction_IsRejected(string column)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var other = new Audit("workspace_connection_saved")
        {
            TargetType = "workspace_connection",
            TargetId = 1,
            MeetCode = column == "code" ? Code : null,
            Previous = column == "previous_course" ? 4 : null,
        };

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertAuditAsync(host, other, ct));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(AuditAbsent, failure.ConstraintName);
    }

    [Theory]
    [InlineData("blank_code")]
    [InlineData("previous_zero")]
    [InlineData("previous_negative")]
    public async Task ABlankCodeOrANonPositivePreviousCourse_IsRejected(string violation)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var row = violation switch
        {
            "blank_code" => LegalFor("meet_code_course_picked") with { MeetCode = string.Empty },
            "previous_zero" => LegalFor("meet_code_relinked") with { Previous = 0 },
            "previous_negative" => LegalFor("meet_code_relinked") with { Previous = -3 },
            _ => throw new ArgumentOutOfRangeException(nameof(violation)),
        };

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertAuditAsync(host, row, ct));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(AuditValues, failure.ConstraintName);
    }

    // ---------------------------------------------------------------- audit_event purge count (db-design §3.3)

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task APurgeRow_WithTheLinkCount_IsAccepted(int links)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertAuditAsync(host, PurgeRow() with { PurgedLinks = links }, ct);

        Assert.Equal(1L, await host.CountAsync("audit_event", ct, "purged_meet_code_links = @links", ("links", links)));
    }

    /// <summary>db-design §3.3: a purge row written before this Story carries no link count and stays valid.</summary>
    [Fact]
    public async Task APurgeRow_WithoutTheLinkCount_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertAuditAsync(host, PurgeRow() with { PurgedLinks = null }, ct);

        Assert.Equal(1L, await host.CountAsync("audit_event", ct, "purged_meet_code_links IS NULL"));
    }

    [Fact]
    public async Task ANegativeLinkCount_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(
            () => InsertAuditAsync(host, PurgeRow() with { PurgedLinks = -1 }, ct));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(AuditPurgeLinks, failure.ConstraintName);
    }

    [Fact]
    public async Task TheLinkCount_OnAnotherAction_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var other = new Audit("workspace_connection_saved")
        {
            TargetType = "workspace_connection",
            TargetId = 1,
            MeetCode = null,
            PurgedLinks = 1,
        };

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertAuditAsync(host, other, ct));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(AuditPurgeLinks, failure.ConstraintName);
    }

    // ---------------------------------------------------------------- sync_state (db-design §4)

    [Fact]
    public async Task AFailedRow_WithTheLinkingStep_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertSyncStateAsync(host, "failed", "ScopeNotAuthorized", "linking", null, ct);

        Assert.Equal(1L, await host.CountAsync("sync_state", ct, "failed_step = 'linking'"));
    }

    [Fact]
    public async Task ACompletedRow_WithTheLinkingStep_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(
            () => InsertSyncStateAsync(host, "completed", null, "linking", At, ct));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(SyncFailedStep, failure.ConstraintName);
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>The legal row of db-design §3.2 for one of the six actions: a person's, or the system's for the automatic one.</summary>
    private static Audit LegalFor(string action) => action switch
    {
        "meet_code_auto_linked" => new Audit(action) { ActorType = "system", ActorId = null, ActorRole = null, RequestId = null },
        "meet_code_relinked" => new Audit(action) { Previous = 4 },
        "meet_code_marked_not_a_course" => new Audit(action) { TargetType = null, TargetId = null, Previous = 4 },
        _ => new Audit(action),
    };

    private static Audit PurgeRow() => new("retention_purge_run")
    {
        ActorType = "system",
        ActorId = null,
        ActorRole = null,
        TargetType = null,
        TargetId = null,
        RequestId = null,
        MeetCode = null,
        Counts = 1,
        MeetCounts = 1,
        PurgedLinks = 1,
    };

    private static Task<int> InsertAuditAsync(InstallationTestHost host, Audit row, CancellationToken cancellationToken) =>
        host.ExecuteAsync(
            InsertAuditRow,
            cancellationToken,
            ("actorType", row.ActorType),
            ("actorId", row.ActorId),
            ("actorRole", row.ActorRole),
            ("action", row.Action),
            ("targetType", row.TargetType),
            ("targetId", row.TargetId),
            ("outcome", row.Outcome),
            ("refusal", row.Refusal),
            ("requestId", row.RequestId),
            ("meetCode", row.MeetCode),
            ("previous", row.Previous),
            ("counts", row.Counts),
            ("meetCounts", row.MeetCounts),
            ("purgedLinks", row.PurgedLinks));

    private static Task<int> InsertSyncStateAsync(
        InstallationTestHost host,
        string status,
        string? lastError,
        string failedStep,
        DateTimeOffset? lastSuccessfulRunAt,
        CancellationToken cancellationToken) =>
        host.ExecuteAsync(
            InsertSyncState,
            cancellationToken,
            ("status", status),
            ("runId", Guid.NewGuid()),
            ("at", At),
            ("lastError", lastError),
            ("lastSuccessfulRunAt", lastSuccessfulRunAt),
            ("failedStep", failedStep));

    /// <summary>"name type length nullable" per column, sorted ordinally; "-" where there is no length.</summary>
    private static async Task<IReadOnlyList<string>> ColumnsAsync(
        InstallationTestHost host,
        string table,
        CancellationToken cancellationToken)
    {
        var rows = await host.QueryAsync(
            """
            SELECT column_name, data_type, character_maximum_length, is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @table
            """,
            r => $"{r.GetString(0)} {r.GetString(1)} {(r.IsDBNull(2) ? "-" : r.GetInt32(2).ToString(System.Globalization.CultureInfo.InvariantCulture))} {r.GetString(3)}",
            cancellationToken,
            ("table", table));
        return rows.Order(StringComparer.Ordinal).ToList();
    }

    private static async Task<string> IndexDefinitionAsync(
        InstallationTestHost host,
        string name,
        CancellationToken cancellationToken) =>
        await host.ScalarAsync<string>(
            "SELECT indexdef FROM pg_indexes WHERE schemaname = 'public' AND indexname = @name",
            cancellationToken,
            ("name", name))
        ?? throw new Xunit.Sdk.XunitException($"Index {name} does not exist.");

    /// <summary>An <c>audit_event</c> row as offered to the database; the defaults are a person's succeeded change.</summary>
    private sealed record Audit(string Action)
    {
        public string ActorType { get; init; } = "app_user";

        public long? ActorId { get; init; } = 1;

        public string? ActorRole { get; init; } = "dean";

        public string? TargetType { get; init; } = "course";

        public long? TargetId { get; init; } = 5;

        public string Outcome { get; init; } = "succeeded";

        public string? Refusal { get; init; }

        public string? RequestId { get; init; } = "request-id";

        public string? MeetCode { get; init; } = Code;

        public long? Previous { get; init; }

        public int? Counts { get; init; }

        public int? MeetCounts { get; init; }

        public int? PurgedLinks { get; init; }
    }
}
