using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-008 db-design 3: the <c>app_user</c> table's columns, its unique index and its nine check constraints —
/// above all the two that make an Admin with a password impossible in the database itself (S-04, BR-010;
/// PC-3 … PC-9; TC-2).
/// </summary>
public sealed class AppUserSchemaTests(PostgreSqlFixture database)
{
    private const string Insert =
        """
        INSERT INTO app_user (email, normalized_email, role, sign_in_method, password_hash, security_stamp,
                              concurrency_stamp, access_failed_count, ui_language, is_disabled, created_at, updated_at)
        VALUES (@email, @normalizedEmail, @role, @signInMethod, @passwordHash, 'stamp', 'concurrency',
                @accessFailedCount, @uiLanguage, false, now(), now())
        """;

    [Fact]
    public async Task Columns_MatchTheDesign()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await InstallationSchemaQueries.ColumnsAsync(host, "app_user", ct);

        Assert.Equal(
            new[]
            {
                "access_failed_count integer - NO 0",
                "concurrency_stamp character varying 64 NO -",
                "created_at timestamp with time zone - NO -",
                "email character varying 254 NO -",
                "id bigint - NO -",
                "is_disabled boolean - NO false",
                "last_successful_sign_in_at timestamp with time zone - YES -",
                "lockout_end timestamp with time zone - YES -",
                "normalized_email character varying 254 NO -",
                "password_hash character varying 256 YES -",
                "role character varying 16 NO -",
                "security_stamp character varying 64 NO -",
                "sign_in_method character varying 16 NO -",
                "ui_language character varying 8 NO -",
                "updated_at timestamp with time zone - NO -",
            },
            columns);
    }

    /// <summary>db-design 3: no column holds a Google token, a subject id, a display name or a picture (S-09).</summary>
    [Fact]
    public async Task NoColumn_HoldsAGoogleTokenOrProfileDatum()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await InstallationSchemaQueries.ColumnsAsync(host, "app_user", ct);

        Assert.DoesNotContain(columns, c => c.Contains("token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(columns, c => c.Contains("subject", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(columns, c => c.Contains("picture", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(columns, c => c.Contains("display_name", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(columns, c => c.Contains("client_secret", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PrimaryKeyAndConstraints_AreNamedAsTheDesignFixesThem()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var primaryKey = await InstallationSchemaQueries.PrimaryKeyAsync(host, "app_user", ct);
        var checks = await InstallationSchemaQueries.CheckConstraintsAsync(host, "app_user", ct);

        Assert.Equal("pk_app_user", primaryKey);
        Assert.Equal(
            new[]
            {
                "ck_app_user_access_failed_count",
                "ck_app_user_email_lowercase",
                "ck_app_user_password_hash",
                "ck_app_user_role",
                "ck_app_user_role_sign_in_method",
                "ck_app_user_sign_in_method",
                "ck_app_user_ui_language",
            },
            checks.Where(c => c.StartsWith("ck_app_user", StringComparison.Ordinal)));
    }

    /// <summary>db-design 3.2, PC-7: the unique index is the only index, and it covers the lookup too.</summary>
    [Fact]
    public async Task TheOnlyIndexes_ArePrimaryKeyAndTheUniqueNormalizedEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var indexes = await InstallationSchemaQueries.IndexNamesAsync(host, "app_user", ct);

        Assert.Equal(new[] { "pk_app_user", "uq_app_user_normalized_email" }, indexes);
    }

    /// <summary>db-design 5: no foreign key on the table.</summary>
    [Fact]
    public async Task TheTable_HasNoForeignKey()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.Empty(await InstallationSchemaQueries.ForeignKeysAsync(host, "app_user", ct));
    }

    /// <summary>S-04, db-design 3.1: an Admin row carrying any password hash is impossible — empty string included.</summary>
    [Theory]
    [InlineData("a-real-looking-hash")]
    [InlineData("")]
    [InlineData(" ")]
    public async Task AnAdminRowWithAPasswordHash_IsRejected(string passwordHash)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => InsertAsync(
            host,
            ct,
            role: "admin",
            signInMethod: "google",
            passwordHash: passwordHash));

        Assert.Contains("ck_app_user", Text(failure), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await host.AppUsersAsync(ct));
    }

    /// <summary>db-design 3.1: an Admin signing in by password, or a Dean by Google, is impossible.</summary>
    [Theory]
    [InlineData("admin", "password")]
    [InlineData("dean", "google")]
    public async Task ARoleWithTheWrongSignInMethod_IsRejected(string role, string signInMethod)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => InsertAsync(
            host,
            ct,
            role: role,
            signInMethod: signInMethod,
            passwordHash: signInMethod == "password" ? "hash" : null));

        Assert.Contains("ck_app_user_role_sign_in_method", Text(failure), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>db-design 3.1: a Dean row without a hash is impossible in the first version.</summary>
    [Fact]
    public async Task ADeanRowWithoutAPasswordHash_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => InsertAsync(
            host,
            ct,
            role: "dean",
            signInMethod: "password",
            passwordHash: null));

        Assert.Contains("ck_app_user_password_hash", Text(failure), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>db-design 3.1: AppRole has exactly two members in the first version.</summary>
    [Theory]
    [InlineData("teacher")]
    [InlineData("student")]
    [InlineData("owner")]
    [InlineData("Admin")]
    public async Task AnUnknownRole_IsRejected(string role)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => InsertAsync(host, ct, role: role, signInMethod: "google"));

        Assert.Contains("ck_app_user", Text(failure), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>db-design 3.1, BR-079: a mixed-case address is rejected — the last line of defence.</summary>
    [Fact]
    public async Task AMixedCaseEmail_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => InsertAsync(
            host,
            ct,
            email: SignInTestData.AdminEmailMixedCase,
            role: "admin",
            signInMethod: "google"));

        Assert.Contains("ck_app_user_email_lowercase", Text(failure), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>db-design 3.1: the language codes are the two of NFR-073.</summary>
    [Theory]
    [InlineData("ru")]
    [InlineData("uk-UA")]
    [InlineData("UK")]
    public async Task AnUnknownUiLanguage_IsRejected(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => InsertAsync(
            host,
            ct,
            role: "admin",
            signInMethod: "google",
            uiLanguage: language));

        Assert.Contains("ck_app_user_ui_language", Text(failure), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>db-design 3.1: the failed-attempt counter never goes below zero.</summary>
    [Fact]
    public async Task ANegativeAccessFailedCount_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => InsertAsync(
            host,
            ct,
            role: "admin",
            signInMethod: "google",
            accessFailedCount: -1));

        Assert.Contains("ck_app_user_access_failed_count", Text(failure), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>db-design 3.1, AC-007: the second row for an address is rejected by the database.</summary>
    [Fact]
    public async Task ASecondRowWithTheSameNormalizedEmail_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await InsertAsync(host, ct, role: "admin", signInMethod: "google");

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => InsertAsync(host, ct, role: "admin", signInMethod: "google"));

        Assert.Contains("uq_app_user_normalized_email", Text(failure), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>db-design 3: a valid Admin row is accepted, so the constraints reject only what they must.</summary>
    [Fact]
    public async Task AValidAdminRow_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertAsync(host, ct, role: "admin", signInMethod: "google");

        var row = Assert.Single(await host.AppUsersAsync(ct));
        Assert.Null(row.PasswordHash);
        Assert.Equal("admin", row.Role);
        Assert.Equal("google", row.SignInMethod);
    }

    private static Task<int> InsertAsync(
        InstallationTestHost host,
        CancellationToken cancellationToken,
        string email = SignInTestData.AdminEmail,
        string role = "admin",
        string signInMethod = "google",
        string? passwordHash = null,
        string uiLanguage = "uk",
        int accessFailedCount = 0) =>
        host.ExecuteAsync(
            Insert,
            cancellationToken,
            ("email", email),
            ("normalizedEmail", email),
            ("role", role),
            ("signInMethod", signInMethod),
            ("passwordHash", passwordHash),
            ("accessFailedCount", accessFailedCount),
            ("uiLanguage", uiLanguage));

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
