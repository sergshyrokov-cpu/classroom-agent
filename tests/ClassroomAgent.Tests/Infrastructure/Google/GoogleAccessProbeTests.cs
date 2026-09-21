using System.Net;
using System.Text;
using System.Text.Json;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Infrastructure.Google;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Infrastructure.Google;

/// <summary>
/// US-011 spec FR-002…FR-005, FR-016, VR-004: the Google implementation of the port, driven offline through a scripted
/// transport with a key generated at run time (TC-4). It proves what the HTTP tests cannot: that each token request is
/// for one scope and impersonates the technical account, that the two reads are minimal GETs, and that Google's answers
/// are mapped onto the closed list of outcomes — an answer it does not recognise is never passed off as a configuration
/// diagnosis. The wire shapes are Google's documented error formats, written synthetically.
/// </summary>
public sealed class GoogleAccessProbeTests
{
    private const string Reference = "installation-google-key";
    private const string TechnicalAccount = AccessCheckTestData.TechnicalAccount;
    private static readonly string Scope = AccessCheckTestData.Scopes[1];

    private static readonly string[] GoogleHosts = ["oauth2.googleapis.com", "classroom.googleapis.com", "admin.googleapis.com"];

    private static GoogleAccessProbe ProbeWith(ScriptedHttpHandler transport, string? keyJson = null, string? reference = Reference) =>
        new(
            new DictionarySecretStore(new Dictionary<string, string> { [Reference] = keyJson ?? SyntheticServiceAccountKey.Create() }),
            new GoogleServiceAccountSettings(reference),
            transport);

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

