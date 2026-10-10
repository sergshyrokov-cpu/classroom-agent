using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;
using Google;
using Google.Apis.Admin.Reports.reports_v1;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Http;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using GoogleActivity = Google.Apis.Admin.Reports.reports_v1.Data.Activity;

namespace ClassroomAgent.Infrastructure.Google;

/// <summary>
/// The Google implementation of <see cref="IMeetReportsReader"/> (US-031 spec FR-003, FR-004; entity model §7): reads
/// <c>activities.list</c> of Admin Reports for user <c>all</c>, application <c>meet</c>, event <c>call_ended</c>, over
/// the requested window, following every page, as the school's technical account (BR-015) with the
/// <c>admin.reports.audit.readonly</c> scope only — nothing is written to Google (SC-8). The only place the Reports SDK
/// types of Meet events exist (AD-4).
/// </summary>
/// <remarks>
/// Every request goes through <see cref="GoogleRetryHandler"/> and the US-017 failure classification, as
/// <see cref="GoogleClassroomReader"/>'s do; a retried request repeats the same page token. Of each event only the
/// values spec FR-003 lists leave this class — no telemetry, device, location, display name, IP or
/// <c>is_external</c> (AC-013). The parameter names are mapped in <see cref="Map"/> alone, so the live check of
/// <c>trebovaniya.md</c> §7 item 28 (OD-009) changes one method. The key is resolved per call and never kept, logged or
/// returned (SC-7, SC-10).
/// </remarks>
public sealed partial class GoogleMeetReportsReader : IMeetReportsReader
{
    private const string ApplicationName = "classroom-agent";

    private const string AllUsers = "all";

    private const string CallEnded = "call_ended";

    /// <summary>The largest page <c>activities.list</c> serves; it bounds one page's transaction (db-design §7).</summary>
    private const int PageSize = 1000;

    private static readonly string[] Scopes = [GoogleDelegationScopes.AdminReportsAuditReadonly];

    private readonly ISecretStore _secretStore;
    private readonly GoogleServiceAccountSettings _settings;
    private readonly HttpMessageHandler _transport;
    private readonly TimeProvider _timeProvider;
    private readonly IGoogleRetryJitter _jitter;
    private readonly ILogger<GoogleMeetReportsReader> _logger;
    private readonly TimeSpan? _attemptTimeout;

    /// <summary>The same seams as <see cref="GoogleClassroomReader"/>: key store, transport, clock, jitter, logger.</summary>
    public GoogleMeetReportsReader(
        ISecretStore secretStore,
        GoogleServiceAccountSettings settings,
        HttpMessageHandler transport,
        TimeProvider timeProvider,
        IGoogleRetryJitter jitter,
        ILogger<GoogleMeetReportsReader> logger,
        TimeSpan? attemptTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(secretStore);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(jitter);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(attemptTimeout ?? TimeSpan.FromTicks(1), TimeSpan.Zero);
        _secretStore = secretStore;
        _settings = settings;
        _transport = transport;
        _timeProvider = timeProvider;
        _jitter = jitter;
        _logger = logger;
        _attemptTimeout = attemptTimeout;
    }

    public async IAsyncEnumerable<MeetEventPage> ReadCallEndedAsync(
        string impersonationUser,
        DateTimeOffset from,
        DateTimeOffset to,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(impersonationUser);

        using var service = CreateService(impersonationUser);
        string? pageToken = null;
        do
        {
            var request = service.Activities.List(AllUsers, ActivitiesResource.ListRequest.ApplicationNameEnum.Meet);
            request.EventName = CallEnded;
            request.StartTime = Rfc3339(from);
            request.EndTime = Rfc3339(to);
            request.MaxResults = PageSize;
            request.PageToken = pageToken;
            var page = await GuardAsync(() => request.ExecuteAsync(cancellationToken), cancellationToken);

            var events = new List<MeetCallEndedEvent>();
            var unreadable = 0;
            foreach (var activity in page.Items ?? [])
            {
                var mapped = Map(activity);
                if (mapped.Count == 0)
                {
                    unreadable++;
                }

                events.AddRange(mapped);
            }

            yield return new MeetEventPage(events, unreadable);
            pageToken = page.NextPageToken;
        }
        while (!string.IsNullOrEmpty(pageToken));
    }

