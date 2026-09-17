using System.Net;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-005 AC-014: liveness and readiness answer only on the private port, with the state only;
/// readiness is <c>Unhealthy</c> (503) without the database, <c>Degraded</c> (200) in read-only mode or
/// after an unsuccessful check within the grace period, <c>Healthy</c> otherwise (spec FR-013; api-design §7;
/// DC-6, DC-11; TC-5).
/// </summary>
public sealed class HealthEndpointTests(PostgreSqlFixture database)
{
    private static readonly TimeSpan AfterSuccess = TimeSpan.FromHours(6);
    private static readonly TimeSpan AfterFailure = TimeSpan.FromMinutes(15);

    [Fact]
    public async Task Liveness_OnPrivatePort_IsHealthy()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var response = await host.SendPrivateAsync("GET", "/health/live", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("Healthy", response.Body);
        Assert.StartsWith("text/plain", response.ContentType ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Liveness_IsHealthy_EvenWithoutTheDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[InstallationConfigurationKeys.ConnectionString] = UnreachableDatabase(host.ConnectionString);
        host.Start();

        var response = await host.SendPrivateAsync("GET", "/health/live", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("Healthy", response.Body);
    }

    [Fact]
    public async Task Readiness_DatabaseUnreachable_IsUnhealthy503()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[InstallationConfigurationKeys.ConnectionString] = UnreachableDatabase(host.ConnectionString);
        host.Start();

        var response = await host.SendPrivateAsync("GET", "/health/ready", ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.Status);
        Assert.Equal("Unhealthy", response.Body);
    }

    [Fact]
    public async Task Readiness_NeverConfirmed_IsDegraded200()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var response = await host.SendPrivateAsync("GET", "/health/ready", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("Degraded", response.Body);
    }

    [Fact]
    public async Task Readiness_AfterASuccessfulCheck_ActiveAndRecent_IsHealthy()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.ControlPlane.ReplySuccess();
        host.Start();
        await host.WaitForNextCheckAtAsync(host.Time.GetUtcNow() + AfterSuccess, ct);

        var response = await host.SendPrivateAsync("GET", "/health/ready", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("Healthy", response.Body);
    }

    [Fact]
    public async Task Readiness_Suspended_IsDegraded()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.ControlPlane.Reply(FakeControlPlaneClient.Answer(Domain.Enums.InstallationStatus.Suspended));
        host.Start();
        await host.WaitForNextCheckAtAsync(host.Time.GetUtcNow() + AfterSuccess, ct);

        var response = await host.SendPrivateAsync("GET", "/health/ready", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("Degraded", response.Body);
    }

    [Fact]
    public async Task Readiness_GracePeriodExpired_IsDegraded()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await host.InsertLegitimacyStateAsync(ct, host.Time.GetUtcNow() - TimeSpan.FromDays(8));
        host.Start();

        var response = await host.SendPrivateAsync("GET", "/health/ready", ct);

        Assert.Equal("Degraded", response.Body);
    }

    [Fact]
    public async Task Readiness_LastCheckFailed_WithinGrace_IsDegraded_UntilASuccess()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var start = host.Time.GetUtcNow();
        await host.InsertLegitimacyStateAsync(ct, start - TimeSpan.FromDays(1));
        host.ControlPlane.ReplyFailure(CheckFailureCategory.Unreachable).ReplySuccess();
        host.Start();
        await host.WaitForNextCheckAtAsync(start + AfterFailure, ct);

        var degraded = await host.SendPrivateAsync("GET", "/health/ready", ct);
        host.Time.Advance(AfterFailure);
        await host.WaitForNextCheckAtAsync(start + AfterFailure + AfterSuccess, ct);
        var healthy = await host.SendPrivateAsync("GET", "/health/ready", ct);

        Assert.Equal(HttpStatusCode.OK, degraded.Status);
        Assert.Equal("Degraded", degraded.Body);
        Assert.Equal("Healthy", healthy.Body);
    }

    [Fact]
    public async Task Readiness_BeforeTheFirstCheckCompletes_UsesStoredStateOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await host.InsertLegitimacyStateAsync(ct, host.Time.GetUtcNow() - TimeSpan.FromHours(3));
        host.ControlPlane.ReplyLater();
        host.Start();
        await host.ControlPlane.WaitForCallsAsync(1, ct);

        var response = await host.SendPrivateAsync("GET", "/health/ready", ct);

        Assert.Equal("Healthy", response.Body);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthPaths_OnPublicPort_Return404EmptyBody(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var response = await host.SendPublicAsync("GET", path, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.Equal(string.Empty, response.Body);
    }

    [Theory]
    [InlineData("/health/live", "Host", "localhost:8081")]
    [InlineData("/health/ready", "Host", "localhost:8081")]
    [InlineData("/health/ready", "X-Forwarded-Host", "localhost:8081")]
    [InlineData("/health/live", "X-Forwarded-Port", "8081")]
    public async Task HealthPaths_OnPublicPort_WithForgedHeaders_Return404(string path, string header, string value)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var response = await host.SendPublicAsync("GET", path, ct, new Dictionary<string, string> { [header] = value });

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.DoesNotContain("Healthy", response.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Degraded", response.Body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("POST", "/health/live")]
    [InlineData("POST", "/health/ready")]
    [InlineData("DELETE", "/health/ready")]
    public async Task HealthPaths_OtherMethods_AreNotHandled(string method, string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var response = await host.SendPrivateAsync(method, path, ct);

        Assert.True(
            response.Status is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"{method} {path}: expected 404 or 405, got {(int)response.Status}.");
    }

    [Fact]
    public async Task Readiness_BodyIsTheStateWordOnly_NoReasonTimeOrVersion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await host.InsertLegitimacyStateAsync(ct, host.Time.GetUtcNow() - TimeSpan.FromDays(9), status: "suspended");
        host.Start();

        var response = await host.SendPrivateAsync("GET", "/health/ready", ct);

        Assert.Equal("Degraded", response.Body);
        Assert.DoesNotContain("Suspended", response.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(InstallationTestData.Domain, response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrivatePort_HasNoHstsOrHttpsRedirect()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var response = await host.SendPrivateAsync("GET", "/health/live", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.False(response.Headers.ContainsKey("Strict-Transport-Security"));
        Assert.False(response.Headers.ContainsKey("Location"));
    }

    private static string UnreachableDatabase(string connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = "missing_" + Guid.NewGuid().ToString("N"),
            Timeout = 2,
        }.ConnectionString;
}
