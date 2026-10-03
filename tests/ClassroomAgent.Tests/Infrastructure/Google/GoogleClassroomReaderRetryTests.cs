using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Infrastructure.Google;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging;

namespace ClassroomAgent.Tests.Infrastructure.Google;

/// <summary>
/// US-017 spec FR-001…FR-004, VR-001, FR-010, AC-001…AC-005, AC-011, AC-012, AC-014: the retry policy and the
/// classification of Google failures, proven through the real adapter over a scripted HTTP handler with a manual
/// <see cref="TimeProvider"/> and a fixed jitter (TC-4, OD-010). No test sleeps: a pause is observed as a pending timer
/// of the manual clock and is crossed by advancing it.
/// </summary>
public sealed class GoogleClassroomReaderRetryTests
{
    private const string Reference = "installation-google-key";
    private const string TechnicalAccount = AccessCheckTestData.TechnicalAccount;
    private const string TokenHost = "oauth2.googleapis.com";
    private const string LeakMarker = "SYNTHETIC-GOOGLE-DETAIL-7f3a";
    private const string RetryEventName = "SyncGoogleRetry";

    private static readonly DateTimeOffset Start = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Tolerance = TimeSpan.FromMilliseconds(1);

    // ---- scenario 1: transient HTTP statuses -------------------------------------------------------------------

    public static TheoryData<HttpStatusCode> TransientStatuses => new(
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.ServiceUnavailable);

    /// <summary>
    /// FR-001, FR-002, AC-001: a 429 or 5xx is repeated once, not sooner than the nominal 2 s pause (factor 1.0), and the
    /// courses of the second answer are returned.
    /// </summary>
    [Theory]
    [MemberData(nameof(TransientStatuses))]
    public async Task ATransientStatus_IsRetriedAfterTwoSeconds_AndTheSecondAnswerIsReturned(HttpStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(1.0);
        world.Api(_ => ScriptedHttpHandler.EmptyResponse(status));
        world.Api(_ => CoursePage(null, (CourseTestData.CourseId(1), CourseTestData.CourseName(1), "ACTIVE")));

        var task = ReadCoursesAsync(world.Reader, ct);
        await StepThroughPauseAsync(world, TimeSpan.FromSeconds(2), 2, ct);
        var courses = await task.WaitAsync(ManualTimeProvider.RealTimeLimit, ct);

        Assert.Equal([CourseTestData.CourseId(1)], courses);
        Assert.Equal(2, world.ApiRequests.Count);
    }

    // ---- scenario 2: network failures --------------------------------------------------------------------------

    /// <summary>FR-001: a dropped or refused connection is transient and retried.</summary>
    [Fact]
    public async Task ADroppedConnection_IsRetried()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(1.0);
        world.Api(_ => throw new HttpRequestException("synthetic connection failure"));
        world.Api(_ => CoursePage(null, (CourseTestData.CourseId(1), CourseTestData.CourseName(1), "ACTIVE")));

        var task = ReadCoursesAsync(world.Reader, ct);
        await StepThroughPauseAsync(world, TimeSpan.FromSeconds(2), 2, ct);
        var courses = await task.WaitAsync(ManualTimeProvider.RealTimeLimit, ct);

