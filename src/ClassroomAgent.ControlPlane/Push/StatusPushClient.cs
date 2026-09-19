using System.Net;
using System.Net.Http.Json;
using ClassroomAgent.Contracts;

namespace ClassroomAgent.ControlPlane.Push;

/// <summary>
/// One HTTP attempt against an installation's push receiver (US-006 spec FR-006; api-design §6). Plain HTTP,
/// no cookies, no credentials, no redirects; the attempt is bound to the 10-second timeout on the injected
/// clock, and the response body is never read (S-10).
/// </summary>
public sealed class StatusPushClient(IHttpClientFactory clients, TimeProvider timeProvider) : IStatusPushClient
{
    /// <summary>The name the push HTTP client is registered under; the tests replace its primary handler.</summary>
    public const string HttpClientName = "status-push";

    public async Task<StatusPushAttempt> SendAsync(string address, Guid identifier, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(StatusPushDelivery.AttemptTimeout, timeProvider);
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(address.TrimEnd('/') + "/" + ServiceChannel.StatusPushPath))
        {
            Content = JsonContent.Create(new StatusPushRequest(identifier), options: ServiceChannel.JsonOptions),
        };

        try
        {
            // ResponseHeadersRead: the status is the whole answer; the body is never read into logs or state.
            using var response = await clients
                .CreateClient(HttpClientName)
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, attempt.Token);

            return response.StatusCode switch
            {
                HttpStatusCode.Accepted => new StatusPushAttempt(StatusPushAttemptResult.Delivered),
                HttpStatusCode.NotFound => new StatusPushAttempt(StatusPushAttemptResult.Refused),
                _ => new StatusPushAttempt(StatusPushAttemptResult.UnexpectedStatus, (int)response.StatusCode),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The push itself was replaced or the host is stopping: not an attempt outcome.
            throw;
        }
        catch (OperationCanceledException)
        {
            return new StatusPushAttempt(StatusPushAttemptResult.Timeout);
        }
        catch (HttpRequestException)
        {
            // Connection refused, reset, or a name that does not resolve; the message may name the host (SC-10).
            return new StatusPushAttempt(StatusPushAttemptResult.ConnectionFailed);
        }
    }
}
