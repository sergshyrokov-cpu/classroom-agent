using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-012 db-design §3: the one column this Story adds and the one check constraint that guards it. The
/// constraint matters as much as the column — it makes an Admin row carrying a temporary password impossible in
/// the database itself, beside the two US-008 constraints that already make an Admin password hash impossible
/// (S-04, SC-2; PC-3 … PC-9; TC-2).
/// </summary>
public sealed class DeanAccountSchemaTests(PostgreSqlFixture database)
{
    private const string Insert =
        """
        INSERT INTO app_user (email, normalized_email, role, sign_in_method, password_hash, security_stamp,
                              concurrency_stamp, access_failed_count, ui_language, is_disabled,
                              password_is_temporary, created_at, updated_at)
        VALUES (@email, @email, @role, @signInMethod, @passwordHash, 'stamp', 'concurrency', 0, 'uk', false,
                @temporary, now(), now())
        """;

    /// <summary>db-design §3.1: the column exists, is not nullable and defaults to false.</summary>
    [Fact]
    public async Task TheColumn_IsNotNullableAndDefaultsToFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await InstallationSchemaQueries.ColumnsAsync(host, "app_user", ct);

        Assert.Contains("password_is_temporary boolean - NO false", columns);
    }

    /// <summary>db-design §3.2: the constraint is there, beside the ones US-008 wrote.</summary>
    [Fact]
    public async Task TheConstraint_IsDeclared()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var checks = await InstallationSchemaQueries.CheckConstraintsAsync(host, "app_user", ct);

        Assert.Contains("ck_app_user_password_temporary", checks);
    }

    /// <summary>db-design §3.2: an Admin row with a temporary password is rejected by PostgreSQL itself.</summary>
    [Fact]
    public async Task AnAdminRowWithATemporaryPassword_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => host.ExecuteAsync(
            Insert,
            ct,
            ("email", "admin@school-one.example.test"),
            ("role", "admin"),
            ("signInMethod", "google"),
            ("passwordHash", null),
            ("temporary", true)));

        Assert.Contains("ck_app_user_password_temporary", Text(failure), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>db-design §3.2: a Dean row may carry either state — both are legitimate at any time.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ADeanRow_MayCarryEitherState(bool temporary)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var rows = await host.ExecuteAsync(
            Insert,
            ct,
            ("email", DeanAccountTestData.DeanEmail),
            ("role", "dean"),
            ("signInMethod", "password"),
            ("passwordHash", "hash"),
            ("temporary", temporary));

        Assert.Equal(1, rows);
    }

    /// <summary>
    /// db-design §3.3: the unique index still refuses a second account on the same normalized email, whatever
    /// its role — which is what makes an address "already used" one answer for a Dean and an Admin alike
    /// (spec VR-001, S-03).
    /// </summary>
    [Fact]
    public async Task ASecondAccountOnTheSameAddress_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await host.ExecuteAsync(
            Insert,
            ct,
            ("email", DeanAccountTestData.DeanEmail),
            ("role", "dean"),
            ("signInMethod", "password"),
            ("passwordHash", "hash"),
            ("temporary", true));

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => host.ExecuteAsync(
            Insert,
            ct,
            ("email", DeanAccountTestData.DeanEmail),
            ("role", "dean"),
            ("signInMethod", "password"),
            ("passwordHash", "hash"),
            ("temporary", false)));

        Assert.Contains("uq_app_user_normalized_email", Text(failure), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// db-design §3.3, §5: this Story adds no table and no index (PC-7). The expected table set gained
    /// <c>sync_state</c> with US-013 and the three course tables with US-014, which are the Stories that
    /// add them, and <c>course_work</c> and <c>submission</c> with US-015, the three template tables with US-027 — US-012 still adds none.
    /// US-032 adds <c>meeting_code_link</c> (US-032 db-design §2).
    /// </summary>
    [Fact]
    public async Task TheStory_AddsNoTableAndNoIndex()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var tables = await host.TableNamesAsync(ct);
        var indexes = await InstallationSchemaQueries.IndexNamesAsync(host, "app_user", ct);

        Assert.Equal(
            new[]
            {
                "__EFMigrationsHistory",
                "app_user",
                "audit_event",
                "classroom_participant",
                "course",
                "course_membership",
                "course_work",
                "legitimacy_state",
                "meet_participation",
                "meet_session",
                "meeting_code_link",
                "report_template",
                "report_template_mark",
                "report_template_scale_row",
                "submission",
                "sync_state",
                "workspace_connection",
            },
            tables.Order(StringComparer.Ordinal));
        Assert.Equal(new[] { "pk_app_user", "uq_app_user_normalized_email" }, indexes.Order(StringComparer.Ordinal));
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
