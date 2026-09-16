using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Security;

/// <summary>AC-011, FR-017: the Data Protection key ring lives in the configured directory (SC-7).</summary>
public sealed class DataProtectionTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task KeyRing_IsWrittenToConfiguredDirectory()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        await client.GetAsync("/setup", ct);

        Assert.NotEmpty(Directory.EnumerateFiles(host.KeyDirectory, "key-*.xml"));
    }
}
