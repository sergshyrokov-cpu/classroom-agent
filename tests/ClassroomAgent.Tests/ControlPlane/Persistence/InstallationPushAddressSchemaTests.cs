using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;

namespace ClassroomAgent.Tests.ControlPlane.Persistence;

/// <summary>
/// US-006 db-design §3.1, §7: the nullable <c>installation.push_address</c> column, its shape constraint,
/// the absence of a unique index, and the unchanged immutability triggers (AC-002, AC-003).
/// </summary>
public sealed class InstallationPushAddressSchemaTests(PostgreSqlFixture database)
{
    private const string Insert =
        """
        INSERT INTO installation (identifier, name, domain, client_id, status, push_address, created_at, updated_at)
        VALUES (@identifier, @name, @domain, @clientId, 'active', @pushAddress, now(), now())
        """;

    [Fact]
    public async Task PushAddressColumn_IsNullableVarchar255()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var columns = await SchemaQueries.ColumnsAsync(host, "installation", ct);

        Assert.Contains("push_address character varying 255 YES -", columns);
    }

    [Fact]
    public async Task PushAddress_HasNoUniqueIndex()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var indexes = await host.QueryAsync(
            "SELECT indexdef FROM pg_indexes WHERE tablename = 'installation'",
            r => r.GetString(0),
            ct);

        Assert.DoesNotContain(indexes, index => index.Contains("push_address", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("http://10.0.0.5:8081")]
    [InlineData("http://school-a.private:8081")]
    [InlineData("http://[fd00::5]:8081")]
    [InlineData("http://10.0.0.5:65535")]
    [InlineData(null)]
    public async Task CanonicalValueOrNull_IsAccepted(string? address)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var written = await InsertAsync(host, address, ct);

        Assert.Equal(1, written);
    }

    [Theory]
    [InlineData("https://10.0.0.5:8081")]
    [InlineData("http://10.0.0.5")]
    [InlineData("http://10.0.0.5:8081/")]
    [InlineData("http://10.0.0.5:8081/push")]
    [InlineData("http://School-A.Private:8081")]
    [InlineData("http://10.0.0.5:08081")]
    [InlineData("10.0.0.5:8081")]
    [InlineData("")]
    public async Task NonCanonicalValue_ViolatesTheCheckConstraint(string address)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(host, address, ct));

        Assert.Equal("ck_installation_push_address_format", exception.ConstraintName);
    }

    [Fact]
    public async Task UpdatingThePushAddressDirectly_IsAllowed_WhileTheDomainStaysImmutable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        var identifier = await host.InsertInstallationAsync(ct);

        await host.SetPushAddressDirectlyAsync(identifier, PushTestData.Address, ct);

        Assert.Equal(PushTestData.Address, await host.PushAddressAsync(identifier, ct));
        await Assert.ThrowsAsync<PostgresException>(() => host.ExecuteAsync(
            "UPDATE installation SET domain = 'other.example.test' WHERE identifier = @identifier",
            ct,
            ("identifier", identifier)));
    }

    private static Task<int> InsertAsync(ControlPlaneTestHost host, string? address, CancellationToken cancellationToken) =>
        host.ExecuteAsync(
            Insert,
            cancellationToken,
            ("identifier", Guid.NewGuid()),
            ("name", InstallationTestData.Name),
            ("domain", Guid.NewGuid().ToString("N") + ".example.test"),
            ("clientId", Random.Shared.NextInt64(100000000000000000, 999999999999999999).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("pushAddress", address));
}
