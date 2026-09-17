using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-005 AC-007, AC-008, AC-011: a successful check records time, status, compatibility, domain and
/// client ID in the single <c>legitimacy_state</c> row; an unsuccessful one keeps the last success, and
/// only <c>upgrade_required</c> still records the answer (spec FR-007, FR-009; db-design §4.2).
/// </summary>
public sealed class CheckLegitimacyRecordingTests(PostgreSqlFixture database)
{
    private static readonly TimeSpan AfterSuccess = TimeSpan.FromHours(6);
    private static readonly TimeSpan AfterFailure = TimeSpan.FromMinutes(15);

    [Fact]
    public async Task SuccessfulCheck_WithNoRow_CreatesTheRowWithTheAnswer()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var start = host.Time.GetUtcNow();
        host.ControlPlane.ReplySuccess();

        host.Start();
        await host.WaitForNextCheckAtAsync(start + AfterSuccess, ct);

        var row = Assert.Single(await host.LegitimacyStatesAsync(ct));
        Assert.True(row.Singleton);
        Assert.Equal(start, row.LastSuccessfulCheckAt);
        Assert.Equal("active", row.Status);
        Assert.Equal("supported", row.Compatibility);
        Assert.Equal(InstallationTestData.Domain, row.Domain);
        Assert.Equal(InstallationTestData.ClientId, row.ClientId);
        Assert.Equal(start, row.CreatedAt);
        Assert.Equal(start, row.UpdatedAt);
    }

    [Theory]
    [InlineData(InstallationStatus.Suspended, CompatibilityState.Supported, "suspended", "supported")]
    [InlineData(InstallationStatus.Active, CompatibilityState.UpgradeRecommended, "active", "upgrade_recommended")]
    [InlineData(InstallationStatus.Suspended, CompatibilityState.UpgradeRecommended, "suspended", "upgrade_recommended")]
    public async Task SuspendedOrUpgradeRecommendedAnswer_IsASuccessfulCheck(
        InstallationStatus status,
        CompatibilityState compatibility,
        string storedStatus,
        string storedCompatibility)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var start = host.Time.GetUtcNow();
        host.ControlPlane.Reply(FakeControlPlaneClient.Answer(status, compatibility));

        host.Start();
        await host.WaitForNextCheckAtAsync(start + AfterSuccess, ct);

        var row = Assert.Single(await host.LegitimacyStatesAsync(ct));
        Assert.Equal(start, row.LastSuccessfulCheckAt);
        Assert.Equal(storedStatus, row.Status);
        Assert.Equal(storedCompatibility, row.Compatibility);
    }

    [Fact]
    public async Task SuccessfulCheck_WithAnExistingRow_UpdatesItInPlace()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var start = host.Time.GetUtcNow();
        var earlier = start - TimeSpan.FromDays(3);
        await host.InsertLegitimacyStateAsync(ct, earlier, status: "suspended", compatibility: "upgrade_recommended", clientId: "9999999999");
        var before = Assert.Single(await host.LegitimacyStatesAsync(ct));
        host.ControlPlane.Reply(FakeControlPlaneClient.Answer(clientId: InstallationTestData.OtherClientId));

        host.Start();
        await host.WaitForNextCheckAtAsync(start + AfterSuccess, ct);

        var row = Assert.Single(await host.LegitimacyStatesAsync(ct));
        Assert.Equal(before.Id, row.Id);
        Assert.Equal(start, row.LastSuccessfulCheckAt);
        Assert.Equal("active", row.Status);
        Assert.Equal("supported", row.Compatibility);
        Assert.Equal(InstallationTestData.OtherClientId, row.ClientId);
        Assert.Equal(before.CreatedAt, row.CreatedAt);
        Assert.Equal(start, row.UpdatedAt);
    }

    [Fact]
    public async Task UpgradeRequired_WithNoRow_RecordsAnswerWithoutLastSuccess()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var start = host.Time.GetUtcNow();
        host.ControlPlane.Reply(FakeControlPlaneClient.Answer(
            InstallationStatus.Suspended,
            CompatibilityState.UpgradeRequired,
            InstallationTestData.OtherDomain,
            InstallationTestData.OtherClientId));

        host.Start();
        await host.WaitForNextCheckAtAsync(start + AfterFailure, ct);

        var row = Assert.Single(await host.LegitimacyStatesAsync(ct));
        Assert.Null(row.LastSuccessfulCheckAt);
        Assert.Equal("suspended", row.Status);
        Assert.Equal("upgrade_required", row.Compatibility);
        Assert.Equal(InstallationTestData.OtherDomain, row.Domain);
        Assert.Equal(InstallationTestData.OtherClientId, row.ClientId);
    }

    [Fact]
    public async Task UpgradeRequired_WithAnExistingRow_KeepsLastSuccess_RecordsTheRest()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var start = host.Time.GetUtcNow();
        var lastSuccess = start - TimeSpan.FromDays(2);
        await host.InsertLegitimacyStateAsync(ct, lastSuccess);
        host.ControlPlane.Reply(FakeControlPlaneClient.Answer(
            InstallationStatus.Suspended,
            CompatibilityState.UpgradeRequired,
            clientId: InstallationTestData.OtherClientId));

        host.Start();
        await host.WaitForNextCheckAtAsync(start + AfterFailure, ct);

        var row = Assert.Single(await host.LegitimacyStatesAsync(ct));
        Assert.Equal(lastSuccess, row.LastSuccessfulCheckAt);
        Assert.Equal("suspended", row.Status);
        Assert.Equal("upgrade_required", row.Compatibility);
        Assert.Equal(InstallationTestData.OtherClientId, row.ClientId);
    }

    [Theory]
    [InlineData(CheckFailureCategory.Unreachable)]
    [InlineData(CheckFailureCategory.Timeout)]
    [InlineData(CheckFailureCategory.ErrorAnswer)]
    [InlineData(CheckFailureCategory.UnparseableAnswer)]
    [InlineData(CheckFailureCategory.UnknownInstallation)]
    public async Task UnsuccessfulCheck_WithAnExistingRow_ChangesNothing(CheckFailureCategory category)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var start = host.Time.GetUtcNow();
        await host.InsertLegitimacyStateAsync(ct, start - TimeSpan.FromDays(1), stampedAt: start - TimeSpan.FromDays(1));
        var before = Assert.Single(await host.LegitimacyStatesAsync(ct));
        host.ControlPlane.ReplyFailure(category);

        host.Start();
        await host.WaitForNextCheckAtAsync(start + AfterFailure, ct);

        Assert.Equal(before, Assert.Single(await host.LegitimacyStatesAsync(ct)));
    }

    [Theory]
    [InlineData(CheckFailureCategory.Unreachable)]
    [InlineData(CheckFailureCategory.UnknownInstallation)]
    public async Task UnsuccessfulCheck_WithNoRow_CreatesNoRow(CheckFailureCategory category)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var start = host.Time.GetUtcNow();
        host.ControlPlane.ReplyFailure(category);

        host.Start();
        await host.WaitForNextCheckAtAsync(start + AfterFailure, ct);

        Assert.Empty(await host.LegitimacyStatesAsync(ct));
    }

    [Fact]
    public async Task ManyChecks_KeepExactlyOneRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var start = host.Time.GetUtcNow();
        host.ControlPlane
            .ReplySuccess()
            .Reply(FakeControlPlaneClient.Answer(InstallationStatus.Suspended))
            .Reply(FakeControlPlaneClient.Answer(compatibility: CompatibilityState.UpgradeRequired))
            .ReplySuccess();

        host.Start();
        var due = start + AfterSuccess;
        await host.WaitForNextCheckAtAsync(due, ct);
        host.Time.Advance(AfterSuccess);
        due += AfterSuccess;
        await host.WaitForNextCheckAtAsync(due, ct);
        host.Time.Advance(AfterSuccess);
        await host.WaitForNextCheckAtAsync(due + AfterFailure, ct);
        host.Time.Advance(AfterFailure);
        await host.WaitForNextCheckAtAsync(due + AfterFailure + AfterSuccess, ct);

        var row = Assert.Single(await host.LegitimacyStatesAsync(ct));
        Assert.Equal(due + AfterFailure, row.LastSuccessfulCheckAt);
        Assert.Equal("active", row.Status);
        Assert.Equal("supported", row.Compatibility);
    }

    [Fact]
    public async Task Restart_ReadsTheStoredRow_AndTheNextSuccessUpdatesIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var first = await InstallationTestHost.CreateAsync(database, ct);
        var start = first.Time.GetUtcNow();
        first.ControlPlane.ReplySuccess();
        first.Start();
        await first.WaitForNextCheckAtAsync(start + AfterSuccess, ct);
        await first.StopAsync();

        var later = start + TimeSpan.FromDays(2);
        await using var second = InstallationTestHost.Restart(first, later);
        second.ControlPlane.ReplySuccess();
        second.Start();
        await second.WaitForNextCheckAtAsync(later + AfterSuccess, ct);

        var row = Assert.Single(await second.LegitimacyStatesAsync(ct));
        Assert.Equal(later, row.LastSuccessfulCheckAt);
        Assert.Equal(start, row.CreatedAt);
    }
}
