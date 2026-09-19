using System.Net;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-007 AC-005: reading and exporting are never blocked. The enforcement point applies to writes and to
/// Google calls only, and a read is not made to depend on the legitimacy state (spec FR-008). Read-only is
/// a normal mode, so readiness keeps answering HTTP 200 (US-005 FR-013).
/// </summary>
public sealed class ReadsInReadOnlyModeTests(PostgreSqlFixture database)
{
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task TheModeQuery_AnswersInReadOnlyMode(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, cause, ct);

        using var scope = host.CreateScope();
        var mode = await scope.ServiceProvider.GetRequiredService<GetLegitimacyModeQuery>().ExecuteAsync(ct);

        Assert.True(mode.IsReadOnly);
        Assert.NotNull(mode.Reason);
    }

    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task TheReadinessQuery_AnswersInReadOnlyMode(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, cause, ct);

        using var scope = host.CreateScope();
        var readiness = await scope.ServiceProvider.GetRequiredService<GetReadinessQuery>().ExecuteAsync(ct);

        Assert.Equal(ReadinessState.Degraded, readiness);
    }

    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task Readiness_StaysTwoHundred(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, cause, ct);

        var response = await host.SendPrivateAsync("GET", "/health/ready", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
    }

    [Fact]
    public async Task AReadUseCase_TakesNeitherTheGuardNorTheScope()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, ReadOnlyModeHost.Cause.Suspended, ct);

        // Resolving the read use cases proves they need nothing the enforcement introduced.
        using var scope = host.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<GetLegitimacyModeQuery>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<GetReadinessQuery>());

        var parameters = new[] { typeof(GetLegitimacyModeQuery), typeof(GetReadinessQuery) }
            .SelectMany(t => t.GetConstructors())
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .ToList();
        Assert.DoesNotContain(typeof(IReadOnlyModeGuard), parameters);
        Assert.DoesNotContain(typeof(ServiceWriteScope), parameters);
    }
}