    private static JsonElement ClaimsOf(ScriptedHttpHandler.RecordedRequest tokenRequest)
    {
        var form = tokenRequest.Body.Split('&')
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1].Replace('+', ' ')), StringComparer.Ordinal);
        Assert.Equal("urn:ietf:params:oauth:grant-type:jwt-bearer", form["grant_type"]);
        var payload = form["assertion"].Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        return document.RootElement.Clone();
    }

    // ---- delegation ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task ATokenRequest_IsForThatOneScope_ImpersonatingTheTechnicalAccount()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedHttpHandler((_, _) => Task.FromResult(TokenIssued()));
        var probe = ProbeWith(transport);

        var attempt = await probe.RequestDelegatedTokenAsync(TechnicalAccount, Scope, ct);

        Assert.Equal(AccessCheckStepOutcome.Succeeded, attempt.Outcome);
        Assert.NotNull(attempt.Token);
        var request = Assert.Single(transport.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("oauth2.googleapis.com", request.Uri!.Host);
        var claims = ClaimsOf(request);
        Assert.Equal(Scope, claims.GetProperty("scope").GetString());
        Assert.Equal(TechnicalAccount, claims.GetProperty("sub").GetString());
        Assert.Equal(SyntheticServiceAccountKey.ClientEmail, claims.GetProperty("iss").GetString());
    }

    /// <summary>Google's answer when the scope is not authorised in domain-wide delegation for this client ID.</summary>
    [Fact]
    public async Task UnauthorizedClient_IsScopeNotAuthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedHttpHandler((_, _) => Task.FromResult(TokenError(
            HttpStatusCode.Unauthorized,
            "unauthorized_client",
            AccessCheckTestData.GoogleErrorText + ", or client not authorized for any of the scopes requested.")));

        var attempt = await ProbeWith(transport).RequestDelegatedTokenAsync(TechnicalAccount, Scope, ct);

        Assert.Equal(AccessCheckStepOutcome.ScopeNotAuthorized, attempt.Outcome);
        Assert.Null(attempt.Token);
    }

    [Fact]
    public async Task AnUnknownImpersonatedUser_IsTechnicalAccountUnknown()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedHttpHandler((_, _) => Task.FromResult(TokenError(
            HttpStatusCode.BadRequest,
            "invalid_grant",
            "Invalid email or User ID")));

        var attempt = await ProbeWith(transport).RequestDelegatedTokenAsync(TechnicalAccount, Scope, ct);

        Assert.Equal(AccessCheckStepOutcome.TechnicalAccountUnknown, attempt.Outcome);
    }

    /// <summary>The key was deleted or disabled in the Owner's project.</summary>
    [Fact]
    public async Task AnInvalidSignature_IsKeyRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedHttpHandler((_, _) => Task.FromResult(TokenError(
            HttpStatusCode.BadRequest,
            "invalid_grant",
            "Invalid JWT Signature.")));

        var attempt = await ProbeWith(transport).RequestDelegatedTokenAsync(TechnicalAccount, Scope, ct);

        Assert.Equal(AccessCheckStepOutcome.KeyRejected, attempt.Outcome);
    }

    public static TheoryData<HttpStatusCode> Unavailable => new(
        HttpStatusCode.InternalServerError,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.TooManyRequests);

    [Theory]
    [MemberData(nameof(Unavailable))]
    public async Task AServerErrorOrThrottling_OnTheTokenEndpoint_IsGoogleUnavailable(HttpStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedHttpHandler((_, _) => Task.FromResult(ScriptedHttpHandler.EmptyResponse(status)));

        var attempt = await ProbeWith(transport).RequestDelegatedTokenAsync(TechnicalAccount, Scope, ct);

        Assert.Equal(AccessCheckStepOutcome.GoogleUnavailable, attempt.Outcome);
    }

    [Fact]
    public async Task ANetworkFailure_IsGoogleUnavailable()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedHttpHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("synthetic connection failure")));

        var attempt = await ProbeWith(transport).RequestDelegatedTokenAsync(TechnicalAccount, Scope, ct);

        Assert.Equal(AccessCheckStepOutcome.GoogleUnavailable, attempt.Outcome);
    }

    /// <summary>Spec FR-005: an answer the implementation cannot classify is GoogleUnavailable, never a configuration diagnosis.</summary>
    [Fact]
    public async Task AnUnrecognisedError_IsGoogleUnavailable()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedHttpHandler((_, _) => Task.FromResult(TokenError(
            HttpStatusCode.BadRequest,
            "something_google_added_later",
            "An error no one has seen yet.")));

        var attempt = await ProbeWith(transport).RequestDelegatedTokenAsync(TechnicalAccount, Scope, ct);

        Assert.Equal(AccessCheckStepOutcome.GoogleUnavailable, attempt.Outcome);
    }

    // ---- the key (spec FR-016, VR-004, I-1) ---------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task WithoutAReference_TheKeyIsUnavailable_AndNothingIsSent(string? reference)
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedHttpHandler((_, _) => Task.FromResult(TokenIssued()));

        var attempt = await ProbeWith(transport, reference: reference).RequestDelegatedTokenAsync(TechnicalAccount, Scope, ct);

        Assert.Equal(AccessCheckStepOutcome.KeyUnavailable, attempt.Outcome);
        Assert.Empty(transport.Requests);
    }

    [Fact]
    public async Task AReferenceTheStoreDoesNotHold_IsKeyUnavailable_AndNothingIsSent()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedHttpHandler((_, _) => Task.FromResult(TokenIssued()));

        var attempt = await ProbeWith(transport, reference: "some-other-reference").RequestDelegatedTokenAsync(TechnicalAccount, Scope, ct);

        Assert.Equal(AccessCheckStepOutcome.KeyUnavailable, attempt.Outcome);
        Assert.Empty(transport.Requests);
    }

    public static TheoryData<string> NotAKey => new(
        "not json at all",
        """{"type":"service_account","client_email":"x@y.iam.gserviceaccount.com"}""",
        """{"type":"service_account","private_key":"-----BEGIN PRIVATE KEY-----\nAAAA\n-----END PRIVATE KEY-----\n"}""",
        """{"type":"authorized_user","client_id":"1","client_secret":"s","refresh_token":"r"}""");

    [Theory]
    [MemberData(nameof(NotAKey))]
    public async Task ContentThatIsNotAServiceAccountKey_IsKeyUnavailable_AndNothingIsSent(string content)
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedHttpHandler((_, _) => Task.FromResult(TokenIssued()));

        var attempt = await ProbeWith(transport, keyJson: content).RequestDelegatedTokenAsync(TechnicalAccount, Scope, ct);

        Assert.Equal(AccessCheckStepOutcome.KeyUnavailable, attempt.Outcome);
        Assert.Empty(transport.Requests);
    }

    // ---- the reads (spec FR-003, I-3) ---------------------------------------------------------------------------

    [Fact]
    public async Task TheClassroomRead_IsOneGetOfAtMostOneCourse_WithTheToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedHttpHandler((_, _) => Task.FromResult(ScriptedHttpHandler.JsonResponse(HttpStatusCode.OK, "{}")));

        var outcome = await ProbeWith(transport).ReadCoursesAsync(new DelegatedToken("ya29.synthetic-access-token"), ct);

        Assert.Equal(AccessCheckStepOutcome.Succeeded, outcome);
        var request = Assert.Single(transport.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("classroom.googleapis.com", request.Uri!.Host);
        Assert.Equal("/v1/courses", request.Uri.AbsolutePath);
        Assert.Contains("pageSize=1", request.Uri.Query, StringComparison.Ordinal);
        Assert.Equal("Bearer ya29.synthetic-access-token", request.Headers["Authorization"]);
    }

    [Fact]
    public async Task TheReportsRead_IsOneGetOfAtMostOneMeetEvent_ForAllUsers()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedHttpHandler((_, _) => Task.FromResult(ScriptedHttpHandler.JsonResponse(HttpStatusCode.OK, "{}")));

        var outcome = await ProbeWith(transport).ReadMeetActivityAsync(new DelegatedToken("ya29.synthetic-access-token"), ct);

        Assert.Equal(AccessCheckStepOutcome.Succeeded, outcome);
        var request = Assert.Single(transport.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("admin.googleapis.com", request.Uri!.Host);
        Assert.Equal("/admin/reports/v1/activity/users/all/applications/meet", request.Uri.AbsolutePath);
        Assert.Contains("maxResults=1", request.Uri.Query, StringComparison.Ordinal);
        Assert.Equal("Bearer ya29.synthetic-access-token", request.Headers["Authorization"]);
    }

    /// <summary>AC-003: a read that returns data is success too — and the data is only inspected for success.</summary>
    [Fact]
    public async Task AReadThatReturnsData_IsSucceeded()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedHttpHandler((_, _) => Task.FromResult(ScriptedHttpHandler.JsonResponse(
            HttpStatusCode.OK,
            """{"courses":[{"id":"600000000001","name":"Algebra 7-A","ownerId":"1"}],"nextPageToken":"abc"}""")));

        var outcome = await ProbeWith(transport).ReadCoursesAsync(new DelegatedToken("ya29.synthetic-access-token"), ct);

        Assert.Equal(AccessCheckStepOutcome.Succeeded, outcome);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task APermissionDeniedRead_IsTechnicalAccountCannotRead()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedHttpHandler((_, _) => Task.FromResult(ApiError(
            HttpStatusCode.Forbidden,
            "Not Authorized to access this resource/api",
            "forbidden",
            "PERMISSION_DENIED")));

        var outcome = await ProbeWith(transport).ReadMeetActivityAsync(new DelegatedToken("ya29.synthetic-access-token"), ct);

        Assert.Equal(AccessCheckStepOutcome.TechnicalAccountCannotRead, outcome);
        Assert.Single(transport.Requests);
    }

    [Theory]
    [InlineData("SERVICE_DISABLED")]
    [InlineData("accessNotConfigured")]
    public async Task AReadAgainstADisabledApi_IsApiNotEnabled(string reason)
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedHttpHandler((_, _) => Task.FromResult(ApiError(
            HttpStatusCode.Forbidden,
            "Google Classroom API has not been used in project 1 before or it is disabled.",
            reason,
            "PERMISSION_DENIED")));

        var outcome = await ProbeWith(transport).ReadCoursesAsync(new DelegatedToken("ya29.synthetic-access-token"), ct);

        Assert.Equal(AccessCheckStepOutcome.ApiNotEnabled, outcome);
    }

    [Theory]
    [MemberData(nameof(Unavailable))]
    public async Task AServerErrorOrThrottling_OnARead_IsGoogleUnavailable_AndNotRetried(HttpStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedHttpHandler((_, _) => Task.FromResult(ScriptedHttpHandler.EmptyResponse(status)));

        var outcome = await ProbeWith(transport).ReadCoursesAsync(new DelegatedToken("ya29.synthetic-access-token"), ct);

        Assert.Equal(AccessCheckStepOutcome.GoogleUnavailable, outcome);
        Assert.Single(transport.Requests);
    }

    // ---- where requests go (SC-13, S-06, S-12) -----------------------------------------------------------------

    /// <summary>Every request goes to Google; the reads are GETs and the only POST is the token request.</summary>
    [Fact]
    public async Task AFullSequence_OnlyReachesGoogle_AndWritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedHttpHandler((request, _) => Task.FromResult(
            request.RequestUri!.Host == "oauth2.googleapis.com"
                ? TokenIssued()
                : ScriptedHttpHandler.JsonResponse(HttpStatusCode.OK, "{}")));
        var probe = ProbeWith(transport);

        foreach (var scope in AccessCheckTestData.Scopes)
        {
            var attempt = await probe.RequestDelegatedTokenAsync(TechnicalAccount, scope, ct);
            Assert.Equal(AccessCheckStepOutcome.Succeeded, attempt.Outcome);
        }

        var token = new DelegatedToken("ya29.synthetic-access-token");
        Assert.Equal(AccessCheckStepOutcome.Succeeded, await probe.ReadCoursesAsync(token, ct));
        Assert.Equal(AccessCheckStepOutcome.Succeeded, await probe.ReadMeetActivityAsync(token, ct));

        Assert.Equal(8, transport.Requests.Count);
        Assert.All(transport.Requests, r => Assert.Contains(r.Uri!.Host, GoogleHosts));
        Assert.All(
            transport.Requests.Where(r => r.Method != HttpMethod.Get),
            r => Assert.Equal("oauth2.googleapis.com", r.Uri!.Host));
        Assert.Equal(
            AccessCheckTestData.Scopes,
            transport.Requests.Where(r => r.Method == HttpMethod.Post).Select(r => ClaimsOf(r).GetProperty("scope").GetString()));
    }
}
