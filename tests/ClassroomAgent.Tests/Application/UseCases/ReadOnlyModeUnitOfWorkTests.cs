using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-007 AC-003, AC-004: the commit backstop. In read-only mode a commit no use case declared a
/// permitted service write is refused - the default is refusal, not permission - while a declared write
/// of the BR-026 closed list still commits. Outside read-only mode nothing changes (spec FR-005, FR-006).
/// </summary>
public sealed class ReadOnlyModeUnitOfWorkTests(PostgreSqlFixture database)
{
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task AUseCaseThatForgotTheGuard_StillCannotCommit(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, cause, ct);
        var before = await host.LegitimacyStatesAsync(ct);

        await Assert.ThrowsAsync<ReadOnlyModeException>(() => ReadOnlyModeHost.InScopeAsync<UnguardedWriteUseCase>(
            host,
            useCase => useCase.ExecuteAsync(host.Time.GetUtcNow(), ct)));

        var after = await host.LegitimacyStatesAsync(ct);
        Assert.Equal(before, after);
        Assert.DoesNotContain(after, r => r.Domain == SyntheticWriteUseCase.MarkerDomain);
    }

    [Fact]
    public async Task TheBackstopRefusal_NamesTheBackstopAsItsOperation()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, ReadOnlyModeHost.Cause.Suspended, ct);

        var refusal = await Assert.ThrowsAsync<ReadOnlyModeException>(
            () => ReadOnlyModeHost.InScopeAsync<UnguardedWriteUseCase>(
                host,
                useCase => useCase.ExecuteAsync(host.Time.GetUtcNow(), ct)));

        Assert.Equal(ClassroomAgent.Application.UseCases.ReadOnlyModeUnitOfWork.Operation, refusal.Operation);
    }

    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task ADeclaredServiceWrite_CommitsInReadOnlyMode(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, cause, ct);

        await ReadOnlyModeHost.InScopeAsync<DeclaredServiceWriteUseCase>(
            host,
            useCase => useCase.ExecuteAsync(host.Time.GetUtcNow(), ct));

        var row = Assert.Single(await host.LegitimacyStatesAsync(ct));
        Assert.Equal(SyntheticWriteUseCase.MarkerDomain, row.Domain);
    }

    [Fact]
    public async Task OutsideReadOnlyMode_AnUndeclaredCommitSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, ReadOnlyModeHost.Cause.NotReadOnly, ct);

        await ReadOnlyModeHost.InScopeAsync<UnguardedWriteUseCase>(
            host,
            useCase => useCase.ExecuteAsync(host.Time.GetUtcNow(), ct));

        var row = Assert.Single(await host.LegitimacyStatesAsync(ct));
        Assert.Equal(SyntheticWriteUseCase.MarkerDomain, row.Domain);
    }

    [Fact]
    public async Task TheDeclaration_DoesNotOutliveItsScope()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, ReadOnlyModeHost.Cause.Suspended, ct);
        await ReadOnlyModeHost.InScopeAsync<DeclaredServiceWriteUseCase>(
            host,
            useCase => useCase.ExecuteAsync(host.Time.GetUtcNow(), ct));

        // A second, undeclared write in a new scope is refused again: permission was not left open.
        await Assert.ThrowsAsync<ReadOnlyModeException>(() => ReadOnlyModeHost.InScopeAsync<UnguardedWriteUseCase>(
            host,
            useCase => useCase.ExecuteAsync(host.Time.GetUtcNow(), ct)));
    }
}