        Assert.Equal([CourseTestData.CourseId(1)], courses);
        Assert.Equal(2, world.ApiRequests.Count);
    }

    /// <summary>FR-001: a timeout that is not the caller's cancellation is transient and retried.</summary>
    [Fact]
    public async Task ATimeout_ThatIsNotTheCallersCancellation_IsRetried()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(1.0);
        world.Api(_ => throw new TaskCanceledException("synthetic timeout", new TimeoutException()));
        world.Api(_ => CoursePage(null, (CourseTestData.CourseId(1), CourseTestData.CourseName(1), "ACTIVE")));

        var task = ReadCoursesAsync(world.Reader, ct);
        await StepThroughPauseAsync(world, TimeSpan.FromSeconds(2), 2, ct);
        var courses = await task.WaitAsync(ManualTimeProvider.RealTimeLimit, ct);

        Assert.Equal([CourseTestData.CourseId(1)], courses);
        Assert.Equal(2, world.ApiRequests.Count);
    }

    // ---- scenario 3: exhausted ---------------------------------------------------------------------------------

    public static TheoryData<double> Factors => new(1.0, 0.8);

    /// <summary>
    /// FR-002, AC-002: four attempts in all, the pauses 2 s, 8 s and 30 s each times the jitter factor, then a final
    /// transient failure with the diagnosis GoogleUnavailable and one retry warning per retried attempt (FR-010).
    /// </summary>
    [Theory]
    [MemberData(nameof(Factors))]
    public async Task EveryAttemptFailing_StopsAtFourAttempts_WithThePausesTimesTheFactor(double factor)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(factor);
        world.Api(_ => ScriptedHttpHandler.EmptyResponse(HttpStatusCode.ServiceUnavailable));

        var task = ReadCoursesAsync(world.Reader, ct);
        await StepThroughPauseAsync(world, TimeSpan.FromSeconds(2 * factor), 2, ct);
        await StepThroughPauseAsync(world, TimeSpan.FromSeconds(8 * factor), 3, ct);
        await StepThroughPauseAsync(world, TimeSpan.FromSeconds(30 * factor), 4, ct);
        var failure = await Assert.ThrowsAsync<GoogleReadFailedException>(
            () => task.WaitAsync(ManualTimeProvider.RealTimeLimit, ct));

        Assert.Equal(GoogleReadFailureKind.Transient, failure.Kind);
        Assert.Equal(SyncDiagnosis.GoogleUnavailable, failure.Diagnosis);
        Assert.Equal(4, world.ApiRequests.Count);
        Assert.Empty(world.Time.PendingDueTimes);
        Assert.Equal(3, world.RetryWarnings.Count);
    }

    // ---- scenario 4: Retry-After -------------------------------------------------------------------------------

    public static TheoryData<string, double> RetryAfterCases => new()
    {
        { "5", 5 },
        { "600", 120 },
        { "date+10s", 10 },
        { "-1", 2 },
        { "abc", 2 },
        { "date-1h", 2 },
    };

    /// <summary>
    /// FR-002, VR-001: Retry-After in seconds or as an HTTP date replaces the nominal pause, capped at 2 minutes; a
    /// negative, unparseable or past value means the nominal pause.
    /// </summary>
    [Theory]
    [MemberData(nameof(RetryAfterCases))]
    public async Task RetryAfter_ReplacesTheNominalPause_WhenUsable(string header, double expectedSeconds)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(1.0);
        world.Api(_ => WithRetryAfter(ScriptedHttpHandler.EmptyResponse(HttpStatusCode.TooManyRequests), header));
        world.Api(_ => CoursePage(null, (CourseTestData.CourseId(1), CourseTestData.CourseName(1), "ACTIVE")));

        var task = ReadCoursesAsync(world.Reader, ct);
        await StepThroughPauseAsync(world, TimeSpan.FromSeconds(expectedSeconds), 2, ct);
        var courses = await task.WaitAsync(ManualTimeProvider.RealTimeLimit, ct);

        Assert.Equal([CourseTestData.CourseId(1)], courses);
    }

    // ---- scenario 5: the token endpoint ------------------------------------------------------------------------

    /// <summary>FR-002: a token request is a request like any other — a 503 on it is retried and then succeeds.</summary>
    [Fact]
    public async Task ATokenRequest_Answering503_IsRetried()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(1.0);
        world.Token(_ => ScriptedHttpHandler.EmptyResponse(HttpStatusCode.ServiceUnavailable));
        world.Token(_ => TokenIssued());
        world.Api(_ => CoursePage(null, (CourseTestData.CourseId(1), CourseTestData.CourseName(1), "ACTIVE")));

        var task = ReadCoursesAsync(world.Reader, ct);
        await StepThroughPauseAsync(world, TimeSpan.FromSeconds(2), 1, ct);
        var courses = await task.WaitAsync(ManualTimeProvider.RealTimeLimit, ct);

        Assert.Equal([CourseTestData.CourseId(1)], courses);
        Assert.Equal(2, world.TokenRequests.Count);
        Assert.Single(world.ApiRequests);
    }

    public static TheoryData<HttpStatusCode, string, string, SyncDiagnosis> TokenRefusals => new()
    {
        { HttpStatusCode.Unauthorized, "unauthorized_client", "Client is unauthorized to retrieve access tokens using this method", SyncDiagnosis.ScopeNotAuthorized },
        { HttpStatusCode.BadRequest, "access_denied", "Requested client not authorized.", SyncDiagnosis.ScopeNotAuthorized },
        { HttpStatusCode.BadRequest, "invalid_grant", "Invalid JWT Signature.", SyncDiagnosis.KeyRejected },
        { HttpStatusCode.BadRequest, "invalid_grant", "Invalid email or User ID", SyncDiagnosis.TechnicalAccountUnknown },
        { HttpStatusCode.Unauthorized, "invalid_client", "The OAuth client was not found.", SyncDiagnosis.KeyRejected },
    };

    /// <summary>
    /// FR-001, FR-006, AC-003: a configuration refusal at the token endpoint is final at once — one token request, no
    /// API request, no pause — and carries the diagnosis the access check would give it.
    /// </summary>
    [Theory]
    [MemberData(nameof(TokenRefusals))]
    public async Task AConfigurationRefusal_AtTheTokenEndpoint_IsFinalAtOnce(
        HttpStatusCode status,
        string error,
        string description,
        SyncDiagnosis expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(1.0);
        world.Token(_ => TokenError(status, error, description));

        var failure = await Assert.ThrowsAsync<GoogleReadFailedException>(
            () => ReadCoursesAsync(world.Reader, ct).WaitAsync(ManualTimeProvider.RealTimeLimit, ct));

        Assert.Equal(GoogleReadFailureKind.Configuration, failure.Kind);
        Assert.Equal(expected, failure.Diagnosis);
        Assert.Single(world.TokenRequests);
        Assert.Empty(world.ApiRequests);
        Assert.Empty(world.Time.PendingDueTimes);
        Assert.Empty(world.RetryWarnings);
    }

    // ---- scenario 6: API 403 -----------------------------------------------------------------------------------

    /// <summary>FR-001: a 403 for a disabled API is the Owner's to fix; one attempt only.</summary>
    [Theory]
    [InlineData("accessNotConfigured")]
    [InlineData("SERVICE_DISABLED")]
    public async Task A403_ForADisabledApi_IsApiNotEnabled_AfterOneAttempt(string reason)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(1.0);
        world.Api(_ => ApiError(HttpStatusCode.Forbidden, "Classroom API has not been used in project 1.", reason, "PERMISSION_DENIED"));

        var failure = await Assert.ThrowsAsync<GoogleReadFailedException>(
            () => ReadCoursesAsync(world.Reader, ct).WaitAsync(ManualTimeProvider.RealTimeLimit, ct));

        Assert.Equal(GoogleReadFailureKind.Configuration, failure.Kind);
        Assert.Equal(SyncDiagnosis.ApiNotEnabled, failure.Diagnosis);
        Assert.Single(world.ApiRequests);
    }

    /// <summary>FR-001: any other 403 is the technical account's rights; one attempt only.</summary>
    [Fact]
    public async Task AnotherA403_IsTechnicalAccountCannotRead_AfterOneAttempt()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(1.0);
        world.Api(_ => ApiError(HttpStatusCode.Forbidden, "Not Authorized to access this resource/api", "forbidden", "PERMISSION_DENIED"));

        var failure = await Assert.ThrowsAsync<GoogleReadFailedException>(
            () => ReadCoursesAsync(world.Reader, ct).WaitAsync(ManualTimeProvider.RealTimeLimit, ct));

        Assert.Equal(GoogleReadFailureKind.Configuration, failure.Kind);
        Assert.Equal(SyncDiagnosis.TechnicalAccountCannotRead, failure.Diagnosis);
        Assert.Single(world.ApiRequests);
    }

    // ---- scenario 7: missing key -------------------------------------------------------------------------------

    /// <summary>FR-001: no key under the configured reference is KeyUnavailable and nothing is sent.</summary>
    [Fact]
    public async Task AMissingKey_IsKeyUnavailable_AndNothingIsSent()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(1.0, keyPresent: false);

        var failure = await Assert.ThrowsAsync<GoogleReadFailedException>(
            () => ReadCoursesAsync(world.Reader, ct).WaitAsync(ManualTimeProvider.RealTimeLimit, ct));

        Assert.Equal(GoogleReadFailureKind.Configuration, failure.Kind);
        Assert.Equal(SyncDiagnosis.KeyUnavailable, failure.Diagnosis);
        Assert.Empty(world.Transport.Requests);
    }

    // ---- scenario 8: course gone and unexpected ----------------------------------------------------------------

    /// <summary>FR-001, AC-004: a 404 on a request scoped to one course means the course is gone; one attempt, no diagnosis.</summary>
    [Theory]
    [InlineData("roster")]
    [InlineData("coursework")]
    [InlineData("submissions")]
    public async Task A404_OnAPerCourseRead_IsCourseGone_AfterOneAttempt(string operation)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(1.0);
        world.Api(_ => ApiError(HttpStatusCode.NotFound, "Requested entity was not found.", "notFound", "NOT_FOUND"));

        var failure = await Assert.ThrowsAsync<GoogleReadFailedException>(
            () => ReadAsync(world.Reader, operation, ct).WaitAsync(ManualTimeProvider.RealTimeLimit, ct));

        Assert.Equal(GoogleReadFailureKind.CourseGone, failure.Kind);
        Assert.Null(failure.Diagnosis);
        Assert.Single(world.ApiRequests);
    }

    /// <summary>FR-001: a 404 on the course listing is not a course gone — it is unexpected.</summary>
    [Fact]
    public async Task A404_OnTheCourseListing_IsUnexpected()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(1.0);
        world.Api(_ => ApiError(HttpStatusCode.NotFound, "Requested entity was not found.", "notFound", "NOT_FOUND"));

        var failure = await Assert.ThrowsAsync<GoogleReadFailedException>(
            () => ReadAsync(world.Reader, "courses", ct).WaitAsync(ManualTimeProvider.RealTimeLimit, ct));

        Assert.Equal(GoogleReadFailureKind.Unexpected, failure.Kind);
        Assert.Equal(SyncDiagnosis.Unexpected, failure.Diagnosis);
        Assert.Single(world.ApiRequests);
    }

    /// <summary>FR-001: a 400 is none of the classes and is not retried.</summary>
    [Fact]
    public async Task A400_IsUnexpected_AfterOneAttempt()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(1.0);
        world.Api(_ => ApiError(HttpStatusCode.BadRequest, "Request contains an invalid argument.", "badRequest", "INVALID_ARGUMENT"));

        var failure = await Assert.ThrowsAsync<GoogleReadFailedException>(
            () => ReadAsync(world.Reader, "courses", ct).WaitAsync(ManualTimeProvider.RealTimeLimit, ct));

        Assert.Equal(GoogleReadFailureKind.Unexpected, failure.Kind);
        Assert.Equal(SyncDiagnosis.Unexpected, failure.Diagnosis);
        Assert.Single(world.ApiRequests);
    }

    /// <summary>FR-001: an answer the adapter cannot parse is unexpected, not transient.</summary>
    [Fact]
    public async Task A200_WithAMalformedBody_IsUnexpected_AfterOneAttempt()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(1.0);
        world.Api(_ => ScriptedHttpHandler.JsonResponse(HttpStatusCode.OK, "{ this is not json"));

        var failure = await Assert.ThrowsAsync<GoogleReadFailedException>(
            () => ReadAsync(world.Reader, "courses", ct).WaitAsync(ManualTimeProvider.RealTimeLimit, ct));

        Assert.Equal(GoogleReadFailureKind.Unexpected, failure.Kind);
        Assert.Equal(SyncDiagnosis.Unexpected, failure.Diagnosis);
        Assert.Single(world.ApiRequests);
    }

    // ---- scenario 9: no Google detail leaks --------------------------------------------------------------------

    /// <summary>
    /// FR-004, SC-10: the failure carries no text Google sent. The scripted body really holds the marker (asserted), so
    /// the absence below is not vacuous.
    /// </summary>
    [Fact]
    public async Task TheFailure_CarriesNoTextGoogleSent()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(1.0);
        var scripted = ApiError(HttpStatusCode.Forbidden, LeakMarker, LeakMarker, "PERMISSION_DENIED");
        var scriptedBody = await scripted.Content.ReadAsStringAsync(ct);
        world.Api(_ => ApiError(HttpStatusCode.Forbidden, LeakMarker, LeakMarker, "PERMISSION_DENIED"));

        var failure = await Assert.ThrowsAsync<GoogleReadFailedException>(
            () => ReadAsync(world.Reader, "courses", ct).WaitAsync(ManualTimeProvider.RealTimeLimit, ct));

        Assert.Contains(LeakMarker, scriptedBody, StringComparison.Ordinal);
        Assert.Single(world.ApiRequests);
        Assert.DoesNotContain(LeakMarker, failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(LeakMarker, failure.ToString(), StringComparison.Ordinal);
        Assert.All(world.Log.Entries, e => Assert.DoesNotContain(LeakMarker, e.Message, StringComparison.Ordinal));
    }

    // ---- scenario 10: paging -----------------------------------------------------------------------------------

    /// <summary>
    /// FR-002: a retry repeats the page that failed with its own page token; pages already read are not requested again.
    /// </summary>
    [Fact]
    public async Task ARetriedPage_CarriesItsPageToken_AndEarlierPagesAreNotRepeated()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(1.0);
        world.Api(_ => CoursePage("p2", (CourseTestData.CourseId(1), CourseTestData.CourseName(1), "ACTIVE")));
        world.Api(_ => ScriptedHttpHandler.EmptyResponse(HttpStatusCode.TooManyRequests));
        world.Api(_ => CoursePage(null, (CourseTestData.CourseId(2), CourseTestData.CourseName(2), "ACTIVE")));

        var task = ReadCoursesAsync(world.Reader, ct);
        await StepThroughPauseAsync(world, TimeSpan.FromSeconds(2), 3, ct);
        var courses = await task.WaitAsync(ManualTimeProvider.RealTimeLimit, ct);

        Assert.Equal([CourseTestData.CourseId(1), CourseTestData.CourseId(2)], courses);
        var listRequests = world.ApiRequests;
        Assert.Equal(3, listRequests.Count);
        Assert.DoesNotContain("pageToken", listRequests[0].Uri!.Query, StringComparison.Ordinal);
        Assert.Contains("pageToken=p2", listRequests[1].Uri!.Query, StringComparison.Ordinal);
        Assert.Contains("pageToken=p2", listRequests[2].Uri!.Query, StringComparison.Ordinal);
    }

    // ---- scenario 11: logging ----------------------------------------------------------------------------------

    /// <summary>
    /// FR-010: a retried attempt writes exactly one Warning named SyncGoogleRetry and a first-time success writes none.
    /// The two worlds differ only in whether a retry happens, so the zero is not an unobserved logger.
    /// </summary>
    [Fact]
    public async Task OneRetriedAttempt_WritesOneWarning_AndNoRetryWritesNone()
    {
        var ct = TestContext.Current.CancellationToken;
        var retried = new World(1.0);
        retried.Api(_ => ScriptedHttpHandler.JsonResponse(HttpStatusCode.ServiceUnavailable, LeakMarker));
        retried.Api(_ => CoursePage(null, (CourseTestData.CourseId(1), CourseTestData.CourseName(1), "ACTIVE")));
        var untroubled = new World(1.0);
        untroubled.Api(_ => CoursePage(null, (CourseTestData.CourseId(1), CourseTestData.CourseName(1), "ACTIVE")));

        var task = ReadCoursesAsync(retried.Reader, ct);
        await StepThroughPauseAsync(retried, TimeSpan.FromSeconds(2), 2, ct);
        await task.WaitAsync(ManualTimeProvider.RealTimeLimit, ct);
        await ReadCoursesAsync(untroubled.Reader, ct).WaitAsync(ManualTimeProvider.RealTimeLimit, ct);

        var warning = Assert.Single(retried.RetryWarnings);
        Assert.DoesNotContain(LeakMarker, warning.Message, StringComparison.Ordinal);
        Assert.Empty(untroubled.RetryWarnings);
    }

    // ---- scenario 12: shutdown during a pause ------------------------------------------------------------------

    /// <summary>
    /// FR-003, AC-012: cancelling the caller's token while a pause is pending ends the read at once, without the clock
    /// reaching the end of the pause, and no second request is ever sent (checked after the clock then runs far on).
    /// </summary>
    [Fact]
    public async Task ShutdownDuringAPause_EndsTheReadPromptly_AndSendsNothingFurther()
    {
        var ct = TestContext.Current.CancellationToken;
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var world = new World(1.0);
        world.Api(_ => ScriptedHttpHandler.EmptyResponse(HttpStatusCode.ServiceUnavailable));
        world.Api(_ => CoursePage(null, (CourseTestData.CourseId(1), CourseTestData.CourseName(1), "ACTIVE")));

        var task = ReadCoursesAsync(world.Reader, shutdown.Token);
        var pause = await WaitForPauseAsync(world, 2, ct);
        var before = world.Time.GetUtcNow();
        shutdown.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(ManualTimeProvider.RealTimeLimit, ct));

        Assert.Equal(before, world.Time.GetUtcNow());
        Assert.True(pause > TimeSpan.Zero);
        world.Time.Advance(TimeSpan.FromHours(1));
        Assert.Single(world.ApiRequests);
    }

    // ---- scenario 13: a stalled response body ------------------------------------------------------------------

    /// <summary>
    /// FR-002: the per-attempt bound covers the body as well as the headers. The first answer is a 200 whose body never
    /// arrives; it times out after 200 ms of real time, is retried after the 2 s pause on the manual clock, and the
    /// second answer is returned. Without the buffering under the attempt token this read never ends.
    /// </summary>
    [Fact]
    public async Task AStalledResponseBody_TimesOutTheAttempt_AndIsRetried()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(1.0, attemptTimeout: TimeSpan.FromMilliseconds(200));
        world.Api(_ => StalledBody());
        world.Api(_ => CoursePage(null, (CourseTestData.CourseId(1), CourseTestData.CourseName(1), "ACTIVE")));

        var task = ReadCoursesAsync(world.Reader, ct);
        await StepThroughPauseAsync(world, TimeSpan.FromSeconds(2), 2, ct);
        var courses = await task.WaitAsync(TimeSpan.FromSeconds(10), ct);

        Assert.Equal([CourseTestData.CourseId(1)], courses);
        Assert.Equal(2, world.ApiRequests.Count);
        Assert.Single(world.RetryWarnings);
    }

    /// <summary>FR-002: when every attempt stalls, four attempts are made and the read fails as transient.</summary>
    [Fact]
    public async Task EveryResponseBodyStalling_StopsAtFourAttempts_AsATransientFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World(1.0, attemptTimeout: TimeSpan.FromMilliseconds(200));
        world.Api(_ => StalledBody());

        var task = ReadCoursesAsync(world.Reader, ct);
        await StepThroughPauseAsync(world, TimeSpan.FromSeconds(2), 2, ct);
        await StepThroughPauseAsync(world, TimeSpan.FromSeconds(8), 3, ct);
        await StepThroughPauseAsync(world, TimeSpan.FromSeconds(30), 4, ct);
        var failure = await Assert.ThrowsAsync<GoogleReadFailedException>(
            () => task.WaitAsync(TimeSpan.FromSeconds(10), ct));

        Assert.Equal(GoogleReadFailureKind.Transient, failure.Kind);
        Assert.Equal(SyncDiagnosis.GoogleUnavailable, failure.Diagnosis);
        Assert.Equal(4, world.ApiRequests.Count);
    }

    // ---- helpers -----------------------------------------------------------------------------------------------

    /// <summary>Waits until <paramref name="totalRequests"/> requests were sent and the adapter sits in a pause.</summary>
    private static async Task<TimeSpan> WaitForPauseAsync(World world, int totalRequests, CancellationToken ct)
    {
        await world.Time.WaitUntilAsync(
            () => world.Transport.Requests.Count == totalRequests && world.Time.PendingDueTimes.Count > 0,
            $"a retry pause after request {totalRequests}",
            ct);
        return world.Time.PendingDueTimes[0] - world.Time.GetUtcNow();
    }

    /// <summary>
    /// Asserts the pause that follows request number <paramref name="totalRequests"/> is <paramref name="expected"/>
    /// long, proves nothing is sent 100 ms before it ends, then lets it end.
    /// </summary>
    private static async Task StepThroughPauseAsync(World world, TimeSpan expected, int totalRequests, CancellationToken ct)
    {
        var pause = await WaitForPauseAsync(world, totalRequests, ct);
        Assert.InRange(pause, expected - Tolerance, expected + Tolerance);

        var early = pause - TimeSpan.FromMilliseconds(100);
        world.Time.Advance(early);
        Assert.Equal(totalRequests, world.Transport.Requests.Count);
        Assert.NotEmpty(world.Time.PendingDueTimes);

        world.Time.Advance(pause - early);
    }

    private static async Task<List<string>> ReadCoursesAsync(GoogleClassroomReader reader, CancellationToken ct)
    {
        var ids = new List<string>();
        await foreach (var course in reader.ReadCoursesAsync(TechnicalAccount, ct))
        {
            ids.Add(course.GoogleId);
        }

        return ids;
    }

    private static async Task ReadAsync(GoogleClassroomReader reader, string operation, CancellationToken ct)
    {
        switch (operation)
        {
            case "courses":
                await ReadCoursesAsync(reader, ct);
                break;
            case "roster":
                await reader.ReadRosterAsync(TechnicalAccount, CourseTestData.CourseId(1), ct);
                break;
            case "coursework":
                await reader.ReadCourseWorkAsync(TechnicalAccount, CourseTestData.CourseId(1), ct);
                break;
            case "submissions":
                await reader.ReadSubmissionsAsync(TechnicalAccount, CourseTestData.CourseId(1), ct);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown scripted operation.");
        }
    }

    private static HttpResponseMessage WithRetryAfter(HttpResponseMessage response, string spec)
    {
        switch (spec)
        {
            case "date+10s":
                response.Headers.RetryAfter = new RetryConditionHeaderValue(Start.AddSeconds(10));
                break;
            case "date-1h":
                response.Headers.RetryAfter = new RetryConditionHeaderValue(Start.AddHours(-1));
                break;
            default:
                Assert.True(response.Headers.TryAddWithoutValidation("Retry-After", spec));
                break;
        }

        return response;
    }

    /// <summary>A 200 whose headers arrive and whose body never does, until the read is cancelled.</summary>
    private static HttpResponseMessage StalledBody() => new(HttpStatusCode.OK) { Content = new StalledContent() };

    private static HttpResponseMessage TokenIssued() =>
        ScriptedHttpHandler.JsonResponse(
            HttpStatusCode.OK,
            """{"access_token":"ya29.synthetic-access-token","expires_in":3599,"token_type":"Bearer"}""");

    private static HttpResponseMessage TokenError(HttpStatusCode status, string error, string description) =>
        ScriptedHttpHandler.JsonResponse(
            status,
            JsonSerializer.Serialize(new Dictionary<string, string> { ["error"] = error, ["error_description"] = description }));

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

    private static HttpResponseMessage CoursePage(string? nextPageToken, params (string Id, string Name, string State)[] courses) =>
        ScriptedHttpHandler.JsonResponse(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["courses"] = courses.Select(c => new Dictionary<string, object?>
                {
                    ["id"] = c.Id,
                    ["name"] = c.Name,
                    ["courseState"] = c.State,
                }).ToArray(),
                ["nextPageToken"] = nextPageToken,
            }));

    /// <summary>
    /// One reader over a scripted transport: token requests and API requests draw from separate queues (a missing token
    /// answer is a success), the last API answer repeats once its queue is empty, and every request is recorded.
    /// </summary>
    private sealed class World
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _api = new();
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _token = new();
        private Func<HttpRequestMessage, HttpResponseMessage>? _lastApi;

        public World(double jitterFactor, bool keyPresent = true, TimeSpan? attemptTimeout = null)
        {
            Time = new ManualTimeProvider(Start);
            Log = new CapturingLogger<GoogleClassroomReader>();
            Transport = new ScriptedHttpHandler((request, _) => Task.FromResult(Answer(request)));
            var secrets = keyPresent
                ? new Dictionary<string, string> { [Reference] = SyntheticServiceAccountKey.Create() }
                : new Dictionary<string, string>();
            Reader = new GoogleClassroomReader(
                new DictionarySecretStore(secrets),
                new GoogleServiceAccountSettings(Reference),
                Transport,
                Time,
                new FixedRetryJitter(jitterFactor),
                Log,
                attemptTimeout);
        }

        public ManualTimeProvider Time { get; }

        public CapturingLogger<GoogleClassroomReader> Log { get; }

        public ScriptedHttpHandler Transport { get; }

        public GoogleClassroomReader Reader { get; }

        public IReadOnlyList<ScriptedHttpHandler.RecordedRequest> ApiRequests =>
            Transport.Requests.Where(r => r.Uri?.Host != TokenHost).ToList();

        public IReadOnlyList<ScriptedHttpHandler.RecordedRequest> TokenRequests =>
            Transport.Requests.Where(r => r.Uri?.Host == TokenHost).ToList();

        public IReadOnlyList<CapturingLogger<GoogleClassroomReader>.Entry> RetryWarnings =>
            Log.Entries.Where(e => e.Level == LogLevel.Warning && e.EventId.Name == RetryEventName).ToList();

        public void Api(Func<HttpRequestMessage, HttpResponseMessage> answer) => _api.Enqueue(answer);

        public void Token(Func<HttpRequestMessage, HttpResponseMessage> answer) => _token.Enqueue(answer);

        private HttpResponseMessage Answer(HttpRequestMessage request)
        {
            if (request.RequestUri?.Host == TokenHost)
            {
                return _token.Count > 0 ? _token.Dequeue()(request) : TokenIssued();
            }

            if (_api.Count > 0)
            {
                _lastApi = _api.Dequeue();
            }

            Assert.NotNull(_lastApi);
            return _lastApi(request);
        }
    }

    private sealed class StalledContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            Task.Delay(Timeout.Infinite);

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) =>
            Task.Delay(Timeout.Infinite, cancellationToken);

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }
}
