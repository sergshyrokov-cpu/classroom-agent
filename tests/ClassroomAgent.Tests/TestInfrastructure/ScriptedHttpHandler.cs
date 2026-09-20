namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that answers from a script and records every request
/// (US-008 test strategy 2.2). It is the single seam through which a test controls both outbound
/// destinations of the installation — the Control Plane channel and the Google backchannel — so no
/// automated test reaches a network (TC-4, SC-13).
/// </summary>
public sealed class ScriptedHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    : HttpMessageHandler
{
    private readonly Lock _gate = new();
    private readonly List<RecordedRequest> _requests = [];

    public IReadOnlyList<RecordedRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return _requests.ToList();
            }
        }
    }

    /// <summary>Answers every request with that status and JSON body.</summary>
    public static ScriptedHttpHandler Json(System.Net.HttpStatusCode status, string body) =>
        new((_, _) => Task.FromResult(JsonResponse(status, body)));

    /// <summary>Answers each request from the queue in turn; a request past the end fails the test.</summary>
    public static ScriptedHttpHandler Sequence(params Func<HttpRequestMessage, HttpResponseMessage>[] answers)
    {
        var index = 0;
        return new ScriptedHttpHandler((request, _) =>
        {
            var current = Interlocked.Increment(ref index) - 1;
            Assert.True(current < answers.Length, $"No scripted answer for request {current + 1} ({request.RequestUri}).");
            return Task.FromResult(answers[current](request));
        });
    }

    public static HttpResponseMessage JsonResponse(System.Net.HttpStatusCode status, string body) =>
        new(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };

    /// <summary>A body with no content type at all — the "404 without the outcome body" row of api-design 2.4.</summary>
    public static HttpResponseMessage EmptyResponse(System.Net.HttpStatusCode status) => new(status);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var recorded = new RecordedRequest(
            request.Method,
            request.RequestUri,
            request.Content?.Headers.ContentType?.ToString() ?? string.Empty,
            body,
            request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase));
        lock (_gate)
        {
            _requests.Add(recorded);
        }

        return await respond(request, cancellationToken);
    }

    public sealed record RecordedRequest(
        HttpMethod Method,
        Uri? Uri,
        string ContentType,
        string Body,
        IReadOnlyDictionary<string, string> Headers);
}
