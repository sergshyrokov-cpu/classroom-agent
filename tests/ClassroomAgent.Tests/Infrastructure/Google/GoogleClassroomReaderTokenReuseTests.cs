using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;
using ClassroomAgent.Infrastructure.Google;
using ClassroomAgent.Tests.TestInfrastructure;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;

namespace ClassroomAgent.Tests.Infrastructure.Google;

/// <summary>
/// US-077 AC-001…AC-006, spec FR-001…FR-005, VR-001…VR-002a, S-01…S-03: the access token of
/// <see cref="GoogleClassroomReader"/> is remembered and reused per (technical account, scope set), through the real
/// adapter over a fake token endpoint and a manual clock (TC-4). Every assertion is on what crosses the transport or
/// on what the reader still holds afterwards; no test names an internal type of the cache.
/// </summary>
public sealed class GoogleClassroomReaderTokenReuseTests
{
    private const string Reference = "installation-google-key";
    private const string Account = AccessCheckTestData.TechnicalAccount;
    private const string OtherAccount = "other-technical-account@synthetic-school.test";
    private const string TokenHost = "oauth2.googleapis.com";

    /// <summary>A real-time window in which a defective, unserialized reader would send its own token request.</summary>
    private static readonly TimeSpan OverlapWindow = TimeSpan.FromMilliseconds(200);

