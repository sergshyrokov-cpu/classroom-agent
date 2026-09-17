using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Infrastructure.ControlPlane;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Infrastructure.ControlPlane;

/// <summary>
/// US-005 AC-007, AC-008: the installation's HTTP client posts the contract request and classifies every
/// reply — answer, unreachable, timeout after 30 seconds, error answer, unparseable answer, unknown
/// installation — reporting the category only (api-design §6; spec FR-007, VR-003, I-6). The network is a
/// scripted handler; the timeout runs on the manual clock.
/// </summary>
public sealed class ControlPlaneClientTests
{
    private static readonly Guid InstallationId = Guid.Parse("3f1c2a8e-5b7d-4c9e-a1f0-2d6b8e4c7a15");

    [Fact]
    public async Task PostsContractRequest_ToTheCheckPath_AsJson()
    {
        var ct = TestContext.Current.CancellationToken;
        var handler = new ScriptedHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, ValidAnswer())));
        var client = Client(handler, out _);

        await client.CheckAsync(InstallationId, "1.4.2", 1, ct);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri("https://control-plane.test/service/v1/legitimacy-checks"), request.Uri);
        Assert.StartsWith("application/json", request.ContentType, StringComparison.OrdinalIgnoreCase);
        using var body = JsonDocument.Parse(request.Body);
        var properties = body.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
        Assert.Equal(new[] { "applicationVersion", "contractVersion", "installationId" }, properties.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(InstallationId, Guid.Parse(properties["installationId"].GetString()!));
        Assert.Equal("1.4.2", properties["applicationVersion"].GetString());
        Assert.Equal(1, properties["contractVersion"].GetInt32());
        Assert.False(request.Headers.ContainsKey("Cookie"));
        Assert.False(request.Headers.ContainsKey("Authorization"));
    }

    [Theory]
    [InlineData("active", "supported", InstallationStatus.Active, CompatibilityState.Supported)]
    [InlineData("suspended", "upgrade_recommended", InstallationStatus.Suspended, CompatibilityState.UpgradeRecommended)]
    [InlineData("active", "upgrade_required", InstallationStatus.Active, CompatibilityState.UpgradeRequired)]
    public async Task ValidAnswer_IsReturnedAsAnswer(string status, string compatibility, InstallationStatus expectedStatus, CompatibilityState expectedCompatibility)
    {
        var ct = TestContext.Current.CancellationToken;
        var body = ValidAnswer(status, compatibility);
        var client = Client(new ScriptedHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, body))), out _);

        var reply = await client.CheckAsync(InstallationId, "1.0.0", 1, ct);

        Assert.Equal(
            new ControlPlaneCheckReply.Answer(expectedStatus, expectedCompatibility, InstallationTestData.Domain, InstallationTestData.ClientId),
            reply);
    }

    [Fact]
    public async Task AnswerWithUnknownExtraProperties_IsStillAnAnswer()
    {
        var ct = TestContext.Current.CancellationToken;
        var body = """{"status":"active","compatibility":"supported","domain":"school-one.example.test","clientId":"100000000000000000001","future":{"x":1}}""";
        var client = Client(new ScriptedHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, body))), out _);

        var reply = await client.CheckAsync(InstallationId, "1.0.0", 1, ct);

        Assert.IsType<ControlPlaneCheckReply.Answer>(reply);
    }

    public static TheoryData<string, string> UnparseableBodies => new()
    {
        { "empty", string.Empty },
        { "not JSON", "<html>ok</html>" },
        { "truncated", """{"status":"active","compatibility":"supported","domain":"school-one.example.test","clientId":"1000""" },
        { "array", """[{"status":"active"}]""" },
        { "missing status", """{"compatibility":"supported","domain":"school-one.example.test","clientId":"100000000000000000001"}""" },
        { "missing compatibility", """{"status":"active","domain":"school-one.example.test","clientId":"100000000000000000001"}""" },
        { "missing domain", """{"status":"active","compatibility":"supported","clientId":"100000000000000000001"}""" },
        { "missing clientId", """{"status":"active","compatibility":"supported","domain":"school-one.example.test"}""" },
        { "unknown status", """{"status":"paused","compatibility":"supported","domain":"school-one.example.test","clientId":"100000000000000000001"}""" },
        { "status in capitals", """{"status":"ACTIVE","compatibility":"supported","domain":"school-one.example.test","clientId":"100000000000000000001"}""" },
        { "unknown compatibility", """{"status":"active","compatibility":"fine","domain":"school-one.example.test","clientId":"100000000000000000001"}""" },
        { "domain too short", """{"status":"active","compatibility":"supported","domain":"ab","clientId":"100000000000000000001"}""" },
        { "domain too long", "{\"status\":\"active\",\"compatibility\":\"supported\",\"domain\":\"" + new string('a', 254) + "\",\"clientId\":\"100000000000000000001\"}" },
        { "client id with a letter", """{"status":"active","compatibility":"supported","domain":"school-one.example.test","clientId":"10000000000000000000a"}""" },
        { "client id nine digits", """{"status":"active","compatibility":"supported","domain":"school-one.example.test","clientId":"123456789"}""" },
        { "client id 33 digits", """{"status":"active","compatibility":"supported","domain":"school-one.example.test","clientId":"123456789012345678901234567890123"}""" },
        { "status a number", """{"status":1,"compatibility":"supported","domain":"school-one.example.test","clientId":"100000000000000000001"}""" },
        { "null values", """{"status":null,"compatibility":null,"domain":null,"clientId":null}""" },
    };

    [Theory]
    [MemberData(nameof(UnparseableBodies))]
    public async Task Status200_WithInvalidBody_IsUnparseableAnswer(string scenario, string body)
    {
        var ct = TestContext.Current.CancellationToken;
        var client = Client(new ScriptedHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, body))), out _);

        var reply = await client.CheckAsync(InstallationId, "1.0.0", 1, ct);

        Assert.True(
            reply == new ControlPlaneCheckReply.Failure(CheckFailureCategory.UnparseableAnswer),
            $"{scenario}: got {reply}.");
    }

    [Fact]
    public async Task Status404_WithUnknownInstallationOutcome_IsUnknownInstallation()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = Client(new ScriptedHandler((_, _) => Task.FromResult(Json(HttpStatusCode.NotFound, """{"outcome":"unknown_installation"}"""))), out _);

        var reply = await client.CheckAsync(InstallationId, "1.0.0", 1, ct);

        Assert.Equal(new ControlPlaneCheckReply.Failure(CheckFailureCategory.UnknownInstallation), reply);
    }

    public static TheoryData<string, int, string, string> ErrorAnswers => new()
    {
        { "404 without body (wrong address)", 404, string.Empty, "text/plain" },
        { "404 HTML page (proxy)", 404, "<html>Not found</html>", "text/html" },
        { "404 other outcome", 404, """{"outcome":"invalid_request"}""", "application/json" },
        { "400 invalid request", 400, """{"outcome":"invalid_request"}""", "application/json" },
        { "302 to setup", 302, string.Empty, "text/plain" },
        { "301 moved", 301, string.Empty, "text/plain" },
        { "401", 401, string.Empty, "text/plain" },
        { "403", 403, string.Empty, "text/plain" },
        { "405", 405, string.Empty, "text/plain" },
        { "500", 500, string.Empty, "text/plain" },
        { "502 gateway", 502, "<html>Bad gateway</html>", "text/html" },
        { "503", 503, string.Empty, "text/plain" },
        { "201 with an answer", 201, """{"status":"active","compatibility":"supported","domain":"school-one.example.test","clientId":"100000000000000000001"}""", "application/json" },
        { "204 no content", 204, string.Empty, "text/plain" },
    };

    [Theory]
    [MemberData(nameof(ErrorAnswers))]
    public async Task OtherStatus_IsErrorAnswer(string scenario, int status, string body, string contentType)
    {
        var ct = TestContext.Current.CancellationToken;
        var handler = new ScriptedHandler((_, _) =>
        {
            var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body, Encoding.UTF8, contentType) };
            if (status is 301 or 302)
            {
                response.Headers.Location = new Uri("/setup", UriKind.Relative);
            }

            return Task.FromResult(response);
        });
        var client = Client(handler, out _);

        var reply = await client.CheckAsync(InstallationId, "1.0.0", 1, ct);

        Assert.True(reply == new ControlPlaneCheckReply.Failure(CheckFailureCategory.ErrorAnswer), $"{scenario}: got {reply}.");
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ConnectionFailure_IsUnreachable()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = Client(new ScriptedHandler((_, _) => throw new HttpRequestException("zz connection refused", null, null)), out _);

        var reply = await client.CheckAsync(InstallationId, "1.0.0", 1, ct);

        Assert.Equal(new ControlPlaneCheckReply.Failure(CheckFailureCategory.Unreachable), reply);
    }

    [Fact]
    public async Task TlsFailure_IsUnreachable()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = Client(
            new ScriptedHandler((_, _) => throw new HttpRequestException("ssl", new AuthenticationException("untrusted certificate"))),
            out _);

        var reply = await client.CheckAsync(InstallationId, "1.0.0", 1, ct);

        Assert.Equal(new ControlPlaneCheckReply.Failure(CheckFailureCategory.Unreachable), reply);
    }

    [Fact]
    public async Task NoAnswerWithinThirtySeconds_IsTimeout()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = Client(new ScriptedHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }), out var time);
        var start = time.GetUtcNow();

        var call = client.CheckAsync(InstallationId, "1.0.0", 1, ct);
        await time.WaitForTimerAtAsync(start + TimeSpan.FromSeconds(30), ct);
        time.Advance(TimeSpan.FromSeconds(29));
        Assert.False(call.IsCompleted);
        time.Advance(TimeSpan.FromSeconds(1));

        var reply = await call.WaitAsync(ManualTimeProvider.RealTimeLimit, ct);
        Assert.Equal(new ControlPlaneCheckReply.Failure(CheckFailureCategory.Timeout), reply);
    }

    [Fact]
    public async Task AnswerJustBeforeThirtySeconds_IsNotATimeout()
    {
        var ct = TestContext.Current.CancellationToken;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = Client(new ScriptedHandler(async (_, token) =>
        {
            await release.Task.WaitAsync(token);
            return Json(HttpStatusCode.OK, ValidAnswer());
        }), out var time);
        var start = time.GetUtcNow();

        var call = client.CheckAsync(InstallationId, "1.0.0", 1, ct);
        await time.WaitForTimerAtAsync(start + TimeSpan.FromSeconds(30), ct);
        time.Advance(TimeSpan.FromSeconds(29));
        release.SetResult();

        var reply = await call.WaitAsync(ManualTimeProvider.RealTimeLimit, ct);
        Assert.IsType<ControlPlaneCheckReply.Answer>(reply);
    }

    [Fact]
    public async Task CallerCancellation_IsNotSwallowedAsAFailure()
    {
        var cancelled = new CancellationTokenSource();
        var client = Client(new ScriptedHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }), out _);

        var call = client.CheckAsync(InstallationId, "1.0.0", 1, cancelled.Token);
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call.WaitAsync(ManualTimeProvider.RealTimeLimit, TestContext.Current.CancellationToken));
    }

    private static ControlPlaneClient Client(ScriptedHandler handler, out ManualTimeProvider time)
    {
        time = new ManualTimeProvider(InstallationTestHost.DefaultStart);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://control-plane.test/") };
        return new ControlPlaneClient(http, time);
    }

    private static string ValidAnswer(string status = "active", string compatibility = "supported") =>
        JsonSerializer.Serialize(new
        {
            status,
            compatibility,
            domain = InstallationTestData.Domain,
            clientId = InstallationTestData.ClientId,
        });

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class ScriptedHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        private readonly List<RecordedRequest> _requests = [];

        public IReadOnlyList<RecordedRequest> Requests => _requests;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            _requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri,
                request.Content?.Headers.ContentType?.ToString() ?? string.Empty,
                body,
                request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase)));
            return await respond(request, cancellationToken);
        }
    }

    private sealed record RecordedRequest(
        HttpMethod Method,
        Uri? Uri,
        string ContentType,
        string Body,
        IReadOnlyDictionary<string, string> Headers);
}
