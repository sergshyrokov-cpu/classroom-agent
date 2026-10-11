using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Security;

/// <summary>
/// US-045 AC-003 (spec FR-001 … FR-005, FR-008; OD-001; trebovaniya v89 §8, DC-10, SC-10): the Control Plane applies
/// the installation's rule — a matched request logs its route template, an unmatched one the fixed marker, and the
/// address the caller sent reaches no line. The probe lines are the test's own (spec I-6).
/// </summary>
public sealed class RouteTemplateLoggingTests(PostgreSqlFixture database)
{
    private const string UnmatchedValue = "zzq-cp-unmatched-probe";

    private const string AddAdminFormTemplate = "/installations/{id:guid}/admins/new";

    private const string ErrorPageTemplate = "/error/{statusCode}";

    [Fact]
    public async Task AMatchedPage_LogsItsRouteTemplate_AndNeverThePathValue()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var value = Guid.NewGuid().ToString("D");
        var baseline = await RouteProbe.BaselineAsync(host.LogDirectory, ct);

        await owner.GetAsync($"/installations/{value}/admins/new", ct);

        var events = await RouteProbe.WaitAsync(host.LogDirectory, baseline, RouteProbe.AfterPipeline, 1, ct);
        var inEndpoint = RouteProbe.PathsOf(events, RouteProbe.InEndpoint);
        Assert.Contains(AddAdminFormTemplate, inEndpoint);
        Assert.All(inEndpoint, path => Assert.Contains(path, new[] { AddAdminFormTemplate, ErrorPageTemplate }));
        Assert.Equal([RouteProbe.Marker], RouteProbe.PathsOf(events, RouteProbe.BeforeRouting));
        await RouteProbe.AssertTheRuleAsync(host.LogDirectory, value, ct);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AnUnmatchedAddress_LogsTheMarker_AndTheErrorPageRoute_NeverTheValue(bool signedIn)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var owner = await host.CreateOwnerAsync(ct);
        using var anonymous = host.CreateClient();
        var baseline = await RouteProbe.BaselineAsync(host.LogDirectory, ct);

        var response = await (signedIn ? owner : anonymous).GetAsync($"/{UnmatchedValue}/{UnmatchedValue}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        var events = await RouteProbe.WaitAsync(host.LogDirectory, baseline, RouteProbe.AfterPipeline, 1, ct);
        Assert.Equal([RouteProbe.Marker], RouteProbe.PathsOf(events, RouteProbe.BeforeRouting));
        Assert.Equal([ErrorPageTemplate], RouteProbe.PathsOf(events, RouteProbe.InEndpoint));
        await RouteProbe.AssertTheRuleAsync(host.LogDirectory, UnmatchedValue, ct);
    }

    [Fact]
    public async Task AStaticFile_LogsTheMarker_NotItsName()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var file = StaticFiles.AnyFile();
        var baseline = await RouteProbe.BaselineAsync(host.LogDirectory, ct);

        var response = await owner.GetAsync(file, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        var events = await RouteProbe.WaitAsync(host.LogDirectory, baseline, RouteProbe.AfterPipeline, 1, ct);
        Assert.Equal([RouteProbe.Marker], RouteProbe.PathsOf(events, RouteProbe.AfterPipeline));
        await RouteProbe.AssertTheRuleAsync(host.LogDirectory, Path.GetFileName(file), ct);
    }

    private Task<ControlPlaneTestHost> StartAsync(CancellationToken cancellationToken) =>
        ControlPlaneTestHost.StartAsync(database, cancellationToken, configureServices: RouteProbe.Register);
}
