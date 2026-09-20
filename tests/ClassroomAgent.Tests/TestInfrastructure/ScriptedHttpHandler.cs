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

    /// <summary>
    /// Every request this transport carried. The installation's one typed client serves the whole service channel,
    /// so a legitimacy check of the background service is recorded here too; assert on
    /// <see cref="AdminLoginCheckRequests"/> when the subject is the sign-in.
    /// </summary>
    public IReadOnlyList<RecordedRequest> AdminLoginCheckRequests =>
        Requests.Where(r => r.Uri?.AbsolutePath.EndsWith(AdminLoginCheckTestData.Path, StringComparison.Ordinal) == true)
            .ToList();

    /// <summary>
    /// Scripts the Admin login check only, and answers everything else — the background legitimacy check — with a
    /// bare <c>503</c>, which that client classifies as an error answer and which changes nothing it has not already
    /// seen. Without this the two calls would share one script and consume each other's answers.
    /// </summary>
    public static ScriptedHttpHandler AdminLoginCheck(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        ArgumentNullException.ThrowIfNull(respond);
        return new ScriptedHttpHandler((request, _) =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith(AdminLoginCheckTestData.Path, StringComparison.Ordinal) != true)
            {
                return Task.FromResult(EmptyResponse(System.Net.HttpStatusCode.ServiceUnavailable));
            }

            try
            {
                return Task.FromResult(respond(request));
            }
            catch (HttpRequestException failure)
            {
                // A scripted connection failure of the channel, raised where the transport would raise it.
                return Task.FromException<HttpResponseMessage>(failure);
            }
        });
    }

    /// <summary>Answers every Admin login check with that status and JSON body; other paths get a bare 503.</summary>
    public static ScriptedHttpHandler AdminLoginCheckJson(System.Net.HttpStatusCode status, string body) =>
        AdminLoginCheck(_ => JsonResponse(status, body));

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
