using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-005 AC-008, AC-010, AC-015: the installation logs each unsuccessful check at <c>Error</c> with its
/// category, <c>upgrade_recommended</c> at <c>Warning</c>, entering read-only mode once at <c>Warning</c>
/// with the reason, leaving it once at <c>Information</c>, and result changes at <c>Information</c> —
/// with identifiers, categories and states only (spec FR-007, FR-010, FR-012; DC-10; SC-10). Event names
/// are fixed by the test strategy §3.
/// </summary>
public sealed class LegitimacyLoggingTests(PostgreSqlFixture database)
{
    private static readonly TimeSpan AfterSuccess = TimeSpan.FromHours(6);
    private static readonly TimeSpan AfterFailure = TimeSpan.FromMinutes(15);

    [Fact]
    public async Task Sequence_LogsModeChangesOnce_FailuresEachTime_ResultChangesOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var t = host.Time.GetUtcNow();
        host.ControlPlane
            .ReplySuccess()
            .ReplySuccess()
            .ReplyFailure(CheckFailureCategory.Unreachable)
            .ReplyFailure(CheckFailureCategory.Unreachable)
            .ReplySuccess();

        host.Start();
        await host.WaitForNextCheckAtAsync(t += AfterSuccess, ct);
        host.Time.Advance(AfterSuccess);
        await host.WaitForNextCheckAtAsync(t += AfterSuccess, ct);
        host.Time.Advance(AfterSuccess);
        await host.WaitForNextCheckAtAsync(t += AfterFailure, ct);
        host.Time.Advance(AfterFailure);
        await host.WaitForNextCheckAtAsync(t += AfterFailure, ct);
        host.Time.Advance(AfterFailure);
        await host.WaitForNextCheckAtAsync(t + AfterSuccess, ct);
        var events = await host.ReadLogEventsAsync(ct);

        var entered = Assert.Single(events, e => e.EventName == "ReadOnlyModeEntered");
        Assert.Equal("Warning", entered.Level);
        Assert.Equal(nameof(LegitimacyModeReason.NotYetConfirmed), entered.Property("Reason"));
        var left = Assert.Single(events, e => e.EventName == "ReadOnlyModeLeft");
        Assert.Equal("Information", left.Level);

        var failed = events.Where(e => e.EventName == "LegitimacyCheckFailed").ToList();
        Assert.Equal(2, failed.Count);
        Assert.All(failed, e =>
        {
            Assert.Equal("Error", e.Level);
            Assert.Equal(nameof(CheckFailureCategory.Unreachable), e.Property("Category"));
        });