    /// <summary>
    /// The single mapping from <c>call_ended</c> parameters to the values of spec FR-003 (OD-009). A missing parameter
    /// is an absent value, judged by the use case (VR-001); an activity with no <c>call_ended</c> event in it maps to
    /// nothing and is counted as unreadable by the caller.
    /// </summary>
    private static List<MeetCallEndedEvent> Map(GoogleActivity activity)
    {
        var occurredAt = activity.Id?.TimeDateTimeOffset;
        var mapped = new List<MeetCallEndedEvent>();
        foreach (var call in (activity.Events ?? []).Where(e => e.Name == CallEnded))
        {
            var parameters = (call.Parameters ?? [])
                .Where(p => p.Name is not null)
                .GroupBy(p => p.Name, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            string? Text(string name) => parameters.TryGetValue(name, out var parameter) ? parameter.Value : null;

            long? Number(string name) =>
                !parameters.TryGetValue(name, out var parameter) ? null
                : parameter.IntValue is { } value ? value
                : long.TryParse(parameter.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed
                : null;

            mapped.Add(new MeetCallEndedEvent(
                occurredAt,
                Text("conference_id"),
                Text("meeting_code"),
                Text("organizer_email"),
                Text("endpoint_id"),
                Text("identifier"),
                Text("identifier_type"),
                Number("duration_seconds")));
        }

        return mapped;
    }

    private static string Rfc3339(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    private ReportsService CreateService(string impersonationUser)
    {
        var httpClients = new TransportFactory(
            new GoogleRetryHandler(_transport, _timeProvider, _jitter, LogRetry, _attemptTimeout));
        return new ReportsService(new BaseClientService.Initializer
        {
            ApplicationName = ApplicationName,
            HttpClientFactory = httpClients,
            HttpClientInitializer = Credential(impersonationUser, httpClients),
            DefaultExponentialBackOffPolicy = ExponentialBackOffPolicy.None,
            GZipEnabled = false,

            // US-017 spec FR-002: the retry sequence, pauses included, must outlast the library's 100 s default; each
            // attempt is bounded by GoogleRetryHandler instead.
            HttpClientTimeout = Timeout.InfiniteTimeSpan,
        });
    }

    private void LogRetry(int attempt, TimeSpan pause, int? status) =>
        LogRetryAttempt(_logger, attempt, pause.TotalSeconds, status);

    [LoggerMessage(
        EventId = 2420,
        EventName = "SyncGoogleRetry",
        Level = LogLevel.Warning,
        Message = "A Google request failed on attempt {Attempt} (HTTP status {Status}); retrying after {PauseSeconds} seconds")]
    private static partial void LogRetryAttempt(ILogger logger, int attempt, double pauseSeconds, int? status);

    /// <summary>
    /// Runs one Google request and turns anything that fails into the closed list of US-017 spec FR-001; there is no
    /// "course gone" class in the Meet step (US-031 spec FR-010). No exception text, reason or body is carried on.
    /// </summary>
    private static async Task<T> GuardAsync<T>(Func<Task<T>> read, CancellationToken cancellationToken)
    {
        try
        {
            return await read();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (GoogleReadFailedException)
        {
            throw;
        }
        catch (TokenResponseException refusal)
        {
            throw FromOutcome(GoogleFailureClassifier.ClassifyTokenError(refusal), refusal.StatusCode);
        }
        catch (GoogleApiException refusal)
        {
            if (GoogleFailureClassifier.IsUnavailable(refusal.HttpStatusCode))
            {
                throw Transient();
            }

            throw refusal.HttpStatusCode == HttpStatusCode.Forbidden
                ? FromOutcome(GoogleFailureClassifier.ClassifyApiError(refusal), refusal.HttpStatusCode)
                : Unexpected();
        }
        catch (Exception failure) when (failure is HttpRequestException or OperationCanceledException)
        {
            throw Transient();
        }
        catch (Exception)
        {
            throw Unexpected();
        }
    }

    private static GoogleReadFailedException FromOutcome(AccessCheckStepOutcome outcome, HttpStatusCode? status) =>
        outcome switch
        {
            AccessCheckStepOutcome.ScopeNotAuthorized => Configuration(SyncDiagnosis.ScopeNotAuthorized),
            AccessCheckStepOutcome.TechnicalAccountUnknown => Configuration(SyncDiagnosis.TechnicalAccountUnknown),
            AccessCheckStepOutcome.TechnicalAccountCannotRead => Configuration(SyncDiagnosis.TechnicalAccountCannotRead),
            AccessCheckStepOutcome.ApiNotEnabled => Configuration(SyncDiagnosis.ApiNotEnabled),
            AccessCheckStepOutcome.KeyUnavailable => Configuration(SyncDiagnosis.KeyUnavailable),
            AccessCheckStepOutcome.KeyRejected => Configuration(SyncDiagnosis.KeyRejected),
            _ => GoogleFailureClassifier.IsUnavailable(status) ? Transient() : Unexpected(),
        };

    private static GoogleReadFailedException Configuration(SyncDiagnosis diagnosis) =>
        new(GoogleReadFailureKind.Configuration, diagnosis);

    private static GoogleReadFailedException Transient() =>
        new(GoogleReadFailureKind.Transient, SyncDiagnosis.GoogleUnavailable);

    private static GoogleReadFailedException Unexpected() =>
        new(GoogleReadFailureKind.Unexpected, SyncDiagnosis.Unexpected);

    /// <summary>
    /// The delegated credential of one call: the technical account as the subject (BR-015), the reports scope only, no
    /// retry of its own. A missing or unusable key ends the call before any request is sent.
    /// </summary>
    private ServiceAccountCredential Credential(string impersonationUser, IHttpClientFactory httpClients)
    {
        var key = LoadKey()
            ?? throw Configuration(SyncDiagnosis.KeyUnavailable);

        return new ServiceAccountCredential(new ServiceAccountCredential.Initializer(key.Id, key.TokenServerUrl)
        {
            Key = key.Key,
            KeyId = key.KeyId,
            User = impersonationUser,
            Scopes = Scopes,
            HttpClientFactory = httpClients,
            DefaultExponentialBackOffPolicy = ExponentialBackOffPolicy.None,
        });
    }

    /// <summary>The key the Owner placed at deployment, resolved per call and never stored, logged or returned (SC-7).</summary>
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
            return null;
        }
    }

    /// <summary>
    /// Hands the retrying transport of one call to the Google client library without letting it dispose the shared
    /// transport underneath (SC-13).
    /// </summary>
    private sealed class TransportFactory(HttpMessageHandler transport) : IHttpClientFactory
    {
        private readonly Handlers _handlers = new(transport);

        public ConfigurableHttpClient CreateHttpClient(CreateHttpClientArgs args)
        {
            var client = _handlers.CreateHttpClient(args);
            client.Timeout = Timeout.InfiniteTimeSpan;
            return client;
        }

        private sealed class Handlers(HttpMessageHandler transport) : HttpClientFactory
        {
            protected override HttpMessageHandler CreateHandler(CreateHttpClientArgs args) => new Borrowed(transport);
        }

        private sealed class Borrowed(HttpMessageHandler inner) : HttpMessageHandler
        {
            private readonly HttpMessageInvoker _invoker = new(inner, disposeHandler: false);

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken) =>
                _invoker.SendAsync(request, cancellationToken);

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
}
