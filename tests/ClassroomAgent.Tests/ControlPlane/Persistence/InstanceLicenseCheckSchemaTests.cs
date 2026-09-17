using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;

namespace ClassroomAgent.Tests.ControlPlane.Persistence;

/// <summary>US-005 db-design §3: the <c>instance_license_check</c> table and its constraints (AC-005).</summary>
public sealed class InstanceLicenseCheckSchemaTests(PostgreSqlFixture database)
{
    private const string Insert =
        """
        INSERT INTO instance_license_check (installation_id, answered_at, application_version, contract_version, answered_status, answered_compatibility, created_at, updated_at)
        VALUES (@installationId, now(), @applicationVersion, @contractVersion, @status, @compatibility, now(), now())
        """;

    [Fact]
    public async Task Columns_MatchDesign()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var columns = await SchemaQueries.ColumnsAsync(host, "instance_license_check", ct);

        Assert.Equal(
            new[]
            {
                "answered_at timestamp with time zone - NO -",
                "answered_compatibility character varying 24 NO -",
                "answered_status character varying 16 NO -",
                "application_version character varying 20 NO -",
                "contract_version integer - NO -",
                "created_at timestamp with time zone - NO -",
                "id bigint - NO -",
                "installation_id bigint - NO -",
                "updated_at timestamp with time zone - NO -",
            },
            columns);
        Assert.Equal("id", await SchemaQueries.PrimaryKeyAsync(host, "instance_license_check", "pk_instance_license_check", ct));
        var unique = await SchemaQueries.IndexDefinitionAsync(host, "uq_instance_license_check_installation_id", ct);
        Assert.NotNull(unique);
        Assert.Contains("UNIQUE", unique, StringComparison.Ordinal);
        Assert.Contains("(installation_id)", unique, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SecondRowForOneInstallation_ViolatesUniqueConstraint()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        var installationId = await host.InstallationInternalIdAsync(await host.InsertInstallationAsync(ct), ct);
        await InsertAsync(host, installationId, ct);

        var error = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(host, installationId, ct));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("uq_instance_license_check_installation_id", error.ConstraintName);
    }

    [Fact]
    public async Task UnknownInstallation_ViolatesForeignKey()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var error = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(host, 999_999, ct));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal("fk_instance_license_check_installation", error.ConstraintName);
    }

    [Fact]
    public async Task InstallationWithACheck_CannotBeDeleted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        var identifier = await host.InsertInstallationAsync(ct);
        await InsertAsync(host, await host.InstallationInternalIdAsync(identifier, ct), ct);

        await Assert.ThrowsAsync<PostgresException>(() => host.ExecuteAsync(
            "DELETE FROM installation WHERE identifier = @identifier",
            ct,
            ("identifier", identifier)));

        Assert.Single(await host.InstanceLicenseChecksAsync(ct));
    }

    [Theory]
    [InlineData("1.0", 1, "active", "supported", "ck_instance_license_check_application_version")]
    [InlineData("01.0.0", 1, "active", "supported", "ck_instance_license_check_application_version")]
    [InlineData("1.0.0-rc1", 1, "active", "supported", "ck_instance_license_check_application_version")]
    [InlineData("1.0.0", 0, "active", "supported", "ck_instance_license_check_contract_version")]
    [InlineData("1.0.0", 1000000, "active", "supported", "ck_instance_license_check_contract_version")]
    [InlineData("1.0.0", 1, "paused", "supported", "ck_instance_license_check_answered_status")]
    [InlineData("1.0.0", 1, "ACTIVE", "supported", "ck_instance_license_check_answered_status")]
    [InlineData("1.0.0", 1, "active", "unknown", "ck_instance_license_check_answered_compatibility")]
    [InlineData("1.0.0", 1, "active", "Supported", "ck_instance_license_check_answered_compatibility")]
    public async Task InvalidValue_ViolatesNamedCheckConstraint(
        string applicationVersion,
        int contractVersion,
        string status,
        string compatibility,
        string constraint)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        var installationId = await host.InstallationInternalIdAsync(await host.InsertInstallationAsync(ct), ct);

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertAsync(host, installationId, ct, applicationVersion, contractVersion, status, compatibility));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
    }

    [Theory]
    [InlineData("0.0.0", 1, "suspended", "upgrade_required")]
    [InlineData("999999.999999.999999", 999999, "active", "upgrade_recommended")]
    public async Task BoundaryValues_AreAccepted(string applicationVersion, int contractVersion, string status, string compatibility)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        var installationId = await host.InstallationInternalIdAsync(await host.InsertInstallationAsync(ct), ct);

        await InsertAsync(host, installationId, ct, applicationVersion, contractVersion, status, compatibility);

        Assert.Single(await host.InstanceLicenseChecksAsync(ct));
    }

    private static Task<int> InsertAsync(
        ControlPlaneTestHost host,
        long installationId,
        CancellationToken cancellationToken,
        string applicationVersion = "1.0.0",
        int contractVersion = 1,
        string status = "active",
        string compatibility = "supported") =>
        host.ExecuteAsync(
            Insert,
            cancellationToken,
            ("installationId", installationId),
            ("applicationVersion", applicationVersion),
            ("contractVersion", contractVersion),
            ("status", status),
            ("compatibility", compatibility));
}
