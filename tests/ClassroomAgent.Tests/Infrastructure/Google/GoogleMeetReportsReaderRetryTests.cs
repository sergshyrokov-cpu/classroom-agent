using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Infrastructure.Google;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Infrastructure.Google;

/// <summary>
/// US-031 AC-008, spec FR-004, FR-010; US-017 spec FR-001…FR-004: the Meet reader goes through the same retry policy and
/// failure classification as the Classroom reader — four attempts, pauses 2, 8 and 30 s, Retry-After honoured, a retried
/// request repeating its own page token — proven over a scripted HTTP handler with a manual <see cref="TimeProvider"/>
/// and a fixed jitter (TC-4). No test sleeps: a pause is observed as a pending timer of the manual clock and is crossed
/// by advancing it.
/// </summary>
public sealed class GoogleMeetReportsReaderRetryTests
{
    private const string Reference = "installation-google-key";
    private const string TechnicalAccount = AccessCheckTestData.TechnicalAccount;
    private const string TokenHost = "oauth2.googleapis.com";

    private static readonly DateTimeOffset Start = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset From = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 9, 17, 8, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Tolerance = TimeSpan.FromMilliseconds(1);

    /// <summary>AC-008, FR-001, FR-002: a 503 is repeated once after the 2 s pause and the second answer's events are returned.</summary>
    [Fact]
    public async Task A503_IsRetriedAfterAPause_AndTheSecondAnswerIsReturned()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(1.0);
        world.Api(_ => ScriptedHttpHandler.EmptyResponse(HttpStatusCode.ServiceUnavailable));
        world.Api(_ => ActivityPage(null, 1));

        var task = ReadConferencesAsync(world.Reader, ct);
        await StepThroughPauseAsync(task, world, TimeSpan.FromSeconds(2), 2, ct);
        var conferences = await task.WaitAsync(ManualTimeProvider.RealTimeLimit, ct);

