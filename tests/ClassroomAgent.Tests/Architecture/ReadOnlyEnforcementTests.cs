using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Architecture;

/// <summary>
/// US-007 AC-007 second half and AC-011: the rule proven in <see cref="WritePathRuleTests"/> holds over the
/// real <c>ClassroomAgent.Application</c> assembly, the enforcement point exists only in that layer, and the
/// decorators that log a refusal are registered in front of it (spec FR-009 … FR-011; AD-3, AD-6).
/// </summary>
public sealed class ReadOnlyEnforcementTests(PostgreSqlFixture database)
{
    [Fact]
    public void TheApplicationAssembly_HasNoUnprotectedWritePath()
    {
        var violations = WritePathRule.Violations(WritePathRule.ApplicationUseCases(), PermittedServiceWrites.Declarations);

        Assert.Empty(violations);
    }

    [Fact]
    public void TheLegitimacyCheckUseCase_IsARecognisedWritePath()
    {
        // The rule would be vacuous if it enumerated nothing: the one write path that exists must be seen.
        Assert.Contains(WritePathRule.ApplicationUseCases(), WritePathRule.IsProtectedPath);
        Assert.True(WritePathRule.IsProtectedPath(typeof(CheckLegitimacyUseCase)));
    }

    [Fact]
    public void TheGuard_IsImplementedOnlyInsideApplication()
    {
        var outside = new[] { typeof(ClassroomAgent.Web.Program).Assembly, typeof(ClassroomAgent.Infrastructure.Persistence.ClassroomAgentDbContext).Assembly }
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IReadOnlyModeGuard).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false })
            .ToList();

        // Only the logging decorator of FR-009 may live outside; it observes the refusal, never decides it.
        Assert.All(outside, t => Assert.Contains("Logging", t.Name, StringComparison.Ordinal));
    }

    [Fact]
    public void TheWebLayer_HoldsNoCopyOfTheRule()
    {
        var webTypes = typeof(ClassroomAgent.Web.Program).Assembly.GetTypes();

        Assert.DoesNotContain(webTypes, t => typeof(IReadOnlyModeGuard).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false });
        Assert.DoesNotContain(webTypes, t => typeof(IUnitOfWork).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false });
    }

    [Fact]
    public void TheRefusal_CarriesNoHttpConcept()
    {
        // AD-9: application exceptions carry no HTTP concept; the 409 mapping arrives with US-008 (OD-001).
        var members = typeof(ReadOnlyModeException).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain(members, n => n.Contains("Status", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, n => n.Contains("Http", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, n => n.Contains("Problem", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TheDecorators_AreRegisteredInFrontOfTheGuardAndTheUnitOfWork()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, ReadOnlyModeHost.Cause.NotReadOnly, ct);
        using var scope = host.CreateScope();

        var guard = scope.ServiceProvider.GetRequiredService<IReadOnlyModeGuard>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // "Every refusal is logged" is a property of the composition (FR-009), so it is asserted, not assumed.
        Assert.IsNotType<ReadOnlyModeGuard>(guard);
        Assert.IsNotType<ClassroomAgent.Infrastructure.Persistence.UnitOfWork>(unitOfWork);
        Assert.IsNotType<ReadOnlyModeUnitOfWork>(unitOfWork);
    }

    [Fact]
    public async Task TheGuardAndTheScope_AreScopedNotSingleton()
    {
        // AC-008: a singleton would outlive the DbContext it reads through and invite a cached mode.
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, ReadOnlyModeHost.Cause.NotReadOnly, ct);

        using var first = host.CreateScope();
        using var second = host.CreateScope();

        Assert.NotSame(
            first.ServiceProvider.GetRequiredService<ServiceWriteScope>(),
            second.ServiceProvider.GetRequiredService<ServiceWriteScope>());
        Assert.NotSame(
            first.ServiceProvider.GetRequiredService<IReadOnlyModeGuard>(),
            second.ServiceProvider.GetRequiredService<IReadOnlyModeGuard>());
    }
}
