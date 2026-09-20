using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Infrastructure.ReadOnly;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Architecture;

/// <summary>
/// US-008 FR-021: the two Minor findings carried out of the US-007 security review are corrected here, because
/// this Story is the next to touch their files — F-1, the undecorated unit of work must be unreachable from DI,
/// and F-2, a read-only refusal must never be downgraded into an ordinary save failure (S-20, SC-5).
/// </summary>
public sealed class SignInWiringTests(PostgreSqlFixture database)
{
    /// <summary>F-1: resolving the port yields the decorated chain.</summary>
    [Fact]
    public async Task ResolvingTheUnitOfWork_YieldsTheDecoratedChain()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var scope = host.CreateScope();

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        Assert.IsType<LoggingUnitOfWork>(unitOfWork);
    }

    /// <summary>
    /// F-1: no bare registration of the undecorated unit of work remains, so no type in <c>Web</c> can inject it
    /// and commit past the read-only backstop.
    /// </summary>
    [Fact]
    public async Task TheUndecoratedUnitOfWork_IsUnreachableFromDi()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var scope = host.CreateScope();

        Assert.Null(scope.ServiceProvider.GetService<UnitOfWork>());
    }

    /// <summary>F-1: the same holds for the bare guard — only the decorated one is registered.</summary>
    [Fact]
    public async Task TheUndecoratedReadOnlyGuard_IsUnreachableFromDi()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var scope = host.CreateScope();

        Assert.Null(scope.ServiceProvider.GetService<ReadOnlyModeGuard>());
        Assert.IsType<LoggingReadOnlyModeGuard>(scope.ServiceProvider.GetRequiredService<IReadOnlyModeGuard>());
    }

    /// <summary>
    /// F-2: <c>CheckLegitimacyUseCase</c> rethrows a read-only refusal instead of folding it into
    /// <c>SaveFailed</c>. The commit is made to refuse, so the only question the test asks is which of the two
    /// the use case does with it — today it answers <c>SaveFailed</c>, which is exactly the finding.
    /// </summary>
    [Fact]
    public async Task AReadOnlyRefusal_IsNotDowngradedIntoASaveFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider(InstallationTestHost.DefaultStart);
        var controlPlane = new FakeControlPlaneClient(clock);
        controlPlane.ReplySuccess();
        var useCase = new CheckLegitimacyUseCase(
            controlPlane,
            new EmptyLegitimacyStateRepository(),
            new RefusingUnitOfWork(),
            new ServiceWriteScope(),
            new InstallationIdentity(Guid.NewGuid(), "1.0.0", 1),
            clock);

        var refusal = await Assert.ThrowsAsync<ReadOnlyModeException>(() => useCase.ExecuteAsync(ct));

        Assert.Equal(LegitimacyModeReason.SuspendedByOwner, refusal.Reason);
    }

    /// <summary>F-2: an ordinary save failure is still an outcome, not an exception (AD-9).</summary>
    [Fact]
    public async Task AnOrdinarySaveFailure_IsStillAnOutcome()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider(InstallationTestHost.DefaultStart);
        var controlPlane = new FakeControlPlaneClient(clock);
        controlPlane.ReplySuccess();
        var useCase = new CheckLegitimacyUseCase(
            controlPlane,
            new EmptyLegitimacyStateRepository(),
            new FailingUnitOfWork(),
            new ServiceWriteScope(),
            new InstallationIdentity(Guid.NewGuid(), "1.0.0", 1),
            clock);

        var outcome = await useCase.ExecuteAsync(ct);

        Assert.Equal(LegitimacyCheckFailure.SaveFailed, outcome.Failure);
    }

    /// <summary>A repository with nothing stored: the use case adds the first row.</summary>
    private sealed class EmptyLegitimacyStateRepository : ILegitimacyStateRepository
    {
        public Task<ClassroomAgent.Domain.Entities.LegitimacyState?> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult<ClassroomAgent.Domain.Entities.LegitimacyState?>(null);

        public Task<ClassroomAgent.Domain.Entities.LegitimacyState?> GetForReadAsync(CancellationToken cancellationToken) =>
            Task.FromResult<ClassroomAgent.Domain.Entities.LegitimacyState?>(null);

        public void Add(ClassroomAgent.Domain.Entities.LegitimacyState state)
        {
        }
    }

    /// <summary>A commit the read-only backstop refuses.</summary>
    private sealed class RefusingUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new ReadOnlyModeException(
                LegitimacyModeReason.SuspendedByOwner,
                InstallationTestHost.DefaultStart,
                "legitimacy-check");
    }

    /// <summary>A commit that fails for an ordinary reason — the database refused the row.</summary>
    private sealed class FailingUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The database refused the row.");
    }

    /// <summary>FR-021: the port of the Control Plane channel stays a single port for a single external system (AD-4).</summary>
    [Fact]
    public async Task TheControlPlaneChannel_IsOnePort()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var scope = host.CreateScope();

        var clients = scope.ServiceProvider.GetServices<IControlPlaneClient>().ToList();

        Assert.Single(clients);
    }
}
