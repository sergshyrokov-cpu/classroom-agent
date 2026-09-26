using System.Net;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using Google;
using Google.Apis.Admin.Reports.reports_v1;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Classroom.v1;
using Google.Apis.Http;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClassroomAgent.Infrastructure.Google;

/// <summary>
/// The Google implementation of <see cref="IGoogleAccessProbe"/> (US-011 spec FR-002…FR-005, FR-016): the only code
/// that references the Google packages (OD-001, AD-4). Every answer is mapped onto the closed list of spec FR-005;
/// an answer it cannot classify is <see cref="AccessCheckStepOutcome.GoogleUnavailable"/>, never a diagnosis.
/// </summary>
/// <remarks>
/// <para>Every request goes through <paramref name="transport"/>, so nothing but Google is reachable from here (SC-13)
/// and the tests drive it offline (TC-4). The client library's own retries are switched off: a check never retries
/// within itself (spec FR-005, S-09).</para>
/// <para>The key is resolved from the secret store for every request and held only for it (spec FR-016); nothing about
/// it, no token and no Google error text is logged or returned (SC-7, SC-10).</para>
/// </remarks>
public sealed partial class GoogleAccessProbe : IGoogleAccessProbe
{
    private const string ApplicationName = "classroom-agent";

    private readonly ISecretStore _secretStore;
    private readonly GoogleServiceAccountSettings _settings;
    private readonly TransportFactory _httpClients;
    private readonly ILogger<GoogleAccessProbe> _logger;

    public GoogleAccessProbe(
        ISecretStore secretStore,
        GoogleServiceAccountSettings settings,
        HttpMessageHandler transport,
        ILogger<GoogleAccessProbe>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(secretStore);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(transport);
        _secretStore = secretStore;
        _settings = settings;
        _httpClients = new TransportFactory(transport);
        _logger = logger ?? NullLogger<GoogleAccessProbe>.Instance;
    }

    public async Task<DelegationAttempt> RequestDelegatedTokenAsync(
        string technicalAccount,
        string scope,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(technicalAccount);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        var key = LoadKey();
        if (key is null)
        {
            return new DelegationAttempt(AccessCheckStepOutcome.KeyUnavailable, null);
        }

        // Spec FR-002: one scope per request, the technical account as the impersonated subject (BR-015, SC-8).
        var credential = new ServiceAccountCredential(new ServiceAccountCredential.Initializer(key.Id, key.TokenServerUrl)
        {
            Key = key.Key,
            KeyId = key.KeyId,
            User = technicalAccount,
            Scopes = [scope],
            HttpClientFactory = _httpClients,
            DefaultExponentialBackOffPolicy = ExponentialBackOffPolicy.None,
        });

        try
        {
            var token = await credential.GetAccessTokenForRequestAsync(authUri: null, cancellationToken);
            return string.IsNullOrWhiteSpace(token)
                ? Unclassified(AccessCheckStepKind.Delegation, null)
                : new DelegationAttempt(AccessCheckStepOutcome.Succeeded, new DelegatedToken(token));
        }
        catch (TokenResponseException refusal)
        {
            var outcome = ClassifyTokenError(refusal);
            if (outcome == AccessCheckStepOutcome.GoogleUnavailable && !IsUnavailable(refusal.StatusCode))
            {
                LogUnclassified(_logger, AccessCheckStepKind.Delegation, (int?)refusal.StatusCode);
            }

            return new DelegationAttempt(outcome, null);
        }
        catch (HttpRequestException)
        {
            return new DelegationAttempt(AccessCheckStepOutcome.GoogleUnavailable, null);
        }
    }

    public Task<AccessCheckStepOutcome> ReadCoursesAsync(DelegatedToken token, CancellationToken cancellationToken) =>
        ReadAsync(
            AccessCheckStepKind.ClassroomRead,
            async initializer =>
            {
                using var service = new ClassroomService(initializer);
                var request = service.Courses.List();
                request.PageSize = 1;
                await request.ExecuteAsync(cancellationToken);
            },
            token);

    public Task<AccessCheckStepOutcome> ReadMeetActivityAsync(DelegatedToken token, CancellationToken cancellationToken) =>
        ReadAsync(
            AccessCheckStepKind.ReportsRead,
            async initializer =>
            {
                using var service = new ReportsService(initializer);
                var request = service.Activities.List("all", ActivitiesResource.ListRequest.ApplicationNameEnum.Meet);
                request.MaxResults = 1;
                await request.ExecuteAsync(cancellationToken);
            },
            token);

    /// <summary>
    /// Spec FR-003: one GET, the answer inspected for success only — nothing from it is copied anywhere (S-08).
    /// </summary>
    private async Task<AccessCheckStepOutcome> ReadAsync(
        AccessCheckStepKind kind,
        Func<BaseClientService.Initializer, Task> read,
        DelegatedToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        var initializer = new BaseClientService.Initializer
        {
            ApplicationName = ApplicationName,
            HttpClientFactory = _httpClients,
            HttpClientInitializer = new BearerToken(token.Value),
            DefaultExponentialBackOffPolicy = ExponentialBackOffPolicy.None,
            GZipEnabled = false,
        };

        try
        {
            await read(initializer);
            return AccessCheckStepOutcome.Succeeded;
        }
        catch (GoogleApiException refusal)
        {
            var outcome = ClassifyApiError(refusal);
            if (outcome == AccessCheckStepOutcome.GoogleUnavailable && !IsUnavailable(refusal.HttpStatusCode))
            {
                LogUnclassified(_logger, kind, (int)refusal.HttpStatusCode);
            }

            return outcome;
        }
        catch (HttpRequestException)
        {
            return AccessCheckStepOutcome.GoogleUnavailable;
        }
    }

