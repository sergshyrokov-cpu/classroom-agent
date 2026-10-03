using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-008 db-design 4: the installation's own <c>audit_event</c> table — its columns, its eleven check
/// constraints, its immutability, the deliberate absence of an index and of a foreign key on
/// <c>actor_id</c> (SC-11, PC-6, PC-7, PC-9; TC-2).
/// </summary>
public sealed class InstallationAuditEventSchemaTests(PostgreSqlFixture database)
{
    private const string Insert =
        """
        INSERT INTO audit_event (occurred_at, actor_type, actor_id, actor_role, action, target_type, target_id,
                                 outcome, refusal_category, request_id, created_at, updated_at)
        VALUES (now(), @actorType, @actorId, @actorRole, @action, @targetType, @targetId, @outcome,
                @refusalCategory, 'request-id', now(), now())
        """;

    [Fact]
    public async Task Columns_MatchTheDesign()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await InstallationSchemaQueries.ColumnsAsync(host, "audit_event", ct);

        Assert.Equal(
            new[]
            {
                "action character varying 64 NO -",
                "actor_id bigint - YES -",
                "actor_role character varying 16 YES -",
                "actor_type character varying 16 NO -",
                "created_at timestamp with time zone - NO -",
                "id bigint - NO -",
                "occurred_at timestamp with time zone - NO -",
                "outcome character varying 16 NO -",
                "purged_accounts integer - YES -",
                "purged_audit_rows integer - YES -",
                "purged_courses integer - YES -",
                "purged_leaver_memberships integer - YES -",
                "purged_participants integer - YES -",
                "refusal_category character varying 32 YES -",
                "request_id character varying 128 YES -",
                "target_id bigint - YES -",
                "target_type character varying 32 YES -",
                "updated_at timestamp with time zone - NO -",
            },
            columns);
    }

    /// <summary>db-design 4, SC-11: no column could carry a personal datum — no email, name or free text.</summary>
    [Fact]
    public async Task NoColumn_CouldCarryAPersonalDatum()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await InstallationSchemaQueries.ColumnsAsync(host, "audit_event", ct);

        Assert.DoesNotContain(columns, c => c.Contains("email", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(columns, c => c.Contains("name", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(columns, c => c.Contains("login", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(columns, c => c.Contains(" text ", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(columns, c => c.Contains("detail", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(columns, c => c.Contains("comment", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PrimaryKeyAndConstraints_AreNamedAsTheDesignFixesThem()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var primaryKey = await InstallationSchemaQueries.PrimaryKeyAsync(host, "audit_event", ct);
        var checks = await InstallationSchemaQueries.CheckConstraintsAsync(host, "audit_event", ct);

        Assert.Equal("pk_audit_event", primaryKey);
        Assert.Equal(
            new[]
            {
                "ck_audit_event_action",
                "ck_audit_event_actor_id",
                "ck_audit_event_actor_role",
                "ck_audit_event_actor_role_value",
                "ck_audit_event_actor_type",
                "ck_audit_event_immutable",
                "ck_audit_event_outcome",
                "ck_audit_event_purge_actor",
                "ck_audit_event_purge_counts",
                "ck_audit_event_purge_counts_absent",
                "ck_audit_event_purge_counts_non_negative",
                "ck_audit_event_refusal_category",
                "ck_audit_event_refusal_category_value",
                "ck_audit_event_target",
                "ck_audit_event_target_type_value",
            },
            checks.Where(c => c.StartsWith("ck_audit_event", StringComparison.Ordinal)));
    }

    /// <summary>
    /// db-design 4.3, PC-7: the table was write-only until US-037, whose purge brings the one query that needs an
    /// index — the delete by <c>occurred_at</c> (US-037 db-design §2.5). Nothing else.
    /// </summary>
    [Fact]
    public async Task TheTable_HasOnlyThePrimaryKeyAndThePurgeIndex()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var indexes = await InstallationSchemaQueries.IndexNamesAsync(host, "audit_event", ct);

        Assert.Equal(new[] { "ix_audit_event_occurred_at", "pk_audit_event" }, indexes);
    }

    /// <summary>db-design 4, PC-9: actor_id carries no foreign key — audit rows outlive the accounts they name.</summary>
    [Fact]
    public async Task ActorId_CarriesNoForeignKey()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.Empty(await InstallationSchemaQueries.ForeignKeysAsync(host, "audit_event", ct));
    }

    /// <summary>db-design 4, PC-11: a row may name an account that no longer exists.</summary>
    [Fact]
    public async Task ARowMayNameAnAccountThatDoesNotExist()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertAsync(host, ct, actorType: "app_user", actorId: 987654, actorRole: "admin", outcome: "succeeded");

        var row = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal(987654, row.ActorId);
    }

    /// <summary>db-design 4.1: an account actor always carries its id and role; anonymous never does.</summary>
    [Theory]
    [InlineData("app_user", null, null, "ck_audit_event_actor_id")]
    [InlineData("app_user", 1L, null, "ck_audit_event_actor_role")]
    [InlineData("anonymous", 1L, "admin", "ck_audit_event_actor_id")]
    [InlineData("anonymous", null, "admin", "ck_audit_event_actor_role")]
    public async Task AnInconsistentActor_IsRejected(string actorType, long? actorId, string? actorRole, string constraint)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => InsertAsync(
            host,
            ct,
            actorType: actorType,
            actorId: actorId,
            actorRole: actorRole,
            outcome: "succeeded"));

        Assert.Contains(constraint, Text(failure), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>db-design 4.1: the actor types are the three FR-012 names.</summary>
    [Theory]
    [InlineData("owner")]
    [InlineData("AppUser")]
    [InlineData("service")]
    public async Task AnUnknownActorType_IsRejected(string actorType)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => InsertAsync(
            host,
            ct,
            actorType: actorType,
            actorId: null,
            actorRole: null,
            outcome: "succeeded"));

        Assert.Contains("ck_audit_event_actor_type", Text(failure), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>db-design 4.1, I-11: only the action this Story performs is allowed.</summary>
    [Theory]
    // US-012 performs dean_sign_in now, so the example of "a value no Story performs" moved on to an action
    // of a later Epic. The point of the test is unchanged: the list stays closed.
    [InlineData("sync_started")]
    [InlineData("export")]
    [InlineData("AdminSignIn")]
    public async Task AnActionNoStoryPerforms_IsRejected(string action)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => InsertAsync(
            host,
            ct,
            action: action,
            outcome: "succeeded"));

        Assert.Contains("ck_audit_event_action", Text(failure), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>db-design 4.1: a refusal always says why; a success never does.</summary>
    [Theory]
    [InlineData("succeeded", "not_in_allowed_admin")]
    [InlineData("refused", null)]
    public async Task AnInconsistentOutcomeAndCategory_IsRejected(string outcome, string? category)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => InsertAsync(
            host,
            ct,
            outcome: outcome,
            refusalCategory: category));

        Assert.Contains("ck_audit_event_refusal_category", Text(failure), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>db-design 4.1: the five refusal categories of FR-012, and nothing else.</summary>
    [Theory]
    [InlineData("not_in_allowed_admin")]
    [InlineData("control_plane_unavailable")]
    [InlineData("unknown_installation")]
    [InlineData("callback_failed")]
    [InlineData("account_disabled")]
    public async Task EachDocumentedRefusalCategory_IsAccepted(string category)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertAsync(host, ct, outcome: "refused", refusalCategory: category);

        Assert.Equal(category, Assert.Single(await host.AuditRowsAsync(ct)).RefusalCategory);
    }

    [Theory]
    // US-012 performs wrong_password now (step 3 of the sign-in sequence), so the example moved on.
    [InlineData("password_expired")]
    [InlineData("NotInAllowedAdmin")]
    public async Task AnUndocumentedRefusalCategory_IsRejected(string category)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => InsertAsync(
            host,
            ct,
            outcome: "refused",
            refusalCategory: category));

        Assert.Contains("ck_audit_event_refusal_category_value", Text(failure), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>db-design 4.1: the target columns travel together.</summary>
    [Theory]
    // US-012 added app_user to the closed list of target types, so the unknown-type example moved on to a
    // type a later Epic will add.
    [InlineData("course", null)]
    [InlineData(null, 7L)]
    public async Task AHalfSetTarget_IsRejected(string? targetType, long? targetId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => InsertAsync(
            host,
            ct,
            outcome: "succeeded",
            targetType: targetType,
            targetId: targetId));

        Assert.Contains("ck_audit_event_target", Text(failure), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// db-design 4.2, PC-6: an update that passes through EF Core moves <c>updated_at</c> and is rejected. It is
    /// stated plainly in the name that this does not defend against a hand-written UPDATE which also rewrites
    /// <c>updated_at</c>.
    /// </summary>
    [Fact]
    public async Task AnUpdateThatMovesUpdatedAt_IsRejected_ThoughRawSqlRewritingBothIsNot()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await InsertAsync(host, ct, outcome: "succeeded");
        var row = Assert.Single(await host.AuditRowsAsync(ct));

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => host.ExecuteAsync(
            "UPDATE audit_event SET updated_at = updated_at + interval '1 second' WHERE id = @id",
            ct,
            ("id", row.Id)));

        Assert.Contains("ck_audit_event_immutable", Text(failure), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>db-design 4.1: the "system" actor type exists for later Stories although nothing writes it yet.</summary>
    [Fact]
    public async Task TheSystemActorType_IsAllowedForLaterStories()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertAsync(host, ct, actorType: "system", actorId: null, actorRole: null, outcome: "succeeded");

        Assert.Equal("system", Assert.Single(await host.AuditRowsAsync(ct)).ActorType);
    }

    /// <summary>Asks for the category that matches the outcome; a caller that states one — null included — wins.</summary>
    private const string DerivedCategory = "<derived from the outcome>";

    private static Task<int> InsertAsync(
        InstallationTestHost host,
        CancellationToken cancellationToken,
        string actorType = "app_user",
        long? actorId = 1,
        string? actorRole = "admin",
        string action = "admin_sign_in",
        string? targetType = null,
        long? targetId = null,
        string outcome = "refused",
        string? refusalCategory = DerivedCategory) =>
        host.ExecuteAsync(
            Insert,
            cancellationToken,
            ("actorType", actorType),
            ("actorId", actorId),
            ("actorRole", actorRole),
            ("action", action),
            ("targetType", targetType),
            ("targetId", targetId),
            ("outcome", outcome),
            ("refusalCategory", refusalCategory == DerivedCategory
                ? outcome == "refused" ? "not_in_allowed_admin" : null
                : refusalCategory));

    private static string Text(Exception exception)
    {
        var text = new System.Text.StringBuilder();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            text.AppendLine(current.Message);
        }

        return text.ToString();
    }
}
