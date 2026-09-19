using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-007 AC-006: in read-only mode no call to Google is made at all. The refusal happens before the port
/// would be called, so nothing leaves the process (spec FR-007; SC-5, SC-8). No Google port exists yet, so
/// the rule and the marker are proven on a synthetic port (test strategy §7).
/// </summary>
public sealed class GoogleDataPortRuleTests(PostgreSqlFixture database)
{
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheGooglePortIsNeverCalled(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, cause, ct);

        await Assert.ThrowsAsync<ReadOnlyModeException>(() => ReadOnlyModeHost.InScopeAsync<SyntheticGoogleUseCase>(
            host,
            useCase => useCase.ExecuteAsync(ct)));

        Assert.Equal(0, host.Services.GetRequiredService<SyntheticGooglePort>().Calls);
    }

    [Fact]
    public async Task OutsideReadOnlyMode_TheGooglePortIsCalled()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, ReadOnlyModeHost.Cause.NotReadOnly, ct);

        await ReadOnlyModeHost.InScopeAsync<SyntheticGoogleUseCase>(host, useCase => useCase.ExecuteAsync(ct));

        Assert.Equal(1, host.Services.GetRequiredService<SyntheticGooglePort>().Calls);
    }

    [Fact]
    public void TheMarker_IsAnEmptyInterfaceInApplicationPorts()
    {
        var marker = typeof(IGoogleDataPort);

        Assert.True(marker.IsInterface);
        Assert.Empty(marker.GetMembers());
        Assert.Equal(typeof(IUnitOfWork).Namespace, marker.Namespace);
    }

    [Fact]
    public void TheMarker_CarriesNoGoogleSdkType()
    {
        // AD-4: no Google SDK type crosses into Application. The marker is the seam, not a dependency.
        var referenced = typeof(IGoogleDataPort).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty);

        Assert.DoesNotContain(referenced, n => n.StartsWith("Google", StringComparison.Ordinal));
    }
}