    /// <summary>
    /// Spec VR-004: a trimmed, non-empty reference that the store resolves to a service-account key in Google's JSON
    /// format with a private key and a client email. Anything else is <see cref="AccessCheckStepOutcome.KeyUnavailable"/>
    /// and no request is sent.
    /// </summary>
    private ServiceAccountCredential? LoadKey()
    {
        var reference = _settings.KeyReference?.Trim();
        if (string.IsNullOrEmpty(reference) || _secretStore.Resolve(reference) is not { Length: > 0 } json)
        {
            return null;
        }

        try
        {
            var credential = CredentialFactory.FromJson<ServiceAccountCredential>(json);
            return string.IsNullOrWhiteSpace(credential.Id) || credential.Key is null ? null : credential;
        }
        catch (Exception invalid) when (invalid is InvalidOperationException
            or ArgumentException
            or FormatException
            or Newtonsoft.Json.JsonException
            or System.Security.Cryptography.CryptographicException
            or NullReferenceException)
        {
            // The content is not echoed, not even its kind of defect (VR-004, SC-7).
            return null;
        }
    }

    /// <summary>Spec FR-005, token endpoint: Google's OAuth error codes and the descriptions it documents for them.</summary>
    private static AccessCheckStepOutcome ClassifyTokenError(TokenResponseException refusal)
    {
        if (IsUnavailable(refusal.StatusCode))
        {
            return AccessCheckStepOutcome.GoogleUnavailable;
        }

        var error = refusal.Error?.Error ?? string.Empty;
        var description = refusal.Error?.ErrorDescription ?? string.Empty;
        return error switch
        {
            "unauthorized_client" or "access_denied" => AccessCheckStepOutcome.ScopeNotAuthorized,
            "invalid_grant" when description.Contains("Invalid JWT Signature", StringComparison.OrdinalIgnoreCase) =>
                AccessCheckStepOutcome.KeyRejected,
            "invalid_grant" when description.Contains("Invalid email or User ID", StringComparison.OrdinalIgnoreCase)
                || description.Contains("invalid_user", StringComparison.OrdinalIgnoreCase) =>
                AccessCheckStepOutcome.TechnicalAccountUnknown,
            "invalid_client" => AccessCheckStepOutcome.KeyRejected,
            _ => AccessCheckStepOutcome.GoogleUnavailable,
        };
    }

    /// <summary>Spec FR-005, API reads: a disabled API is the Owner's; any other 403 is the account's rights.</summary>
    private static AccessCheckStepOutcome ClassifyApiError(GoogleApiException refusal)
    {
        if (IsUnavailable(refusal.HttpStatusCode))
        {
            return AccessCheckStepOutcome.GoogleUnavailable;
        }

        if (refusal.HttpStatusCode != HttpStatusCode.Forbidden)
        {
            return AccessCheckStepOutcome.GoogleUnavailable;
        }

        var reasons = (refusal.Error?.Errors ?? []).Select(e => e.Reason ?? string.Empty).ToList();
        var content = refusal.Error?.ErrorResponseContent ?? string.Empty;
        var disabled = reasons.Contains("accessNotConfigured", StringComparer.Ordinal)
            || reasons.Contains("SERVICE_DISABLED", StringComparer.Ordinal)
            || content.Contains("\"SERVICE_DISABLED\"", StringComparison.Ordinal)
            || content.Contains("\"accessNotConfigured\"", StringComparison.Ordinal);
        return disabled ? AccessCheckStepOutcome.ApiNotEnabled : AccessCheckStepOutcome.TechnicalAccountCannotRead;
    }

    private static bool IsUnavailable(HttpStatusCode? status) =>
        status is HttpStatusCode.TooManyRequests || (status is { } code && (int)code >= 500);

    private DelegationAttempt Unclassified(AccessCheckStepKind kind, int? status)
    {
        LogUnclassified(_logger, kind, status);
        return new DelegationAttempt(AccessCheckStepOutcome.GoogleUnavailable, null);
    }

    [LoggerMessage(
        EventId = 2410,
        EventName = "GoogleAnswerUnclassified",
        Level = LogLevel.Error,
        Message = "A Google answer to the {Step} step could not be classified (HTTP status {Status}); reported as unavailable")]
    private static partial void LogUnclassified(ILogger logger, AccessCheckStepKind step, int? status);

    /// <summary>Hands the one configured transport to the Google client library without letting it dispose it.</summary>
    private sealed class TransportFactory(HttpMessageHandler transport) : HttpClientFactory
    {
        protected override HttpMessageHandler CreateHandler(CreateHttpClientArgs args) => new Borrowed(transport);

        /// <summary>
        /// A handler over a shared transport: disposing it leaves the transport alive for the next request. A 5xx or
        /// 429 answer is turned into a transport failure here, before the client library parses it — its token
        /// client fails on an empty error body — so "Google is unavailable" is decided in one place (spec FR-005).
        /// </summary>
        private sealed class Borrowed(HttpMessageHandler inner) : HttpMessageHandler
        {
            private readonly HttpMessageInvoker _invoker = new(inner, disposeHandler: false);

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = await _invoker.SendAsync(request, cancellationToken);
                if (IsUnavailable(response.StatusCode))
                {
                    var status = response.StatusCode;
                    response.Dispose();
                    throw new HttpRequestException("Google is unavailable.", inner: null, status);
                }

                return response;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _invoker.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }

    /// <summary>Puts the delegated token on each read as a bearer header, and nothing else.</summary>
    private sealed class BearerToken(string value) : IConfigurableHttpClientInitializer, IHttpExecuteInterceptor
    {
        public void Initialize(ConfigurableHttpClient httpClient) => httpClient.MessageHandler.AddExecuteInterceptor(this);

        public Task InterceptAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", value);
            return Task.CompletedTask;
        }
    }
}