        var changed = events.Where(e => e.EventName == "LegitimacyCheckResultChanged").ToList();
        Assert.All(changed, e => Assert.Equal("Information", e.Level));
        Assert.InRange(changed.Count, 2, 3);
    }

    [Theory]
    [InlineData(CheckFailureCategory.Unreachable)]
    [InlineData(CheckFailureCategory.Timeout)]
    [InlineData(CheckFailureCategory.ErrorAnswer)]
    [InlineData(CheckFailureCategory.UnparseableAnswer)]
    [InlineData(CheckFailureCategory.UnknownInstallation)]
    public async Task EachFailureCategory_IsLoggedAtError_WithItsName(CheckFailureCategory category)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.ControlPlane.ReplyFailure(category);

        host.Start();
        await host.WaitForNextCheckAtAsync(host.Time.GetUtcNow() + AfterFailure, ct);
        var events = await host.ReadLogEventsAsync(ct);

        var failed = Assert.Single(events, e => e.EventName == "LegitimacyCheckFailed");
        Assert.Equal("Error", failed.Level);
        Assert.Equal(category.ToString(), failed.Property("Category"));
    }

    [Fact]
    public async Task UpgradeRequired_IsLoggedAtError_WithCategoryUpgradeRequired()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.ControlPlane.Reply(FakeControlPlaneClient.Answer(compatibility: CompatibilityState.UpgradeRequired));

        host.Start();
        await host.WaitForNextCheckAtAsync(host.Time.GetUtcNow() + AfterFailure, ct);
        var events = await host.ReadLogEventsAsync(ct);

        var failed = Assert.Single(events, e => e.EventName == "LegitimacyCheckFailed");
        Assert.Equal("Error", failed.Level);
        Assert.Equal("UpgradeRequired", failed.Property("Category"));
    }

    [Fact]
    public async Task UpgradeRecommended_IsLoggedAtWarning_AndIsNotAFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.ControlPlane.Reply(FakeControlPlaneClient.Answer(compatibility: CompatibilityState.UpgradeRecommended));

        host.Start();
        await host.WaitForNextCheckAtAsync(host.Time.GetUtcNow() + AfterSuccess, ct);
        var events = await host.ReadLogEventsAsync(ct);

        Assert.Equal("Warning", Assert.Single(events, e => e.EventName == "LegitimacyUpgradeRecommended").Level);
        Assert.DoesNotContain(events, e => e.EventName == "LegitimacyCheckFailed");
    }

    [Fact]
    public async Task SuspendedAnswer_EntersReadOnly_WithReasonSuspendedByOwner()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await host.InsertLegitimacyStateAsync(ct, host.Time.GetUtcNow() - TimeSpan.FromHours(1));
        host.ControlPlane.Reply(FakeControlPlaneClient.Answer(InstallationStatus.Suspended));

        host.Start();
        await host.WaitForNextCheckAtAsync(host.Time.GetUtcNow() + AfterSuccess, ct);
        var events = await host.ReadLogEventsAsync(ct);

        var entered = Assert.Single(events, e => e.EventName == "ReadOnlyModeEntered");
        Assert.Equal(nameof(LegitimacyModeReason.SuspendedByOwner), entered.Property("Reason"));
        Assert.DoesNotContain(events, e => e.EventName == "ReadOnlyModeLeft");
    }

    [Fact]
    public async Task StartingInReadOnly_FromStoredExpiredState_IsLoggedAsEntering()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await host.InsertLegitimacyStateAsync(ct, host.Time.GetUtcNow() - TimeSpan.FromDays(10));
        host.ControlPlane.ReplyFailure(CheckFailureCategory.Unreachable);

        host.Start();
        await host.WaitForNextCheckAtAsync(host.Time.GetUtcNow() + AfterFailure, ct);
        var events = await host.ReadLogEventsAsync(ct);

        var entered = Assert.Single(events, e => e.EventName == "ReadOnlyModeEntered");
        Assert.Equal("Warning", entered.Level);
        Assert.Equal(nameof(LegitimacyModeReason.GracePeriodExpired), entered.Property("Reason"));
    }

    [Fact]
    public async Task StartingActiveAndRecent_LogsNoReadOnlyEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await host.InsertLegitimacyStateAsync(ct, host.Time.GetUtcNow() - TimeSpan.FromHours(2));
        host.ControlPlane.ReplySuccess();

        host.Start();
        await host.WaitForNextCheckAtAsync(host.Time.GetUtcNow() + AfterSuccess, ct);
        var events = await host.ReadLogEventsAsync(ct);

        Assert.DoesNotContain(events, e => e.EventName is "ReadOnlyModeEntered" or "ReadOnlyModeLeft");
    }

    [Fact]
    public async Task Logs_CarryNoDomainClientIdOrExceptionTextFromTheCall()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var t = host.Time.GetUtcNow();
        host.ControlPlane
            .Reply(FakeControlPlaneClient.Answer(compatibility: CompatibilityState.UpgradeRecommended, domain: "zz-domain-marker.example.test", clientId: "7777777777777"))
            .Throw(new HttpRequestException("zz-exception-marker zz-domain-marker.example.test"))
            .Reply(FakeControlPlaneClient.Answer(compatibility: CompatibilityState.UpgradeRequired, domain: "zz-domain-marker.example.test", clientId: "7777777777777"));

        host.Start();
        await host.WaitForNextCheckAtAsync(t += AfterSuccess, ct);
        host.Time.Advance(AfterSuccess);
        await host.WaitForNextCheckAtAsync(t += AfterFailure, ct);
        host.Time.Advance(AfterFailure);
        await host.WaitForNextCheckAtAsync(t + AfterFailure, ct);
        var logs = string.Join('\n', await host.ReadLogFilesAsync(ct));

        Assert.NotEmpty(logs);
        Assert.DoesNotContain("zz-domain-marker", logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("7777777777777", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("zz-exception-marker", logs, StringComparison.Ordinal);
    }
}
