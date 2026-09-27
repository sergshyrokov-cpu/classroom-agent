using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-013 AC-001: readiness answers <c>Unhealthy</c> when the synchronization background service is not running,
/// keeps answering <c>Degraded</c> in read-only mode, and stays on the private port with the state only
/// (spec FR-014, FR-016; DC-11, DC-7, SC-9).
/// </summary>
public sealed class SyncReadinessTests(PostgreSqlFixture database)
{
    /// <summary>AC-001: the host registers the service, so a running installation is not Unhealthy.</summary>
    [Fact]
    public async Task WithTheServiceRunning_ReadinessIsNotUnhealthy()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct);
        await host.WaitForFinishedRunAsync(ct);

        var readiness = await host.ReadinessAsync(ct);

        Assert.NotEqual("Unhealthy", readiness);
    }

    /// <summary>AC-001: the marker says the service is running once the host has started (spec FR-014).</summary>
    [Fact]
    public async Task WithTheServiceRunning_TheMarkerSaysSo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct);
        await host.WaitForFinishedRunAsync(ct);

        Assert.True(SyncHostExtensions.MemoryOf(host).IsRunning);
    }

    /// <summary>
    /// AC-001, DC-11: when the service is not running the installation cannot serve users — readiness is
    /// <c>Unhealthy</c> and answers 503, exactly as it does for an unreachable database.
    /// </summary>
    [Fact]
    public async Task WithoutTheServiceRunning_ReadinessIsUnhealthy503()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct);
        await host.WaitForFinishedRunAsync(ct);

        SyncHostExtensions.MemoryOf(host).MarkStopped();
        var response = await host.SendPrivateAsync("GET", "/health/ready", ct);

        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, response.Status);
        Assert.Equal("Unhealthy", response.Body);
    }

    /// <summary>
    /// AC-001, DC-7: read-only mode stays <c>Degraded</c> with 200. The service runs and deliberately skips its
    /// runs; reporting that as an outage would cut off viewing and exporting.
    /// </summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_ReadinessStaysDegraded200(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct, cause);
        await HostLogs.WaitForEventAsync(host.LogDirectory, SyncTestData.LogEvents.RunSkipped, 1, ct);

        var response = await host.SendPrivateAsync("GET", "/health/ready", ct);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.Status);
        Assert.Equal("Degraded", response.Body);
    }

    /// <summary>
    /// AC-001: the service not running outranks read-only mode — <c>Unhealthy</c> is reported when both hold
    /// (spec FR-014, the stated precedence).
    /// </summary>
    [Fact]
    public async Task TheServiceNotRunning_OutranksReadOnlyMode()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct, ReadOnlyModeHost.Cause.Suspended);
        await HostLogs.WaitForEventAsync(host.LogDirectory, SyncTestData.LogEvents.RunSkipped, 1, ct);

        SyncHostExtensions.MemoryOf(host).MarkStopped();
        var response = await host.SendPrivateAsync("GET", "/health/ready", ct);

        Assert.Equal("Unhealthy", response.Body);
    }

    /// <summary>AC-001, FR-016: readiness answers the state and nothing else — no detail about the service.</summary>
    [Fact]
    public async Task Readiness_AnswersTheStateOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct);
        await host.WaitForFinishedRunAsync(ct);

        var response = await host.SendPrivateAsync("GET", "/health/ready", ct);

        Assert.Contains(response.Body, new[] { "Healthy", "Degraded", "Unhealthy" });
        Assert.DoesNotContain("sync", response.Body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>AC-001, SC-9: readiness is not reachable on the public port — unchanged by this Story.</summary>
    [Fact]
    public async Task Readiness_IsNotReachableOnThePublicPort()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(database, ct);

        var response = await host.SendPublicAsync("GET", "/health/ready", ct);

        Assert.NotEqual(System.Net.HttpStatusCode.OK, response.Status);
    }
}
