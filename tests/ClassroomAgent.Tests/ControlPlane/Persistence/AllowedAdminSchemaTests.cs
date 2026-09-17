using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;

namespace ClassroomAgent.Tests.ControlPlane.Persistence;

/// <summary>US-003 db-design §3: the <c>allowed_admin</c> table, its constraints, indexes and triggers (AC-002, AC-003, AC-004, AC-005).</summary>
public sealed class AllowedAdminSchemaTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task AllowedAdminColumns_MatchDesign_NoPasswordOrSecretColumn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var columns = await SchemaQueries.ColumnsAsync(host, "allowed_admin", ct);

        Assert.Equal(
            new[]
            {
                "added_by_owner_id bigint - NO -",
                "created_at timestamp with time zone - NO -",
                "email character varying 254 NO -",
                "id bigint - NO -",
                "identifier uuid - NO -",
                "installation_id bigint - NO -",
                "updated_at timestamp with time zone - NO -",
            },
            columns);
        Assert.Equal("id", await SchemaQueries.PrimaryKeyAsync(host, "allowed_admin", "pk_allowed_admin", ct));
    }

    [Theory]
    [InlineData("uq_allowed_admin_identifier", "UNIQUE", "(identifier)")]
    [InlineData("uq_allowed_admin_installation_email", "UNIQUE", "(installation_id, email)")]
    [InlineData("ix_allowed_admin_added_by_owner_id", "INDEX", "(added_by_owner_id)")]
    public async Task Indexes_Exist(string index, string kind, string columns)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var definition = await SchemaQueries.IndexDefinitionAsync(host, index, ct);

        Assert.NotNull(definition);
        Assert.Contains(kind, definition, StringComparison.Ordinal);
        Assert.Contains(columns, definition, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ForeignKeys_ReferenceInstallationAndOwner_WithRestrict()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var keys = await host.QueryAsync(
            """
            SELECT conname::text, confrelid::regclass::text, confdeltype::text
            FROM pg_constraint
            WHERE conrelid = 'allowed_admin'::regclass AND contype = 'f'
            ORDER BY conname
            """,
            r => $"{r.GetString(0)} {r.GetString(1)} {r.GetString(2)}",
            ct);

        // confdeltype 'r' = RESTRICT.
        Assert.Equal(
            new[] { "fk_allowed_admin_added_by_owner owner r", "fk_allowed_admin_installation installation r" },
            keys);
    }

    [Fact]
    public async Task ValidRow_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);

        await host.InsertAllowedAdminAsync(installation, "o'brien.x-y_z@school-one.example.test", ct);

        Assert.Single(await host.AllowedAdminsAsync(ct));
    }

    [Theory]
    [InlineData("identifier", "uq_allowed_admin_identifier")]
    [InlineData("email", "uq_allowed_admin_installation_email")]
    public async Task DuplicateValue_ViolatesNamedUniqueConstraint(string column, string constraint)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var identifier = Guid.NewGuid();
        await InsertAsync(host, installation, AllowedAdminTestData.Email, ct, identifier);

        var error = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(
            host,
            installation,
            column == "email" ? AllowedAdminTestData.Email : AllowedAdminTestData.OtherEmail,
            ct,
            column == "identifier" ? identifier : Guid.NewGuid()));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
        Assert.Single(await host.AllowedAdminsAsync(ct));
    }

    [Theory]
    [InlineData("Ivan@school-one.example.test")]
    [InlineData("ivan+x@school-one.example.test")]
    [InlineData("iv an@school-one.example.test")]
    [InlineData("@school-one.example.test")]
    [InlineData("іван@school-one.example.test")]
    public async Task CheckConstraints_RejectBadlyShapedEmail(string email)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);

        var error = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(host, installation, email, ct));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.StartsWith("ck_allowed_admin_email_", error.ConstraintName, StringComparison.Ordinal);
        Assert.Empty(await host.AllowedAdminsAsync(ct));
    }

    [Fact]
    public async Task CheckConstraints_AllDesignedConstraintsExist()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var constraints = await host.QueryAsync(
            "SELECT conname::text FROM pg_constraint WHERE conrelid = 'allowed_admin'::regclass AND contype = 'c' ORDER BY conname",
            r => r.GetString(0),
            ct);

        Assert.Equal(new[] { "ck_allowed_admin_email_format", "ck_allowed_admin_email_lower" }, constraints);
    }

    [Theory]
    [InlineData("ivan@gmail.com")]
    [InlineData("ivan@school-two.example.test")]
    [InlineData("ivan@mail.school-one.example.test")]
    public async Task EmailOutsideInstallationDomain_IsRefusedByTrigger(string email)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);

        await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(host, installation, email, ct));

        Assert.Empty(await host.AllowedAdminsAsync(ct));
    }

    [Fact]
    public async Task UnknownInstallationOrOwner_ViolatesForeignKey()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        await host.CreateOwnerAsync(ct);
        var installationId = (await host.InstallationAsync(await host.InsertInstallationAsync(ct), ct))!.Id;
        var ownerId = (await host.OwnerAsync(ct))!.Id;

        var unknownOwner = await Assert.ThrowsAsync<PostgresException>(
            () => InsertRawAsync(host, installationId, ownerId + 1000, ct));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, unknownOwner.SqlState);
        Assert.Equal("fk_allowed_admin_added_by_owner", unknownOwner.ConstraintName);
        await Assert.ThrowsAsync<PostgresException>(() => InsertRawAsync(host, installationId + 1000, ownerId, ct));
        Assert.Empty(await host.AllowedAdminsAsync(ct));
    }

    [Theory]
    [InlineData("email = 'olena.koval@school-one.example.test'")]
    [InlineData("identifier = gen_random_uuid()")]
    [InlineData("created_at = created_at - interval '1 day'")]
    [InlineData("updated_at = now()")]
    [InlineData("email = email")]
    public async Task AnyUpdate_IsRefusedByTrigger(string assignment)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        await host.InsertAllowedAdminAsync(installation, AllowedAdminTestData.Email, ct);
        var before = await host.AllowedAdminsAsync(ct);

        await Assert.ThrowsAsync<PostgresException>(() => host.ExecuteAsync($"UPDATE allowed_admin SET {assignment}", ct));

        Assert.Equal(before, await host.AllowedAdminsAsync(ct));
    }

    [Fact]
    public async Task Delete_IsAllowed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var admin = await host.InsertAllowedAdminAsync(installation, AllowedAdminTestData.Email, ct);

        var deleted = await host.ExecuteAsync("DELETE FROM allowed_admin WHERE identifier = @identifier", ct, ("identifier", admin));

        Assert.Equal(1, deleted);
        Assert.Empty(await host.AllowedAdminsAsync(ct));
    }

    [Fact]
    public async Task Triggers_ExistOnAllowedAdmin()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var triggers = await host.QueryAsync(
            "SELECT tgname::text FROM pg_trigger WHERE tgrelid = 'allowed_admin'::regclass AND NOT tgisinternal ORDER BY tgname",
            r => r.GetString(0),
            ct);

        Assert.Equal(new[] { "trg_allowed_admin_domain_match", "trg_allowed_admin_no_update" }, triggers);
    }

    private static Task<Guid> InsertAsync(
        ControlPlaneTestHost host,
        Guid installation,
        string email,
        CancellationToken cancellationToken,
        Guid? identifier = null) =>
        identifier is null
            ? host.InsertAllowedAdminAsync(installation, email, cancellationToken)
            : InsertWithIdentifierAsync(host, installation, email, identifier.Value, cancellationToken);

    private static async Task<Guid> InsertWithIdentifierAsync(
        ControlPlaneTestHost host,
        Guid installation,
        string email,
        Guid identifier,
        CancellationToken cancellationToken)
    {
        await host.ExecuteAsync(
            """
            INSERT INTO allowed_admin (identifier, installation_id, email, added_by_owner_id, created_at, updated_at)
            VALUES (@identifier, (SELECT id FROM installation WHERE identifier = @installation), @email, (SELECT id FROM owner LIMIT 1), now(), now())
            """,
            cancellationToken,
            ("identifier", identifier),
            ("installation", installation),
            ("email", email));
        return identifier;
    }

    private static Task<int> InsertRawAsync(ControlPlaneTestHost host, long installationId, long ownerId, CancellationToken cancellationToken) =>
        host.ExecuteAsync(
            """
            INSERT INTO allowed_admin (identifier, installation_id, email, added_by_owner_id, created_at, updated_at)
            VALUES (gen_random_uuid(), @installationId, @email, @ownerId, now(), now())
            """,
            cancellationToken,
            ("installationId", installationId),
            ("email", AllowedAdminTestData.Email),
            ("ownerId", ownerId));
}
