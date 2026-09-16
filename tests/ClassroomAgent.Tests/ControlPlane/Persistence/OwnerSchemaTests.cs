using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;

namespace ClassroomAgent.Tests.ControlPlane.Persistence;

/// <summary>AC-004 and db-design §3: the <c>owner</c> table and its constraints.</summary>
public sealed class OwnerSchemaTests(PostgreSqlFixture database)
{
    private const string InsertOwner =
        """
        INSERT INTO owner (user_name, normalized_user_name, password_hash, security_stamp, concurrency_stamp,
                           access_failed_count, lockout_end, ui_language, singleton, created_at, updated_at)
        VALUES (@login, upper(@login), 'hash', 'stamp', 'concurrency', @failed, NULL, @language, @singleton, now(), now())
        """;

    [Fact]
    public async Task SecondOwnerRowWithDifferentLogin_ViolatesSingleton()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        await InsertAsync(host, "owner.first", ct);

        var error = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(host, "owner.second", ct));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("uq_owner_singleton", error.ConstraintName);
        Assert.Equal(1, await host.OwnerCountAsync(ct));
    }

    [Theory]
    [InlineData("de", 0, true, "ck_owner_ui_language")]
    [InlineData("uk", -1, true, "ck_owner_access_failed_count")]
    [InlineData("uk", 0, false, "ck_owner_singleton")]
    public async Task OwnerCheckConstraints_RejectViolatingRows(string language, int failedCount, bool singleton, string constraint)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => InsertAsync(host, "owner.one", ct, language, failedCount, singleton));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
    }

    [Fact]
    public async Task OwnerColumns_MatchDesign_NoEmailOrPhone()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var columns = await SchemaQueries.ColumnsAsync(host, "owner", ct);

        Assert.Equal(
            new[]
            {
                "access_failed_count integer - NO 0",
                "concurrency_stamp character varying 64 NO -",
                "created_at timestamp with time zone - NO -",
                "id bigint - NO -",
                "lockout_end timestamp with time zone - YES -",
                "normalized_user_name character varying 64 NO -",
                "password_hash character varying 256 NO -",
                "security_stamp character varying 64 NO -",
                "singleton boolean - NO true",
                "ui_language character varying 8 NO -",
                "updated_at timestamp with time zone - NO -",
                "user_name character varying 64 NO -",
            },
            columns);
        Assert.Equal("id", await SchemaQueries.PrimaryKeyAsync(host, "owner", "pk_owner", ct));
    }

    [Fact]
    public async Task NormalizedUserNameAndSingleton_HaveUniqueIndexes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var normalized = await SchemaQueries.IndexDefinitionAsync(host, "uq_owner_normalized_user_name", ct);
        var singleton = await SchemaQueries.IndexDefinitionAsync(host, "uq_owner_singleton", ct);

        Assert.NotNull(normalized);
        Assert.Contains("UNIQUE", normalized, StringComparison.Ordinal);
        Assert.Contains("(normalized_user_name)", normalized, StringComparison.Ordinal);
        Assert.NotNull(singleton);
        Assert.Contains("UNIQUE", singleton, StringComparison.Ordinal);
        Assert.Contains("(singleton)", singleton, StringComparison.Ordinal);
    }

    private static Task<int> InsertAsync(
        ControlPlaneTestHost host,
        string login,
        CancellationToken cancellationToken,
        string language = "uk",
        int failedCount = 0,
        bool singleton = true) =>
        host.ExecuteAsync(
            InsertOwner,
            cancellationToken,
            ("login", login),
            ("failed", failedCount),
            ("language", language),
            ("singleton", singleton));
}
