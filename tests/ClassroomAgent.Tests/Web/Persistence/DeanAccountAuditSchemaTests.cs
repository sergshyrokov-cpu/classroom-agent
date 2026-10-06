using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Persistence;

/// <summary>
/// US-012 db-design §4: the three closed lists of <c>audit_event</c> grow, and nothing else about the table
/// changes. The lists are constraints in the database, so a value the code writes but the constraint does not
/// know would fail at the insert, not at review (SC-11, PC-2; TC-2).
/// </summary>
public sealed class DeanAccountAuditSchemaTests(PostgreSqlFixture database)
{
    private const string Insert =
        """
        INSERT INTO audit_event (occurred_at, actor_type, actor_id, actor_role, action, target_type, target_id,
                                 outcome, refusal_category, request_id, created_at, updated_at)
        VALUES (now(), @actorType, @actorId, @actorRole, @action, @targetType, @targetId, @outcome,
                @refusalCategory, 'r-1', now(), now())
        """;

    /// <summary>db-design §4.1: each of the six new actions is accepted by the constraint.</summary>
    [Theory]
    [InlineData(DeanAccountTestData.Audit.Created)]
    [InlineData(DeanAccountTestData.Audit.Disabled)]
    [InlineData(DeanAccountTestData.Audit.ReEnabled)]
    [InlineData(DeanAccountTestData.Audit.PasswordReset)]
    [InlineData(DeanAccountTestData.Audit.PasswordChanged)]
    [InlineData(DeanAccountTestData.Audit.SignIn)]
    public async Task EachNewAction_IsAccepted(string action)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var rows = await host.ExecuteAsync(
            Insert,
            ct,
            ("actorType", "app_user"),
            ("actorId", 1L),
            ("actorRole", "admin"),
            ("action", action),
            ("targetType", DeanAccountTestData.Audit.TargetType),
            ("targetId", 2L),
            ("outcome", "succeeded"),
            ("refusalCategory", null));

        Assert.Equal(1, rows);
    }

    /// <summary>db-design §4.3: each of the three new refusal categories is accepted.</summary>
    [Theory]
    [InlineData(DeanAccountTestData.Audit.UnknownLogin)]
    [InlineData(DeanAccountTestData.Audit.WrongPassword)]
    [InlineData(DeanAccountTestData.Audit.LockedOut)]
    public async Task EachNewRefusalCategory_IsAccepted(string category)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var rows = await host.ExecuteAsync(
            Insert,
            ct,
            ("actorType", "anonymous"),
            ("actorId", null),
            ("actorRole", null),
            ("action", DeanAccountTestData.Audit.SignIn),
            ("targetType", null),
            ("targetId", null),
            ("outcome", "refused"),
            ("refusalCategory", category));

        Assert.Equal(1, rows);
    }

    /// <summary>
    /// db-design §4.4: the unknown-login row — anonymous actor, no identifier, no target at all — is exactly
    /// the shape the existing constraints already force, and it is legal (SC-11).
    /// </summary>
    [Fact]
    public async Task TheUnknownLoginShape_IsLegal()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var rows = await host.ExecuteAsync(
            Insert,
            ct,
            ("actorType", "anonymous"),
            ("actorId", null),
            ("actorRole", null),
            ("action", DeanAccountTestData.Audit.SignIn),
            ("targetType", null),
            ("targetId", null),
            ("outcome", "refused"),
            ("refusalCategory", DeanAccountTestData.Audit.UnknownLogin));

        Assert.Equal(1, rows);
    }

    /// <summary>db-design §4.2: the new target type is accepted.</summary>
    [Fact]
    public async Task TheAccountTargetType_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var rows = await host.ExecuteAsync(
            Insert,
            ct,
            ("actorType", "app_user"),
            ("actorId", 1L),
            ("actorRole", "dean"),
            ("action", DeanAccountTestData.Audit.PasswordChanged),
            ("targetType", DeanAccountTestData.Audit.TargetType),
            ("targetId", 1L),
            ("outcome", "succeeded"),
            ("refusalCategory", null));

        Assert.Equal(1, rows);
    }

    /// <summary>
    /// db-design §4.1: the lists stay closed — an action this Story did not add is still rejected, so the
    /// constraint is not widened beyond what SC-11 names.
    /// </summary>
    [Fact]
    public async Task AnActionOutsideTheList_IsStillRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => host.ExecuteAsync(
            Insert,
            ct,
            ("actorType", "app_user"),
            ("actorId", 1L),
            ("actorRole", "admin"),
            ("action", "dean_account_deleted"),
            ("targetType", DeanAccountTestData.Audit.TargetType),
            ("targetId", 2L),
            ("outcome", "succeeded"),
            ("refusalCategory", null)));

        Assert.Contains("ck_audit_event_action", Text(failure), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>db-design §4.5: no column is added to the table by this Story.</summary>
    [Fact]
    public async Task TheTable_GainsNoColumn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await InstallationSchemaQueries.ColumnsAsync(host, "audit_event", ct);

        // Thirteen since US-008: id, occurred_at, actor_type, actor_id, actor_role, action,
        // target_type, target_id, outcome, refusal_category, request_id, created_at, updated_at — plus, since
        // US-037 (db-design §2.2), the five integer counts of the retention purge's row.
        // US-028 adds the six export columns of the journal export's row (db-design).
        Assert.Equal(24, columns.Count);
    }

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