    private static readonly DateTimeOffset Start = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] CourseAndRosterScopes =
        [GoogleDelegationScopes.CoursesReadonly, GoogleDelegationScopes.RostersReadonly, GoogleDelegationScopes.ProfileEmails];

    private static readonly string[] CourseWorkScopes =
        [GoogleDelegationScopes.CourseWorkStudentsReadonly, GoogleDelegationScopes.CourseWorkMaterialsReadonly];

    // ---- AC-001 One token for many reads ---------------------------------------------------------------------------

    /// <summary>AC-001, FR-003: several consecutive reads of one pair ask once, and every request carries that token.</summary>
    [Fact]
    public async Task ConsecutiveReads_ForOnePair_RequestOneToken_AndEveryReadCarriesIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World();

        await world.ReadCoursesAsync(Account, ct);
        await world.Reader.ReadRosterAsync(Account, CourseTestData.CourseId(1), ct);
        await world.Reader.ReadRosterAsync(Account, CourseTestData.CourseId(2), ct);

        var token = Assert.Single(world.Issued).Token;
        Assert.Equal(5, world.ApiRequests.Count);
        Assert.All(world.ApiRequests, r => Assert.Equal("Bearer " + token, r.Headers["Authorization"]));
    }

    /// <summary>AC-001, FR-003: each page of a paged read takes the remembered token, so a long list costs one request.</summary>
    [Fact]
    public async Task EveryPageOfAPagedRead_CarriesTheRememberedToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World();
        world.Api(_ => CoursePage("page-2"));
        world.Api(_ => CoursePage("page-3"));
        world.Api(_ => CoursePage(null));

        await world.ReadCoursesAsync(Account, ct);

        var token = Assert.Single(world.Issued).Token;
        Assert.Equal(3, world.ApiRequests.Count);
        Assert.All(world.ApiRequests, r => Assert.Equal("Bearer " + token, r.Headers["Authorization"]));
    }

    // ---- AC-002 A different account or scope set gets its own token ------------------------------------------------

    /// <summary>AC-002, FR-002, S-03: alternating accounts each get one token and never carry the other's.</summary>
    [Fact]
    public async Task DifferentAccounts_EachGetTheirOwnToken_AndNeverTheOthers()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World();

        await world.ReadCoursesAsync(Account, ct);
        await world.ReadCoursesAsync(OtherAccount, ct);
        await world.ReadCoursesAsync(Account, ct);
        await world.ReadCoursesAsync(OtherAccount, ct);

        Assert.Equal(2, world.Issued.Count);
        world.AssertEveryRequestCarriedItsOwnPairsToken([Account, OtherAccount, Account, OtherAccount]);
    }

    /// <summary>FR-002: the account compares ordinally and exactly, as passed to the read.</summary>
    [Fact]
    public async Task AccountsDifferingOnlyByCase_AreDifferentPairs()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World();

        await world.ReadCoursesAsync(Account, ct);
        await world.ReadCoursesAsync(Account.ToUpperInvariant(), ct);

        Assert.Equal(2, world.Issued.Count);
    }

    /// <summary>AC-002, FR-002, S-03, AC-011: each of today's two scope sets gets its own token, with exactly its own scopes.</summary>
    [Fact]
    public async Task TheTwoScopeSets_EachGetTheirOwnToken_WithExactlyTheirScopes()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World();

        await world.ReadCoursesAsync(Account, ct);
        await world.Reader.ReadSubmissionsAsync(Account, CourseTestData.CourseId(1), ct);
        await world.Reader.ReadRosterAsync(Account, CourseTestData.CourseId(1), ct);
        await world.Reader.ReadCourseWorkAsync(Account, CourseTestData.CourseId(1), ct);

        Assert.Equal(2, world.Issued.Count);
        var roster = world.Issued[0];
        var work = world.Issued[1];
        Assert.Equal(CourseAndRosterScopes.Order(StringComparer.Ordinal), roster.Scopes.Order(StringComparer.Ordinal));
        Assert.Equal(CourseWorkScopes.Order(StringComparer.Ordinal), work.Scopes.Order(StringComparer.Ordinal));

        foreach (var request in world.ApiRequests)
        {
            var expected = request.Uri!.AbsolutePath.Contains("/courseWork", StringComparison.OrdinalIgnoreCase) ? work : roster;
            Assert.Equal("Bearer " + expected.Token, request.Headers["Authorization"]);
        }
    }

    // ---- AC-003 Expiry -----------------------------------------------------------------------------------------------

    /// <summary>
    /// AC-003, FR-003: reuse needs <c>now + 5 min &lt; expiry</c>. With a one-hour token, one second before the margin
    /// the token is reused; exactly at the margin and after it, a new one is requested.
    /// </summary>
    [Theory]
    [InlineData(3299, 1)]
    [InlineData(3300, 2)]
    [InlineData(3301, 2)]
    public async Task ATokenAtTheMargin_IsReusedOnlyBeforeIt(int elapsedSeconds, int expectedTokens)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World { ExpiresIn = 3600 };

        await world.ReadCoursesAsync(Account, ct);
        world.Time.Advance(TimeSpan.FromSeconds(elapsedSeconds));
        await world.ReadCoursesAsync(Account, ct);

        Assert.Equal(expectedTokens, world.Issued.Count);
        Assert.Equal("Bearer " + world.Issued[^1].Token, world.ApiRequests[^1].Headers["Authorization"]);
    }

    /// <summary>AC-003: past expiry a new token is requested <b>before</b> the read is sent, and the old one never goes out again.</summary>
    [Fact]
    public async Task AnExpiredToken_IsReplacedBeforeTheReadIsSent()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World { ExpiresIn = 3600 };

        await world.ReadCoursesAsync(Account, ct);
        world.Time.Advance(TimeSpan.FromHours(2));
        await world.ReadCoursesAsync(Account, ct);

        Assert.Equal(
            [TokenHost, "classroom.googleapis.com", TokenHost, "classroom.googleapis.com"],
            world.Transport.Requests.Select(r => r.Uri!.Host));
        Assert.Equal("Bearer " + world.Issued[1].Token, world.ApiRequests[1].Headers["Authorization"]);
    }

    /// <summary>FR-003: expiry counts from just before the token request, so a slow answer cannot lengthen the token's life.</summary>
    [Fact]
    public async Task Expiry_IsCountedFromBeforeTheTokenRequestWasSent()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World { ExpiresIn = 3600 };
        var slow = true;
        world.BeforeTokenAnswer = () =>
        {
            if (slow)
            {
                slow = false;
                world.Time.Advance(TimeSpan.FromMinutes(10));
            }
        };

        await world.ReadCoursesAsync(Account, ct);
        world.Time.Advance(TimeSpan.FromMinutes(45)); // 55 minutes after the request was sent: at the margin.
        await world.ReadCoursesAsync(Account, ct);

        Assert.Equal(2, world.Issued.Count);
    }

    /// <summary>AC-003, FR-003, I-1: a long paged read that crosses the margin takes a new token before the next page.</summary>
    [Fact]
    public async Task APagedReadCrossingTheMargin_TakesANewTokenForTheNextPage()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World { ExpiresIn = 3600 };
        world.Api(_ =>
        {
            world.Time.Advance(TimeSpan.FromSeconds(3300));
            return CoursePage("page-2");
        });
        world.Api(_ => CoursePage(null));

        await world.ReadCoursesAsync(Account, ct);

        Assert.Equal(2, world.Issued.Count);
        Assert.Equal("Bearer " + world.Issued[0].Token, world.ApiRequests[0].Headers["Authorization"]);
        Assert.Equal("Bearer " + world.Issued[1].Token, world.ApiRequests[1].Headers["Authorization"]);
    }

    /// <summary>AC-003, FR-003, I-1, I-2: a retried attempt takes its token at that moment, not the one the failed attempt had.</summary>
    [Fact]
    public async Task ARetriedAttempt_TakesItsTokenAtThatMoment()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World { ExpiresIn = 3600 };
        await world.ReadCoursesAsync(Account, ct);
        world.Time.Advance(TimeSpan.FromSeconds(3299));
        world.Api(_ => ScriptedHttpHandler.EmptyResponse(HttpStatusCode.ServiceUnavailable));
        world.Api(_ => CoursePage(null));

        var read = world.ReadCoursesAsync(Account, ct);
        await world.Time.AdvanceWhenDueAsync(TimeSpan.FromSeconds(2), ct);
        await read.WaitAsync(ManualTimeProvider.RealTimeLimit, ct);

        Assert.Equal(2, world.Issued.Count);
        Assert.Equal("Bearer " + world.Issued[0].Token, world.ApiRequests[1].Headers["Authorization"]);
        Assert.Equal("Bearer " + world.Issued[1].Token, world.ApiRequests[2].Headers["Authorization"]);
    }

    /// <summary>VR-002: a token with no expiry, or one not above the margin, serves its own attempt and is not remembered.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(300)]
    public async Task AShortLivedToken_IsUsedOnce_AndNotRemembered(int? expiresIn)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World { ExpiresIn = expiresIn };

        await world.ReadCoursesAsync(Account, ct);
        await world.ReadCoursesAsync(Account, ct);

        Assert.Equal(2, world.Issued.Count);
        Assert.Equal("Bearer " + world.Issued[0].Token, world.ApiRequests[0].Headers["Authorization"]);
        Assert.Equal("Bearer " + world.Issued[1].Token, world.ApiRequests[1].Headers["Authorization"]);
    }

    /// <summary>VR-002: one second above the margin is the first life that is remembered.</summary>
    [Fact]
    public async Task ATokenJustAboveTheMargin_IsRemembered()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World { ExpiresIn = 301 };

        await world.ReadCoursesAsync(Account, ct);
        await world.ReadCoursesAsync(Account, ct);

        Assert.Single(world.Issued);
    }

    /// <summary>VR-002a, AC-003, I-12: a token Google reports as already expired is neither sent nor remembered.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ATokenAlreadyExpired_IsNotSent_AndTheReadEndsUnexpected(int expiresIn)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World { ExpiresIn = expiresIn };

        var failure = await Assert.ThrowsAsync<GoogleReadFailedException>(() => world.ReadCoursesAsync(Account, ct));

        Assert.Equal(GoogleReadFailureKind.Unexpected, failure.Kind);
        Assert.Empty(world.ApiRequests);

        world.ExpiresIn = 3600;
        await world.ReadCoursesAsync(Account, ct);
        Assert.Equal(2, world.Issued.Count);
    }

    /// <summary>VR-001, AC-005: an empty token string ends the read as Unexpected and is not remembered.</summary>
    [Fact]
    public async Task AnEmptyToken_IsNotSent_AndTheReadEndsUnexpected()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World();
        world.Token(_ => ScriptedHttpHandler.JsonResponse(HttpStatusCode.OK, """{"access_token":"","expires_in":3599,"token_type":"Bearer"}"""));

        var failure = await Assert.ThrowsAsync<GoogleReadFailedException>(() => world.ReadCoursesAsync(Account, ct));

        Assert.Equal(GoogleReadFailureKind.Unexpected, failure.Kind);
        Assert.Empty(world.ApiRequests);

        await world.ReadCoursesAsync(Account, ct);
        Assert.Equal(2, world.Transport.Requests.Count(r => r.Uri!.Host == TokenHost));
        Assert.Single(world.ApiRequests);
    }

    // ---- AC-004 Concurrent reads -------------------------------------------------------------------------------------

    /// <summary>AC-004, FR-004: N concurrent reads of one pair with no valid token cause exactly one token request.</summary>
    [Fact]
    public async Task ConcurrentReads_ForOnePair_CauseExactlyOneTokenRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World();
        var gate = world.HoldTokenAnswers();

        var reads = Enumerable.Range(0, 8).Select(_ => Task.Run(() => world.ReadCoursesAsync(Account, ct), ct)).ToList();
        await world.WaitForTokenRequestsAsync(1, ct);
        await world.GiveOverlapAChanceAsync(2, ct);
        gate.SetResult();
        await Task.WhenAll(reads).WaitAsync(ManualTimeProvider.RealTimeLimit, ct);

        var token = Assert.Single(world.Issued).Token;
        Assert.Equal(8, world.ApiRequests.Count);
        Assert.All(world.ApiRequests, r => Assert.Equal("Bearer " + token, r.Headers["Authorization"]));
    }

    /// <summary>AC-004, FR-004, S-03: K pairs cause exactly K token requests, and no read receives another pair's token.</summary>
    [Fact]
    public async Task ConcurrentReads_ForSeveralPairs_CauseOneTokenRequestPerPair()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World();
        var gate = world.HoldTokenAnswers();

        var reads = new List<Task>();
        for (var i = 0; i < 4; i++)
        {
            reads.Add(Task.Run(() => world.ReadCoursesAsync(Account, ct), ct));
            reads.Add(Task.Run(() => world.ReadCoursesAsync(OtherAccount, ct), ct));
            reads.Add(Task.Run(() => world.Reader.ReadSubmissionsAsync(Account, CourseTestData.CourseId(1), ct), ct));
        }

        await world.WaitForTokenRequestsAsync(3, ct);
        await world.GiveOverlapAChanceAsync(4, ct);
        gate.SetResult();
        await Task.WhenAll(reads).WaitAsync(ManualTimeProvider.RealTimeLimit, ct);

        Assert.Equal(3, world.Issued.Count);
        foreach (var request in world.ApiRequests)
        {
            var issued = world.Issued.Single(i => "Bearer " + i.Token == request.Headers["Authorization"]);
            var isWork = request.Uri!.AbsolutePath.Contains("/courseWork", StringComparison.OrdinalIgnoreCase);
            Assert.Equal(isWork ? CourseWorkScopes.Length : CourseAndRosterScopes.Length, issued.Scopes.Count);
        }
    }

    /// <summary>
    /// FR-004: when the endpoint refuses, each waiting read makes its own token request in turn and ends: N requests,
    /// never two at the same moment for one pair.
    /// </summary>
    [Fact]
    public async Task ConcurrentReads_WhenTheEndpointRefuses_EachAskOnce_OneAtATime()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World { TokenRefusal = () => TokenError(HttpStatusCode.BadRequest, "invalid_grant", "Invalid JWT Signature."), TokenAnswerTakes = TimeSpan.FromMilliseconds(25) };

        var reads = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() => Assert.ThrowsAsync<GoogleReadFailedException>(() => world.ReadCoursesAsync(Account, ct)), ct))
            .ToList();
        var failures = await Task.WhenAll(reads).WaitAsync(ManualTimeProvider.RealTimeLimit, ct);

        Assert.All(failures, f => Assert.Equal(SyncDiagnosis.KeyRejected, f.Diagnosis));
        Assert.Equal(4, world.TokenRequests.Count);
        Assert.Equal(1, world.MostTokenRequestsInFlight);
        Assert.Empty(world.ApiRequests);
    }

    /// <summary>
    /// SPEC_REVIEW m-1, FR-004, VR-002: "exactly one" holds only for a token that can be remembered. A successful answer
    /// that is not remembered leaves the next waiter to ask for itself, one at a time, and every read still succeeds.
    /// </summary>
    [Fact]
    public async Task ConcurrentReads_WithATokenThatCannotBeRemembered_EachUseTheirOwn_OneAtATime()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World { ExpiresIn = 300, TokenAnswerTakes = TimeSpan.FromMilliseconds(25) };

        var reads = Enumerable.Range(0, 4).Select(_ => Task.Run(() => world.ReadCoursesAsync(Account, ct), ct)).ToList();
        await Task.WhenAll(reads).WaitAsync(ManualTimeProvider.RealTimeLimit, ct);

        Assert.Equal(4, world.Issued.Count);
        Assert.Equal(1, world.MostTokenRequestsInFlight);
        Assert.Equal(4, world.ApiRequests.Count);
        Assert.Equal(4, world.ApiRequests.Select(r => r.Headers["Authorization"]).Distinct().Count());
    }

    /// <summary>FR-004: a read cancelled while waiting makes no token request and ends as any cancelled read does.</summary>
    [Fact]
    public async Task AReadCancelledWhileWaiting_MakesNoTokenRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World();
        var gate = world.HoldTokenAnswers();
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var holder = Task.Run(() => world.ReadCoursesAsync(Account, ct), ct);
        await world.WaitForTokenRequestsAsync(1, ct);
        var waiter = Task.Run(() => world.ReadCoursesAsync(Account, cancelled.Token), ct);
        await world.GiveOverlapAChanceAsync(2, ct);

        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter.WaitAsync(ManualTimeProvider.RealTimeLimit, ct));

        gate.SetResult();
        await holder.WaitAsync(ManualTimeProvider.RealTimeLimit, ct);
        Assert.Single(world.Issued);
        Assert.Single(world.ApiRequests);
    }

    /// <summary>
    /// SPEC_REVIEW m-2, FR-003: the token request and the wait for another read's request stay outside the Classroom
    /// attempt's per-attempt bound: token time plus Classroom time above the bound is one successful attempt, for the read
    /// that asked and for the read that waited.
    /// </summary>
    [Fact]
    public async Task TheTokenRequest_AndTheWaitForIt_AreNotChargedToTheClassroomAttempt()
    {
        var ct = TestContext.Current.CancellationToken;

        // The bound runs on the system clock (GoogleRetryHandler), so real time is what can exceed it: each step fits the
        // bound with room to spare, and only their sum does not.
        var world = new World(attemptTimeout: TimeSpan.FromSeconds(2)) { TokenAnswerTakes = TimeSpan.FromSeconds(1.2) };
        world.ApiAsync(async _ =>
        {
            await Task.Delay(TimeSpan.FromSeconds(1.2), ct);
            return CoursePage(null);
        });

        var asker = Task.Run(() => world.ReadCoursesAsync(Account, ct), ct);
        await world.WaitForTokenRequestsAsync(1, ct);
        var waiter = Task.Run(() => world.ReadCoursesAsync(Account, ct), ct);
        await Task.WhenAll(asker, waiter).WaitAsync(ManualTimeProvider.RealTimeLimit, ct);

        Assert.Single(world.Issued);
        Assert.Equal(2, world.ApiRequests.Count);
        Assert.DoesNotContain(world.Log.Entries, e => e.Level == LogLevel.Warning);
    }

    // ---- AC-005 Failures are not remembered, and their diagnosis is unchanged ---------------------------------------

    public static TheoryData<HttpStatusCode, string, string, SyncDiagnosis> Refusals => new()
    {
        { HttpStatusCode.Unauthorized, "unauthorized_client", "Client is unauthorized to retrieve access tokens using this method", SyncDiagnosis.ScopeNotAuthorized },
        { HttpStatusCode.BadRequest, "invalid_grant", "Invalid JWT Signature.", SyncDiagnosis.KeyRejected },
        { HttpStatusCode.BadRequest, "invalid_grant", "Invalid email or User ID", SyncDiagnosis.TechnicalAccountUnknown },
    };

    /// <summary>AC-005, FR-005: a refusal keeps today's kind and diagnosis, is not remembered, and the next read asks again and succeeds.</summary>
    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task ARefusal_KeepsItsDiagnosis_AndTheNextReadAsksAgain(
        HttpStatusCode status,
        string error,
        string description,
        SyncDiagnosis expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World();
        world.Token(_ => TokenError(status, error, description));

        var failure = await Assert.ThrowsAsync<GoogleReadFailedException>(() => world.ReadCoursesAsync(Account, ct));
        Assert.Equal(GoogleReadFailureKind.Configuration, failure.Kind);
        Assert.Equal(expected, failure.Diagnosis);
        Assert.Empty(world.ApiRequests);

        await world.ReadCoursesAsync(Account, ct);

        Assert.Equal(2, world.TokenRequests.Count);
        Assert.Single(world.ApiRequests);
        Assert.Equal("Bearer " + Assert.Single(world.Issued).Token, world.ApiRequests[0].Headers["Authorization"]);
    }

    public static TheoryData<string> Outages => new("503", "network");

    /// <summary>
    /// AC-005, FR-003, FR-004, I-11: an outage of the token endpoint ends the read as Transient after four HTTP attempts —
    /// the Classroom retry loop does not repeat it, so the attempts do not multiply. The next read recovers.
    /// </summary>
    [Theory]
    [MemberData(nameof(Outages))]
    public async Task ATokenOutage_EndsTheReadAfterFourAttempts_AndIsNotRemembered(string outage)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            world.Token(_ => outage == "503"
                ? ScriptedHttpHandler.EmptyResponse(HttpStatusCode.ServiceUnavailable)
                : throw new HttpRequestException("synthetic connection failure"));
        }

        var read = world.ReadCoursesAsync(Account, ct);
        foreach (var pause in new[] { 2, 8, 30 })
        {
            await world.Time.AdvanceWhenDueAsync(TimeSpan.FromSeconds(pause), ct);
        }

        var failure = await Assert.ThrowsAsync<GoogleReadFailedException>(() => read.WaitAsync(ManualTimeProvider.RealTimeLimit, ct));

        Assert.Equal(GoogleReadFailureKind.Transient, failure.Kind);
        Assert.Equal(SyncDiagnosis.GoogleUnavailable, failure.Diagnosis);
        Assert.Equal(4, world.TokenRequests.Count);
        Assert.Empty(world.ApiRequests);

        await world.ReadCoursesAsync(Account, ct);
        Assert.Equal(5, world.TokenRequests.Count);
        Assert.Single(world.ApiRequests);
    }

    /// <summary>FR-005: a failed request after expiry leaves the expired token unused; the read fails instead of sending it.</summary>
    [Fact]
    public async Task ARefusalAfterExpiry_DoesNotFallBackToTheExpiredToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World { ExpiresIn = 3600 };
        await world.ReadCoursesAsync(Account, ct);
        world.Time.Advance(TimeSpan.FromHours(2));
        world.Token(_ => TokenError(HttpStatusCode.BadRequest, "invalid_grant", "Invalid JWT Signature."));

        await Assert.ThrowsAsync<GoogleReadFailedException>(() => world.ReadCoursesAsync(Account, ct));

        Assert.Single(world.ApiRequests);
    }

    /// <summary>
    /// FR-005, AC-005: a key that is missing ends as Configuration / KeyUnavailable at the moment a token is needed, and
    /// I-3: while the remembered token is valid the store is not consulted, so the removal is noticed only then.
    /// </summary>
    [Fact]
    public async Task ARemovedKey_IsNoticedWhenATokenIsNeeded()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World { ExpiresIn = 3600 };
        await world.ReadCoursesAsync(Account, ct);

        world.Secrets.Remove();
        await world.ReadCoursesAsync(Account, ct);
        Assert.Single(world.Issued);

        world.Time.Advance(TimeSpan.FromHours(1));
        var failure = await Assert.ThrowsAsync<GoogleReadFailedException>(() => world.ReadCoursesAsync(Account, ct));

        Assert.Equal(GoogleReadFailureKind.Configuration, failure.Kind);
        Assert.Equal(SyncDiagnosis.KeyUnavailable, failure.Diagnosis);
        Assert.Equal(2, world.ApiRequests.Count);
    }

    /// <summary>FR-005: a <c>401</c> from Classroom drops the token, the attempt is repeated with a new one, and the new one is the one remembered.</summary>
    [Fact]
    public async Task A401_ReplacesTheToken_AndTheAttemptIsRepeatedWithTheNewOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World();
        world.Api(_ => ScriptedHttpHandler.EmptyResponse(HttpStatusCode.Unauthorized));
        world.Api(_ => CoursePage(null));

        await world.ReadCoursesAsync(Account, ct);
        await world.ReadCoursesAsync(Account, ct);

        Assert.Equal(2, world.Issued.Count);
        Assert.Equal("Bearer " + world.Issued[0].Token, world.ApiRequests[0].Headers["Authorization"]);
        Assert.Equal("Bearer " + world.Issued[1].Token, world.ApiRequests[1].Headers["Authorization"]);
        Assert.Equal("Bearer " + world.Issued[1].Token, world.ApiRequests[2].Headers["Authorization"]);
    }

    /// <summary>
    /// FR-005: the number of attempts on repeated <c>401</c> is no larger than today (the library's three tries, measured
    /// on the code before this Story), and the read ends as an unexpected failure as it does today.
    /// </summary>
    [Fact]
    public async Task EveryAttemptAnswering401_StopsAsToday()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World();
        world.Api(_ => ScriptedHttpHandler.EmptyResponse(HttpStatusCode.Unauthorized));

        var failure = await Assert.ThrowsAsync<GoogleReadFailedException>(() => world.ReadCoursesAsync(Account, ct));

        Assert.Equal(GoogleReadFailureKind.Unexpected, failure.Kind);
        Assert.InRange(world.ApiRequests.Count, 1, 3);
    }

    /// <summary>FR-005: a <c>401</c> to an attempt that carried an older token does not drop the newer token now remembered.</summary>
    [Fact]
    public async Task A401ForAnOlderToken_DoesNotDropTheNewerOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World { ExpiresIn = 3600 };
        await world.ReadCoursesAsync(Account, ct);

        var firstAttempt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        world.ApiAsync(async _ =>
        {
            await firstAttempt.Task;
            return ScriptedHttpHandler.EmptyResponse(HttpStatusCode.Unauthorized);
        });

        var slow = Task.Run(() => world.ReadCoursesAsync(Account, ct), ct);
        await world.WaitForApiRequestsAsync(2, ct);

        // Another read passes the margin, and so replaces the token, while the slow attempt is still out. It gets an
        // answer of its own: a scripted answer repeats until replaced, and the held one must not be repeated to it.
        world.Api(_ => CoursePage(null));
        world.Time.Advance(TimeSpan.FromSeconds(3300));
        await world.ReadCoursesAsync(Account, ct).WaitAsync(ManualTimeProvider.RealTimeLimit, ct);
        Assert.Equal(2, world.Issued.Count);

        firstAttempt.SetResult();
        await slow.WaitAsync(ManualTimeProvider.RealTimeLimit, ct);

        Assert.Equal(2, world.Issued.Count);
        Assert.Equal("Bearer " + world.Issued[1].Token, world.ApiRequests[^1].Headers["Authorization"]);
    }

    // ---- AC-006 The key and the token stay out of sight --------------------------------------------------------------

    /// <summary>AC-006, FR-003, S-02: the key is resolved once per token request, and a reused token loads no key.</summary>
    [Fact]
    public async Task TheKey_IsResolvedOncePerTokenRequest_AndNotForAReusedToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World { ExpiresIn = 3600 };

        await world.ReadCoursesAsync(Account, ct);
        await world.ReadCoursesAsync(Account, ct);
        await world.ReadCoursesAsync(Account, ct);
        Assert.Equal(1, world.Secrets.Resolutions);

        world.Time.Advance(TimeSpan.FromHours(1));
        await world.ReadCoursesAsync(Account, ct);
        Assert.Equal(2, world.Secrets.Resolutions);
    }

    /// <summary>
    /// AC-006, FR-001, S-01, S-02: between requests the reader holds the token string and its expiry, and nothing built
    /// from the key. The walk covers everything the reader owns; it finds the token, so it is not vacuous.
    /// </summary>
    [Fact]
    public async Task BetweenRequests_TheReaderHoldsTheToken_ButNothingOfTheKey()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World();

        await world.ReadCoursesAsync(Account, ct);

        var token = Assert.Single(world.Issued).Token;
        var held = ReachableFrom(world.Reader);
        Assert.Contains(held, o => o is string s && s == token);
        Assert.DoesNotContain(held, o => o is ServiceAccountCredential or ICredential or System.Security.Cryptography.AsymmetricAlgorithm);
        Assert.DoesNotContain(held, o => o is string s && s.Contains("PRIVATE KEY", StringComparison.Ordinal));
    }

    /// <summary>S-01: no object the reader owns, a record's generated members included, shows the token through <c>ToString</c>.</summary>
    [Fact]
    public async Task NothingTheReaderHolds_ExposesTheTokenThroughToString()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World();

        await world.ReadCoursesAsync(Account, ct);

        var token = Assert.Single(world.Issued).Token;
        var holders = ReachableFrom(world.Reader)
            .Where(o => o is not string && HoldsDirectly(o, token))
            .ToList();
        Assert.NotEmpty(holders);
        Assert.All(holders, h => Assert.DoesNotContain(token, h.ToString() ?? string.Empty, StringComparison.Ordinal));
    }

    /// <summary>FR-005, I-4: an expired token is removed when a token is requested for any pair.</summary>
    [Fact]
    public async Task AnExpiredToken_IsForgottenAtTheNextTokenRequestOfAnyPair()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World { ExpiresIn = 3600 };
        await world.ReadCoursesAsync(Account, ct);
        var expired = world.Issued[0].Token;

        world.Time.Advance(TimeSpan.FromHours(2));
        await world.ReadCoursesAsync(OtherAccount, ct);

        Assert.DoesNotContain(ReachableFrom(world.Reader), o => o is string s && s == expired);
    }

    /// <summary>FR-005: a token still valid stays when another pair's request is made.</summary>
    [Fact]
    public async Task AValidToken_StaysWhenAnotherPairRequestsOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World { ExpiresIn = 3600 };
        await world.ReadCoursesAsync(Account, ct);

        await world.ReadCoursesAsync(OtherAccount, ct);
        await world.ReadCoursesAsync(Account, ct);

        Assert.Equal(2, world.Issued.Count);
    }

    /// <summary>AC-006, S-01: neither the token nor the key appears in a log line or in a failure, on the success and the failure path.</summary>
    [Fact]
    public async Task NeitherTheTokenNorTheKey_AppearInALogLineOrAFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new World();
        world.Api(_ => ScriptedHttpHandler.EmptyResponse(HttpStatusCode.ServiceUnavailable));
        world.Api(_ => CoursePage(null));
        var read = world.ReadCoursesAsync(Account, ct);
        await world.Time.AdvanceWhenDueAsync(TimeSpan.FromSeconds(2), ct);
        await read.WaitAsync(ManualTimeProvider.RealTimeLimit, ct);

        world.Api(_ => ScriptedHttpHandler.EmptyResponse(HttpStatusCode.BadRequest));
        var failure = await Assert.ThrowsAsync<GoogleReadFailedException>(() => world.ReadCoursesAsync(Account, ct));

        var token = world.Issued[0].Token;
        var text = string.Join('\n', world.Log.Entries.Select(e => e.Message + e.Exception)) + failure;
        Assert.DoesNotContain(token, text, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE KEY", text, StringComparison.Ordinal);
        Assert.NotEmpty(world.Log.Entries);
    }

    // ---- helpers ------------------------------------------------------------------------------------------------------

    private static HttpResponseMessage CoursePage(string? nextPageToken) =>
        ScriptedHttpHandler.JsonResponse(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new Dictionary<string, object?> { ["courses"] = Array.Empty<object>(), ["nextPageToken"] = nextPageToken }));

    private static HttpResponseMessage TokenError(HttpStatusCode status, string error, string description) =>
        ScriptedHttpHandler.JsonResponse(
            status,
            JsonSerializer.Serialize(new Dictionary<string, string> { ["error"] = error, ["error_description"] = description }));

    private static bool HoldsDirectly(object holder, string value) =>
        FieldsOf(holder.GetType()).Any(f => f.FieldType == typeof(string) && (string?)f.GetValue(holder) == value);

    private static IEnumerable<FieldInfo> FieldsOf(Type type)
    {
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                yield return field;
            }
        }
    }

    /// <summary>
    /// Every object the reader owns: a walk of instance fields from the reader, leaving out what it merely was handed
    /// (the secret store, the transport, the clock, the jitter, the settings and the logger).
    /// </summary>
    private static List<object> ReachableFrom(GoogleClassroomReader reader)
    {
        var handedIn = new[]
        {
            typeof(ISecretStore), typeof(HttpMessageHandler), typeof(TimeProvider), typeof(IGoogleRetryJitter),
            typeof(GoogleServiceAccountSettings), typeof(ILogger),
        };
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<object>();
        foreach (var field in FieldsOf(reader.GetType()).Where(f => !handedIn.Any(t => t.IsAssignableFrom(f.FieldType))))
        {
            if (field.GetValue(reader) is { } value)
            {
                pending.Push(value);
            }
        }

        while (pending.TryPop(out var current))
        {
            if (current is Type or MemberInfo or Pointer || !seen.Add(current) || current.GetType().IsPrimitive || current is string)
            {
                continue;
            }

            Assert.True(seen.Count < 200_000, "The walk from the reader did not end.");
            if (current is Array array)
            {
                if (!array.GetType().GetElementType()!.IsPrimitive)
                {
                    foreach (var item in array)
                    {
                        if (item is not null)
                        {
                            pending.Push(item);
                        }
                    }
                }

                continue;
            }

            foreach (var field in FieldsOf(current.GetType()).Where(f => !f.FieldType.IsPointer))
            {
                if (field.GetValue(current) is { } value)
                {
                    if (value is string text)
                    {
                        seen.Add(text);
                    }
                    else
                    {
                        pending.Push(value);
                    }
                }
            }
        }

        return seen.ToList();
    }

    private sealed record IssuedToken(string Token, string Subject, IReadOnlyList<string> Scopes);

    private sealed class SpySecretStore : ISecretStore
    {
        private readonly string _json = SyntheticServiceAccountKey.Create();
        private bool _present = true;
        private int _resolutions;

        public int Resolutions => Volatile.Read(ref _resolutions);

        public void Remove() => _present = false;

        public string? Resolve(string reference)
        {
            Interlocked.Increment(ref _resolutions);
            return _present && reference == Reference ? _json : null;
        }
    }

    private sealed class World
    {
        private readonly ConcurrentQueue<Func<HttpRequestMessage, HttpResponseMessage>> _tokenAnswers = new();
        private readonly ConcurrentQueue<Func<HttpRequestMessage, Task<HttpResponseMessage>>> _apiAnswers = new();
        private readonly ConcurrentQueue<IssuedToken> _issued = new();
        private readonly Lock _gate = new();
        private Func<HttpRequestMessage, Task<HttpResponseMessage>>? _lastApi;
        private TaskCompletionSource? _tokenGate;
        private TaskCompletionSource? _apiGate;
        private int _serial;
        private int _inFlight;
        private int _mostInFlight;

        public World(TimeSpan? attemptTimeout = null)
        {
            Time = new ManualTimeProvider(Start);
            Log = new CapturingLogger<GoogleClassroomReader>();
            Secrets = new SpySecretStore();
            Transport = new ScriptedHttpHandler(AnswerAsync);
            Reader = new GoogleClassroomReader(
                Secrets,
                new GoogleServiceAccountSettings(Reference),
                Transport,
                Time,
                new FixedRetryJitter(1.0),
                Log,
                attemptTimeout);
        }

        public ManualTimeProvider Time { get; }

        public CapturingLogger<GoogleClassroomReader> Log { get; }

        public SpySecretStore Secrets { get; }

        public ScriptedHttpHandler Transport { get; }

        public GoogleClassroomReader Reader { get; }

        /// <summary>The <c>expires_in</c> of an issued token; null leaves the field out.</summary>
        public int? ExpiresIn { get; set; } = 3599;

        public Action? BeforeTokenAnswer { get; set; }

        /// <summary>When set, every token request is refused with this answer.</summary>
        public Func<HttpResponseMessage>? TokenRefusal { get; set; }

        /// <summary>A real-time length of each token answer, so overlapping requests are observable.</summary>
        public TimeSpan TokenAnswerTakes { get; set; } = TimeSpan.Zero;

        public IReadOnlyList<IssuedToken> Issued => _issued.ToList();

        public int MostTokenRequestsInFlight => Volatile.Read(ref _mostInFlight);

        public IReadOnlyList<ScriptedHttpHandler.RecordedRequest> TokenRequests =>
            Transport.Requests.Where(r => r.Uri?.Host == TokenHost).ToList();

        public IReadOnlyList<ScriptedHttpHandler.RecordedRequest> ApiRequests =>
            Transport.Requests.Where(r => r.Uri?.Host != TokenHost).ToList();

        public void Token(Func<HttpRequestMessage, HttpResponseMessage> answer) => _tokenAnswers.Enqueue(answer);

        public void Api(Func<HttpRequestMessage, HttpResponseMessage> answer) =>
            _apiAnswers.Enqueue(request => Task.FromResult(answer(request)));

        public void ApiAsync(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer) => _apiAnswers.Enqueue(answer);

        public TaskCompletionSource HoldTokenAnswers()
        {
            _tokenGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _tokenGate;
        }

        public TaskCompletionSource HoldApiAnswers()
        {
            _apiGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _apiGate;
        }

        public Task WaitForTokenRequestsAsync(int count, CancellationToken ct) =>
            Time.WaitUntilAsync(() => TokenRequests.Count >= count, $"{count} token request(s)", ct);

        public Task WaitForApiRequestsAsync(int count, CancellationToken ct) =>
            Time.WaitUntilAsync(() => ApiRequests.Count >= count, $"{count} Classroom request(s)", ct);

        /// <summary>
        /// Holds the token answer open for a short real time, or until a surplus request has shown up: a defective reader
        /// would send its own request in this window. A correct one simply waits, whatever the window's length.
        /// </summary>
        public async Task GiveOverlapAChanceAsync(int surplusRequests, CancellationToken ct)
        {
            var deadline = DateTime.UtcNow + OverlapWindow;
            while (DateTime.UtcNow < deadline && TokenRequests.Count < surplusRequests + 8)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), ct);
            }
        }

        public async Task ReadCoursesAsync(string account, CancellationToken ct)
        {
            await foreach (var _ in Reader.ReadCoursesAsync(account, ct))
            {
                // Draining the sequence is what makes the requests happen.
            }
        }

        public void AssertEveryRequestCarriedItsOwnPairsToken(IReadOnlyList<string> accountsInOrder)
        {
            Assert.Equal(accountsInOrder.Count, ApiRequests.Count);
            for (var i = 0; i < accountsInOrder.Count; i++)
            {
                var issued = Issued.Single(t => "Bearer " + t.Token == ApiRequests[i].Headers["Authorization"]);
                Assert.Equal(accountsInOrder[i], issued.Subject);
            }
        }

        private async Task<HttpResponseMessage> AnswerAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.Host == TokenHost)
            {
                return await AnswerTokenAsync(request, cancellationToken);
            }

            Func<HttpRequestMessage, Task<HttpResponseMessage>>? answer;
            lock (_gate)
            {
                if (_apiAnswers.TryDequeue(out var next))
                {
                    _lastApi = next;
                }

                answer = _lastApi;
            }

            if (_apiGate is { } gate)
            {
                await gate.Task.WaitAsync(cancellationToken);
            }

            return answer is null ? DefaultApiAnswer(request) : await answer(request);
        }

        private static HttpResponseMessage DefaultApiAnswer(HttpRequestMessage request) =>
            ScriptedHttpHandler.JsonResponse(HttpStatusCode.OK, "{}");

        private async Task<HttpResponseMessage> AnswerTokenAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var inFlight = Interlocked.Increment(ref _inFlight);
            UpdateMost(inFlight);
            try
            {
                BeforeTokenAnswer?.Invoke();
                if (_tokenGate is { } gate)
                {
                    await gate.Task.WaitAsync(cancellationToken);
                }

                if (TokenAnswerTakes > TimeSpan.Zero)
                {
                    await Task.Delay(TokenAnswerTakes, cancellationToken);
                }

                if (TokenRefusal is { } refusal)
                {
                    return refusal();
                }

                if (_tokenAnswers.TryDequeue(out var scripted))
                {
                    return scripted(request);
                }

                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                var claims = ClaimsOf(body);
                var token = $"ya29.us077-{Interlocked.Increment(ref _serial)}";
                _issued.Enqueue(new IssuedToken(
                    token,
                    claims.GetProperty("sub").GetString()!,
                    claims.GetProperty("scope").GetString()!.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
                var answer = new Dictionary<string, object?> { ["access_token"] = token, ["token_type"] = "Bearer" };
                if (ExpiresIn is { } expiresIn)
                {
                    answer["expires_in"] = expiresIn;
                }

                return ScriptedHttpHandler.JsonResponse(HttpStatusCode.OK, JsonSerializer.Serialize(answer));
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }

        private void UpdateMost(int inFlight)
        {
            int most;
            do
            {
                most = Volatile.Read(ref _mostInFlight);
                if (inFlight <= most)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref _mostInFlight, inFlight, most) != most);
        }

        private static JsonElement ClaimsOf(string tokenRequestBody)
        {
            var form = tokenRequestBody.Split('&')
                .Select(p => p.Split('=', 2))
                .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1].Replace('+', ' ')), StringComparer.Ordinal);
            var payload = form["assertion"].Split('.')[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            return document.RootElement.Clone();
        }
    }
}
