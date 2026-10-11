using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-045 AC-001, AC-002, AC-005 (spec FR-001 … FR-005, FR-008; I-1 … I-6; trebovaniya v89 §8, DC-10, SC-10): a line
/// written inside a request names the route of the page serving it, or the fixed marker when no page does, and never
/// the address the caller sent. The probe lines are the test's own (I-6); the log configuration is the production one.
/// </summary>
public sealed class RouteTemplateLoggingTests(PostgreSqlFixture database)
{
    private const string MeetCodeValue = "zzq-path-probe";

    private const string UnmatchedValue = "zzq-unmatched-probe";

    private const string CourseChoiceTemplate = "/workspace/meet-codes/{meetingCode}/course-choice";

    private const string ErrorPageTemplate = "/error/{statusCode}";

    [Fact]
    public async Task AMatchedPage_LogsItsRouteTemplate_AndNeverThePathValue()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        using var _client = client;
        var baseline = await RouteProbe.BaselineAsync(host.LogDirectory, ct);

        await client.GetAsync($"/workspace/meet-codes/{MeetCodeValue}/course-choice", ct);

        var events = await RouteProbe.WaitAsync(host.LogDirectory, baseline, RouteProbe.AfterPipeline, 1, ct);
        var inEndpoint = RouteProbe.PathsOf(events, RouteProbe.InEndpoint);
        Assert.Contains(CourseChoiceTemplate, inEndpoint);
        Assert.All(inEndpoint, path => Assert.Contains(path, new[] { CourseChoiceTemplate, ErrorPageTemplate }));
        Assert.Equal([RouteProbe.Marker], RouteProbe.PathsOf(events, RouteProbe.BeforeRouting));
        await RouteProbe.AssertTheRuleAsync(host.LogDirectory, MeetCodeValue, ct);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AnUnmatchedAddress_LogsTheMarker_AndTheErrorPageRoute_NeverTheValue(bool signedIn)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await StartAsync(ct);
        await using var _host = host;
        using var _admin = admin;
        using var client = signedIn ? null : host.CreateClient();
        var baseline = await RouteProbe.BaselineAsync(host.LogDirectory, ct);

        var response = await (client ?? admin).GetAsync($"/{UnmatchedValue}/{UnmatchedValue}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        var events = await RouteProbe.WaitAsync(host.LogDirectory, baseline, RouteProbe.AfterPipeline, 1, ct);
        Assert.Equal([RouteProbe.Marker], RouteProbe.PathsOf(events, RouteProbe.BeforeRouting));
        Assert.Equal([ErrorPageTemplate], RouteProbe.PathsOf(events, RouteProbe.InEndpoint));
        await RouteProbe.AssertTheRuleAsync(host.LogDirectory, UnmatchedValue, ct);
    }

    [Fact]
    public async Task AnUnmatchedAddressOnThePrivatePort_LogsTheMarker_EvenOnceTheFallbackHasAnswered()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        using var _client = client;
        var baseline = await RouteProbe.BaselineAsync(host.LogDirectory, ct);

        var response = await host.SendPrivateAsync("GET", $"/{UnmatchedValue}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        var events = await RouteProbe.WaitAsync(host.LogDirectory, baseline, RouteProbe.AfterPipeline, 1, ct);
        Assert.Equal([RouteProbe.Marker], RouteProbe.PathsOf(events, RouteProbe.BeforeRouting));
        Assert.Equal([RouteProbe.Marker], RouteProbe.PathsOf(events, RouteProbe.AfterPipeline));
        await RouteProbe.AssertTheRuleAsync(host.LogDirectory, UnmatchedValue, ct);
    }

    [Fact]
    public async Task AStaticFile_LogsTheMarker_NotItsName()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        using var _client = client;
        var baseline = await RouteProbe.BaselineAsync(host.LogDirectory, ct);

        var response = await client.GetAsync("/css/site.css", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        var events = await RouteProbe.WaitAsync(host.LogDirectory, baseline, RouteProbe.AfterPipeline, 1, ct);
        Assert.Equal([RouteProbe.Marker], RouteProbe.PathsOf(events, RouteProbe.AfterPipeline));
        await RouteProbe.AssertTheRuleAsync(host.LogDirectory, "site.css", ct);
    }

    private Task<(InstallationTestHost Host, FormClient Client)> StartAsync(CancellationToken cancellationToken) =>
        JournalHostExtensions.StartAsync(
            database,
            Actor.Admin,
            cancellationToken,
            beforeStart: host =>
            {
                var previous = host.ConfigureServices;
                host.ConfigureServices = services =>
                {
                    previous?.Invoke(services);
                    RouteProbe.Register(services);
                };
            });
}
