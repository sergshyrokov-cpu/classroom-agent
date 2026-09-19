using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-007 AC-003, AC-009: the closed list of BR-026 keeps running in read-only mode. The one live case is
/// the <c>LegitimacyState</c> write of US-005 - without it the installation could never leave the mode -
/// and the list itself stays bound to BR-026 by being expressed in exactly one place (spec FR-004, FR-005).
/// </summary>
public sealed class PermittedServiceWriteTests(PostgreSqlFixture database)
{
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task TheLegitimacyCheckWrite_RunsInReadOnlyMode(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await ReadOnlyModeHost.SeedAsync(host, cause, ct);
        host.ConfigureServices = ReadOnlyModeHost.Register;
        var start = host.Time.GetUtcNow();
        host.ControlPlane.ReplySuccess();

        host.Start();
        await host.WaitForNextCheckAtAsync(start + TimeSpan.FromHours(6), ct);

        var row = Assert.Single(await host.LegitimacyStatesAsync(ct));
        Assert.Equal(start, row.LastSuccessfulCheckAt);
        Assert.Equal("active", row.Status);
    }

    [Fact]
    public void TheList_HasExactlyTheFourMembersOfBr026()
    {
        var members = Enum.GetNames<PermittedServiceWrite>().Order(StringComparer.Ordinal);

        Assert.Equal(
            new[]
            {
                nameof(PermittedServiceWrite.AuditEvent),
                nameof(PermittedServiceWrite.LegitimacyCheckState),
                nameof(PermittedServiceWrite.RetentionPurge),
                nameof(PermittedServiceWrite.SignInBookkeeping),
            }.Order(StringComparer.Ordinal),
            members);
    }

    [Fact]
    public void TheRegistry_DeclaresTheLegitimacyCheckUseCase()
    {
        var declaration = Assert.Contains(typeof(CheckLegitimacyUseCase), PermittedServiceWrites.Declarations);

        Assert.Equal(PermittedServiceWrite.LegitimacyCheckState, declaration);
    }

    [Fact]
    public void TheRegistry_DeclaresNothingElse()
    {
        // Every entry is a use case of the Application layer performing a write of the BR-026 list; a
        // registry that grew silently is exactly what AC-004 forbids.
        Assert.All(
            PermittedServiceWrites.Declarations,
            entry => Assert.Equal(typeof(GetLegitimacyModeQuery).Namespace, entry.Key.Namespace));
        Assert.Single(PermittedServiceWrites.Declarations);
    }
}