        Assert.Equal([MeetTestData.ConferenceId(1)], conferences);
        Assert.Equal(2, world.ApiRequests.Count);
    }

    /// <summary>US-017 FR-002: a retry repeats the page that failed with its own page token.</summary>
    [Fact]
    public async Task ARetriedPage_CarriesItsPageToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(1.0);
        world.Api(_ => ActivityPage("p2", 1));
        world.Api(_ => ScriptedHttpHandler.EmptyResponse(HttpStatusCode.TooManyRequests));
        world.Api(_ => ActivityPage(null, 2));

        var task = ReadConferencesAsync(world.Reader, ct);
        await StepThroughPauseAsync(task, world, TimeSpan.FromSeconds(2), 3, ct);
        var conferences = await task.WaitAsync(ManualTimeProvider.RealTimeLimit, ct);

        Assert.Equal([MeetTestData.ConferenceId(1), MeetTestData.ConferenceId(2)], conferences);
        var requests = world.ApiRequests;
        Assert.Equal(3, requests.Count);
        Assert.Null(HttpUtility.ParseQueryString(requests[0].Uri!.Query)["pageToken"]);
        Assert.Equal("p2", HttpUtility.ParseQueryString(requests[1].Uri!.Query)["pageToken"]);
        Assert.Equal("p2", HttpUtility.ParseQueryString(requests[2].Uri!.Query)["pageToken"]);
    }

    /// <summary>US-017 FR-002: Retry-After in seconds replaces the nominal pause.</summary>
    [Fact]
    public async Task RetryAfter_ReplacesTheNominalPause()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(1.0);
        world.Api(_ => WithRetryAfter(ScriptedHttpHandler.EmptyResponse(HttpStatusCode.TooManyRequests), "5"));
        world.Api(_ => ActivityPage(null, 1));

        var task = ReadConferencesAsync(world.Reader, ct);
        await StepThroughPauseAsync(task, world, TimeSpan.FromSeconds(5), 2, ct);
        var conferences = await task.WaitAsync(ManualTimeProvider.RealTimeLimit, ct);

        Assert.Equal([MeetTestData.ConferenceId(1)], conferences);
    }

    /// <summary>
    /// US-017 FR-001: a 403 for a disabled API is the Owner's to fix — a configuration failure with the diagnosis the
    /// Classroom reader gives it, after one attempt and no pause.
    /// </summary>
    [Fact]
    public async Task AConfigurationAnswer_IsFinalAtOnce_WithoutARetry()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(1.0);
        world.Api(_ => ApiError(HttpStatusCode.Forbidden, "Admin SDK API has not been used in project 1.", "accessNotConfigured", "PERMISSION_DENIED"));

        var failure = await Assert.ThrowsAsync<GoogleReadFailedException>(
            () => ReadConferencesAsync(world.Reader, ct).WaitAsync(ManualTimeProvider.RealTimeLimit, ct));

        Assert.Equal(GoogleReadFailureKind.Configuration, failure.Kind);
        Assert.Equal(SyncDiagnosis.ApiNotEnabled, failure.Diagnosis);
        Assert.Single(world.ApiRequests);
        Assert.Empty(world.Time.PendingDueTimes);
    }

    /// <summary>AC-008, US-017 FR-002: four attempts in all, then a transient failure with the diagnosis GoogleUnavailable.</summary>
    [Fact]
    public async Task FourFailingAttempts_EndInATransientFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(1.0);
        world.Api(_ => ScriptedHttpHandler.EmptyResponse(HttpStatusCode.ServiceUnavailable));

        var task = ReadConferencesAsync(world.Reader, ct);
        await StepThroughPauseAsync(task, world, TimeSpan.FromSeconds(2), 2, ct);
        await StepThroughPauseAsync(task, world, TimeSpan.FromSeconds(8), 3, ct);
        await StepThroughPauseAsync(task, world, TimeSpan.FromSeconds(30), 4, ct);
        var failure = await Assert.ThrowsAsync<GoogleReadFailedException>(
            () => task.WaitAsync(ManualTimeProvider.RealTimeLimit, ct));

        Assert.Equal(GoogleReadFailureKind.Transient, failure.Kind);
        Assert.Equal(SyncDiagnosis.GoogleUnavailable, failure.Diagnosis);
        Assert.Equal(4, world.ApiRequests.Count);
        Assert.Empty(world.Time.PendingDueTimes);
    }

    /// <summary>US-017 FR-002: the pauses between the attempts are 2, 8 and 30 s with the jitter factor 1.0.</summary>
    [Fact]
    public async Task ThePauses_AreTwo_Eight_AndThirtySeconds()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(1.0);
        world.Api(_ => ScriptedHttpHandler.EmptyResponse(HttpStatusCode.ServiceUnavailable));

        var task = ReadConferencesAsync(world.Reader, ct);
        var first = await WaitForPauseAsync(task, world, 2, ct);
        world.Time.Advance(first);
        var second = await WaitForPauseAsync(task, world, 3, ct);
        world.Time.Advance(second);
        var third = await WaitForPauseAsync(task, world, 4, ct);
        world.Time.Advance(third);
        await Assert.ThrowsAsync<GoogleReadFailedException>(() => task.WaitAsync(ManualTimeProvider.RealTimeLimit, ct));

        Assert.InRange(first, TimeSpan.FromSeconds(2) - Tolerance, TimeSpan.FromSeconds(2) + Tolerance);
        Assert.InRange(second, TimeSpan.FromSeconds(8) - Tolerance, TimeSpan.FromSeconds(8) + Tolerance);
        Assert.InRange(third, TimeSpan.FromSeconds(30) - Tolerance, TimeSpan.FromSeconds(30) + Tolerance);
    }

    // ---- helpers -----------------------------------------------------------------------------------------------

    /// <summary>Waits until <paramref name="totalRequests"/> requests were sent and the adapter sits in a pause.</summary>
    private static async Task<TimeSpan> WaitForPauseAsync(Task run, World world, int totalRequests, CancellationToken ct)
    {
        var pause = world.Time.WaitUntilAsync(
            () => world.Transport.Requests.Count == totalRequests && world.Time.PendingDueTimes.Count > 0,
            $"a retry pause after request {totalRequests}",
            ct);
        if (await Task.WhenAny(pause, run) == run)
        {
            // The read ended before the pause: surface its failure instead of waiting for a pause that never comes.
            await run;
            Assert.Fail($"The read ended before a retry pause after request {totalRequests}.");
        }

        await pause;
        return world.Time.PendingDueTimes[0] - world.Time.GetUtcNow();
    }

    /// <summary>
    /// Asserts the pause that follows request number <paramref name="totalRequests"/> is <paramref name="expected"/>
    /// long, proves nothing is sent 100 ms before it ends, then lets it end.
    /// </summary>
    private static async Task StepThroughPauseAsync(Task run, World world, TimeSpan expected, int totalRequests, CancellationToken ct)
    {
        var pause = await WaitForPauseAsync(run, world, totalRequests, ct);
        Assert.InRange(pause, expected - Tolerance, expected + Tolerance);

        var early = pause - TimeSpan.FromMilliseconds(100);
        world.Time.Advance(early);
        Assert.Equal(totalRequests, world.Transport.Requests.Count);
        Assert.NotEmpty(world.Time.PendingDueTimes);

        world.Time.Advance(pause - early);
    }

    private static async Task<List<string?>> ReadConferencesAsync(GoogleMeetReportsReader reader, CancellationToken ct)
    {
        var ids = new List<string?>();
        await foreach (var page in reader.ReadCallEndedAsync(TechnicalAccount, From, To, ct))
        {
            ids.AddRange(page.Events.Select(e => e.ConferenceId));
        }

        return ids;
    }

    private static HttpResponseMessage WithRetryAfter(HttpResponseMessage response, string seconds)
    {
        Assert.True(response.Headers.TryAddWithoutValidation("Retry-After", seconds));
        return response;
    }

    private static HttpResponseMessage TokenIssued() =>
        ScriptedHttpHandler.JsonResponse(
            HttpStatusCode.OK,
            """{"access_token":"ya29.synthetic-access-token","expires_in":3599,"token_type":"Bearer"}""");

    private static HttpResponseMessage ApiError(HttpStatusCode status, string message, string reason, string grpcStatus) =>
        ScriptedHttpHandler.JsonResponse(
            status,
            JsonSerializer.Serialize(new
            {
                error = new
                {
                    code = (int)status,
                    message,
                    status = grpcStatus,
                    errors = new[] { new { message, domain = "global", reason } },
                    details = new[]
                    {
                        new Dictionary<string, string>
                        {
                            ["@type"] = "type.googleapis.com/google.rpc.ErrorInfo",
                            ["reason"] = reason,
                            ["domain"] = "googleapis.com",
                        },
                    },
                },
            }));

    /// <summary>One <c>activities.list</c> answer with a single <c>call_ended</c> activity of the numbered conference.</summary>
    private static HttpResponseMessage ActivityPage(string? nextPageToken, int conference) =>
        ScriptedHttpHandler.JsonResponse(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["kind"] = "admin#reports#activities",
                ["items"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["kind"] = "admin#reports#activity",
                        ["id"] = new Dictionary<string, object?>
                        {
                            ["time"] = "2026-09-15T10:00:00.000Z",
                            ["uniqueQualifier"] = conference.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ["applicationName"] = "meet",
                            ["customerId"] = "C0synthetic",
                        },
                        ["events"] = new[]
                        {
                            new Dictionary<string, object?>
                            {
                                ["type"] = "call",
                                ["name"] = "call_ended",
                                ["parameters"] = new object[]
                                {
                                    new Dictionary<string, object?> { ["name"] = "conference_id", ["value"] = MeetTestData.ConferenceId(conference) },
                                    new Dictionary<string, object?> { ["name"] = "meeting_code", ["value"] = MeetTestData.MeetingCode(conference) },
                                    new Dictionary<string, object?> { ["name"] = "organizer_email", ["value"] = MeetTestData.Teacher(1) },
                                    new Dictionary<string, object?> { ["name"] = "endpoint_id", ["value"] = MeetTestData.EndpointId(conference) },
                                    new Dictionary<string, object?> { ["name"] = "identifier", ["value"] = MeetTestData.Student(1) },
                                    new Dictionary<string, object?> { ["name"] = "identifier_type", ["value"] = MeetTestData.EmailIdentifierType },
                                    new Dictionary<string, object?> { ["name"] = "duration_seconds", ["intValue"] = "3600" },
                                },
                            },
                        },
                    },
                },
                ["nextPageToken"] = nextPageToken,
            }));

    /// <summary>
    /// One reader over a scripted transport: token requests and API requests draw from separate queues (a missing token
    /// answer is a success), the last API answer repeats once its queue is empty, and every request is recorded.
    /// </summary>
    private sealed class World
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _api = new();
        private Func<HttpRequestMessage, HttpResponseMessage>? _lastApi;

        public World(double jitterFactor)
        {
            Time = new ManualTimeProvider(Start);
            Transport = new ScriptedHttpHandler((request, _) => Task.FromResult(Answer(request)));
            Reader = new GoogleMeetReportsReader(
                new DictionarySecretStore(new Dictionary<string, string> { [Reference] = SyntheticServiceAccountKey.Create() }),
                new GoogleServiceAccountSettings(Reference),
                Transport,
                Time,
                new FixedRetryJitter(jitterFactor),
                new CapturingLogger<GoogleMeetReportsReader>());
        }

        public ManualTimeProvider Time { get; }

        public ScriptedHttpHandler Transport { get; }

        public GoogleMeetReportsReader Reader { get; }

        public IReadOnlyList<ScriptedHttpHandler.RecordedRequest> ApiRequests =>
            Transport.Requests.Where(r => r.Uri?.Host != TokenHost).ToList();

        public void Api(Func<HttpRequestMessage, HttpResponseMessage> answer) => _api.Enqueue(answer);

        private HttpResponseMessage Answer(HttpRequestMessage request)
        {
            if (request.RequestUri?.Host == TokenHost)
            {
                return TokenIssued();
            }

            if (_api.Count > 0)
            {
                _lastApi = _api.Dequeue();
            }

            Assert.NotNull(_lastApi);
            return _lastApi(request);
        }
    }
}
