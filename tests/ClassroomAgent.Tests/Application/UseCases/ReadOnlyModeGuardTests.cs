using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-007 AC-001, AC-002: a write refuses while the installation is in read-only mode - for all three
/// reasons of BR-025, with the same refusal type - and the refusal carries the reason as data, the
/// operation, and the last successful check where one exists (spec FR-001 … FR-003).
/// </summary>
public sealed class ReadOnlyModeGuardTests(PostgreSqlFixture database)
{
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheWriteIsRefused(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, cause, ct);

        await Assert.ThrowsAsync<ReadOnlyModeException>(() => ExecuteWriteAsync(host, ct));
    }

    [Theory]
    [InlineData(ReadOnlyModeHost.Cause.NeverConfirmed, LegitimacyModeReason.NotYetConfirmed)]
    [InlineData(ReadOnlyModeHost.Cause.Suspended, LegitimacyModeReason.SuspendedByOwner)]
    [InlineData(ReadOnlyModeHost.Cause.GracePeriodExpired, LegitimacyModeReason.GracePeriodExpired)]
    public async Task TheRefusal_NamesTheReason(ReadOnlyModeHost.Cause cause, LegitimacyModeReason expected)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, cause, ct);

        var refusal = await Assert.ThrowsAsync<ReadOnlyModeException>(() => ExecuteWriteAsync(host, ct));

        Assert.Equal(expected, refusal.Reason);
    }

    [Theory]
    [InlineData(ReadOnlyModeHost.Cause.Suspended)]
    [InlineData(ReadOnlyModeHost.Cause.GracePeriodExpired)]
    public async Task TheRefusal_CarriesTheLastSuccessfulCheck(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, cause, ct);

        var refusal = await Assert.ThrowsAsync<ReadOnlyModeException>(() => ExecuteWriteAsync(host, ct));

        Assert.Equal(ReadOnlyModeHost.LastSuccessOf(cause), refusal.LastSuccessfulCheckAt);
    }

    [Fact]
    public async Task NeverConfirmed_CarriesNoLastSuccessfulCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, ReadOnlyModeHost.Cause.NeverConfirmed, ct);

        var refusal = await Assert.ThrowsAsync<ReadOnlyModeException>(() => ExecuteWriteAsync(host, ct));

        Assert.Null(refusal.LastSuccessfulCheckAt);
    }

    [Fact]
    public async Task TheRefusal_CarriesTheOperationConstant()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, ReadOnlyModeHost.Cause.Suspended, ct);

        var refusal = await Assert.ThrowsAsync<ReadOnlyModeException>(() => ExecuteWriteAsync(host, ct));

        Assert.Equal(SyntheticWriteUseCase.Operation, refusal.Operation);
    }

    [Fact]
    public async Task TheMessage_IsAFixedStringCarryingNoReason()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, ReadOnlyModeHost.Cause.GracePeriodExpired, ct);

        var refusal = await Assert.ThrowsAsync<ReadOnlyModeException>(() => ExecuteWriteAsync(host, ct));

        // The reason is read as data (NFR-073); the message is a developer diagnostic and is never parsed.
        Assert.DoesNotContain(nameof(LegitimacyModeReason.GracePeriodExpired), refusal.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(
            ReadOnlyModeHost.LastSuccessOf(ReadOnlyModeHost.Cause.GracePeriodExpired)!.Value.ToString("O"),
            refusal.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task ARefusedWrite_CommitsNothing(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, cause, ct);
        var before = await host.LegitimacyStatesAsync(ct);

        await Assert.ThrowsAsync<ReadOnlyModeException>(() => ExecuteWriteAsync(host, ct));

        var after = await host.LegitimacyStatesAsync(ct);
        Assert.Equal(before, after);
        Assert.DoesNotContain(after, r => r.Domain == SyntheticWriteUseCase.MarkerDomain);
    }

    [Fact]
    public async Task NotInReadOnlyMode_TheWriteProceeds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, ReadOnlyModeHost.Cause.NotReadOnly, ct);

        await ExecuteWriteAsync(host, ct);

        var row = Assert.Single(await host.LegitimacyStatesAsync(ct));
        Assert.Equal(SyntheticWriteUseCase.MarkerDomain, row.Domain);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task AnEmptyOperationName_IsRejected(string operation)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, ReadOnlyModeHost.Cause.NotReadOnly, ct);

        var before = await host.LegitimacyStatesAsync(ct);

        await ReadOnlyModeHost.InScopeAsync<IReadOnlyModeGuard>(
            host,
            async guard => await Assert.ThrowsAsync<ArgumentException>(() => guard.EnsureAllowedAsync(operation, ct)));

        Assert.Equal(before, await host.LegitimacyStatesAsync(ct));
    }

    private static Task ExecuteWriteAsync(InstallationTestHost host, CancellationToken cancellationToken) =>
        ReadOnlyModeHost.InScopeAsync<SyntheticWriteUseCase>(
            host,
            useCase => useCase.ExecuteAsync(host.Time.GetUtcNow(), cancellationToken));
}
