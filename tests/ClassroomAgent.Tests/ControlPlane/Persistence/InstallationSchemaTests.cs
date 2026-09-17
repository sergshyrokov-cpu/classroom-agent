using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;

namespace ClassroomAgent.Tests.ControlPlane.Persistence;

/// <summary>US-002 db-design §3: the <c>installation</c> table, its constraints and triggers (AC-002, AC-004, AC-005).</summary>
public sealed class InstallationSchemaTests(PostgreSqlFixture database)
{
    private const string Insert =
        """
        INSERT INTO installation (identifier, name, domain, client_id, status, created_at, updated_at)
        VALUES (@identifier, @name, @domain, @clientId, @status, now(), now())
        """;

    [Fact]
    public async Task InstallationColumns_MatchDesign_NoKeyOrSecretColumn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var columns = await SchemaQueries.ColumnsAsync(host, "installation", ct);

        Assert.Equal(
            new[]
            {
                "client_id character varying 32 NO -",
                "created_at timestamp with time zone - NO -",
                "domain character varying 253 NO -",
                "id bigint - NO -",
                "identifier uuid - NO -",
                "name character varying 200 NO -",
                "status character varying 16 NO -",
                "updated_at timestamp with time zone - NO -",
            },
            columns);
        Assert.Equal("id", await SchemaQueries.PrimaryKeyAsync(host, "installation", "pk_installation", ct));
    }

    [Theory]
    [InlineData("uq_installation_identifier", "(identifier)")]
    [InlineData("uq_installation_domain", "(domain)")]
    [InlineData("uq_installation_client_id", "(client_id)")]
    public async Task UniqueIndexes_Exist(string index, string column)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var definition = await SchemaQueries.IndexDefinitionAsync(host, index, ct);

        Assert.NotNull(definition);
        Assert.Contains("UNIQUE", definition, StringComparison.Ordinal);
        Assert.Contains(column, definition, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("identifier", "uq_installation_identifier")]
    [InlineData("domain", "uq_installation_domain")]
    [InlineData("client_id", "uq_installation_client_id")]
    public async Task DuplicateValue_ViolatesNamedUniqueConstraint(string column, string constraint)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        var identifier = Guid.NewGuid();
        await InsertAsync(host, ct, identifier: identifier);

        var error = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(
            host,
            ct,
            identifier: column == "identifier" ? identifier : Guid.NewGuid(),
            domain: column == "domain" ? InstallationTestData.Domain : InstallationTestData.OtherDomain,
            clientId: column == "client_id" ? InstallationTestData.ClientId : InstallationTestData.OtherClientId));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
        Assert.Single(await host.InstallationsAsync(ct));
    }

    [Theory]
    [InlineData("", "school.example.test", "1234567890", "active", "ck_installation_name_length")]
    [InlineData("Школа", "localhost", "1234567890", "active", "ck_installation_domain_format")]
    [InlineData("Школа", "ab", "1234567890", "active", "ck_installation_domain_format")]
    [InlineData("Школа", "school_one.example.test", "1234567890", "active", "ck_installation_domain_format")]
    [InlineData("Школа", "school.example.test", "123456789", "active", "ck_installation_client_id_format")]
    [InlineData("Школа", "school.example.test", "12345678901a", "active", "ck_installation_client_id_format")]
    [InlineData("Школа", "school.example.test", "1234567890", "deleted", "ck_installation_status")]
    public async Task CheckConstraints_RejectViolatingRows(string name, string domain, string clientId, string status, string constraint)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => InsertAsync(host, ct, name: name, domain: domain, clientId: clientId, status: status));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
    }

    [Fact]
    public async Task CheckConstraints_AllDesignedConstraintsExist()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var constraints = await host.QueryAsync(
            "SELECT conname::text FROM pg_constraint WHERE conrelid = 'installation'::regclass AND contype = 'c' ORDER BY conname",
            r => r.GetString(0),
            ct);

        // PostgreSQL tests check constraints in name order, so an upper-case domain is reported by
        // ck_installation_domain_format first; ck_installation_domain_lower is asserted by existence.
        Assert.Equal(
            new[]
            {
                "ck_installation_client_id_format",
                "ck_installation_domain_format",
                "ck_installation_domain_lower",
                "ck_installation_name_length",
                "ck_installation_status",
            },
            constraints);
    }

    [Fact]
    public async Task UpperCaseDomain_IsRejectedByACheckConstraint()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => InsertAsync(host, ct, domain: "School.example.test"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.StartsWith("ck_installation_domain_", error.ConstraintName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidRow_AtUpperBounds_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        var label = new string('a', 63);

        await InsertAsync(
            host,
            ct,
            name: string.Concat(Enumerable.Repeat("𝔸", 200)),
            domain: string.Join('.', label, label, label, new string('b', 61)),
            clientId: new string('0', 32),
            status: "suspended");

        Assert.Single(await host.InstallationsAsync(ct));
    }

    [Theory]
    [InlineData("identifier = @value", "value-uuid")]
    [InlineData("domain = @value", "other.example.test")]
    public async Task UpdatingIdentifierOrDomain_IsRefusedByTrigger(string assignment, string value)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        var identifier = Guid.NewGuid();
        await InsertAsync(host, ct, identifier: identifier);
        object parameter = value == "value-uuid" ? Guid.NewGuid() : value;

        await Assert.ThrowsAsync<PostgresException>(() => host.ExecuteAsync(
            $"UPDATE installation SET {assignment} WHERE identifier = @identifier",
            ct,
            ("value", parameter),
            ("identifier", identifier)));

        var row = await host.InstallationAsync(identifier, ct);
        Assert.NotNull(row);
        Assert.Equal(InstallationTestData.Domain, row.Domain);
    }

    [Fact]
    public async Task UpdatingNameClientIdAndStatus_IsAllowed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        var identifier = Guid.NewGuid();
        await InsertAsync(host, ct, identifier: identifier);

        var updated = await host.ExecuteAsync(
            "UPDATE installation SET name = @name, client_id = @clientId, status = 'suspended', domain = domain, identifier = identifier WHERE identifier = @identifier",
            ct,
            ("name", InstallationTestData.OtherName),
            ("clientId", InstallationTestData.OtherClientId),
            ("identifier", identifier));

        Assert.Equal(1, updated);
        var row = await host.InstallationAsync(identifier, ct);
        Assert.Equal(InstallationTestData.OtherName, row?.Name);
        Assert.Equal(InstallationTestData.OtherClientId, row?.ClientId);
        Assert.Equal("suspended", row?.Status);
    }

    [Fact]
    public async Task DeletingInstallation_IsRefusedByTrigger()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        var identifier = Guid.NewGuid();
        await InsertAsync(host, ct, identifier: identifier);

        await Assert.ThrowsAsync<PostgresException>(() => host.ExecuteAsync(
            "DELETE FROM installation WHERE identifier = @identifier",
            ct,
            ("identifier", identifier)));

        Assert.Single(await host.InstallationsAsync(ct));
    }

    [Fact]
    public async Task Triggers_ExistOnInstallation()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var triggers = await host.QueryAsync(
            "SELECT tgname::text FROM pg_trigger WHERE tgrelid = 'installation'::regclass AND NOT tgisinternal ORDER BY tgname",
            r => r.GetString(0),
            ct);

        Assert.Equal(new[] { "trg_installation_immutable_columns", "trg_installation_no_delete" }, triggers);
    }

    private static Task<int> InsertAsync(
        ControlPlaneTestHost host,
        CancellationToken cancellationToken,
        Guid? identifier = null,
        string name = InstallationTestData.Name,
        string domain = InstallationTestData.Domain,
        string clientId = InstallationTestData.ClientId,
        string status = "active") =>
        host.ExecuteAsync(
            Insert,
            cancellationToken,
            ("identifier", identifier ?? Guid.NewGuid()),
            ("name", name),
            ("domain", domain),
            ("clientId", clientId),
            ("status", status));
}
