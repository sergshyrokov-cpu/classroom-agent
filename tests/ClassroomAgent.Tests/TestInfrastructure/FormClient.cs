using System.Globalization;
using System.Text.RegularExpressions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// A browser-like client over the in-process host: keeps cookies itself (so tests can
/// read, copy and replay them), remembers the antiforgery token of the last page and
/// never follows redirects.
/// </summary>
public sealed partial class FormClient(HttpClient http) : IDisposable
{
    private readonly Dictionary<string, string> _cookies = new(StringComparer.Ordinal);

    public string? LastToken { get; private set; }

    public IReadOnlyDictionary<string, string> Cookies => new Dictionary<string, string>(_cookies, StringComparer.Ordinal);

    public void ReplaceCookies(IReadOnlyDictionary<string, string> cookies)
    {
        _cookies.Clear();
        foreach (var (name, value) in cookies)
        {
            _cookies[name] = value;
        }
    }

    public Task<PageResponse> GetAsync(
        string path,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? headers = null) =>
        SendAsync(HttpMethod.Get, path, null, cancellationToken, headers);

    public Task<PageResponse> PostFormAsync(
        string path,
        IEnumerable<KeyValuePair<string, string>> fields,
        CancellationToken cancellationToken,
        bool withToken = true,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        var body = fields.ToList();
        if (withToken)
        {
            var token = LastToken
                ?? throw new InvalidOperationException("The last page carried no antiforgery token field.");
            body.Add(new(Html.AntiforgeryFieldName, token));
        }

        return SendAsync(HttpMethod.Post, path, new FormUrlEncodedContent(body), cancellationToken, headers);
    }

    public async Task<PageResponse> SendAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        if (_cookies.Count > 0)
        {
            request.Headers.Add("Cookie", string.Join("; ", _cookies.Select(c => $"{c.Key}={c.Value}")));
        }

        foreach (var (name, value) in headers ?? new Dictionary<string, string>())
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        using var response = await http.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var setCookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToList() : [];
        foreach (var setCookie in setCookies)
        {
            ApplySetCookie(setCookie);
        }

        var token = Html.InputValue(responseBody, Html.AntiforgeryFieldName);
        if (!string.IsNullOrEmpty(token))
        {
            LastToken = token;
        }

        var allHeaders = response.Headers.Concat(response.Content.Headers)
            .SelectMany(h => h.Value.Select(v => new KeyValuePair<string, string>(h.Key, v)))
            .ToList();

        return new PageResponse(
            response.StatusCode,
            response.Headers.Location?.OriginalString,
            responseBody,
            setCookies,
            allHeaders);
    }

    public void Dispose() => http.Dispose();

    private void ApplySetCookie(string setCookie)
    {
        var parts = setCookie.Split(';', StringSplitOptions.TrimEntries);
        var separator = parts[0].IndexOf('=', StringComparison.Ordinal);
        if (separator <= 0)
        {
            return;
        }

        var name = parts[0][..separator];
        var value = parts[0][(separator + 1)..];
        if (value.Length == 0 || parts.Skip(1).Any(IsPastExpiry))
        {
            _cookies.Remove(name);
        }
        else
        {
            _cookies[name] = value;
        }
    }

    private static bool IsPastExpiry(string attribute)
    {
        var maxAge = MaxAge().Match(attribute);
        if (maxAge.Success)
        {
            return int.Parse(maxAge.Groups[1].Value, CultureInfo.InvariantCulture) <= 0;
        }

        const string expiresPrefix = "expires=";
        return attribute.StartsWith(expiresPrefix, StringComparison.OrdinalIgnoreCase)
            && DateTimeOffset.TryParse(
                attribute[expiresPrefix.Length..],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var expires)
            && expires < new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }

    [GeneratedRegex("^max-age=(-?\\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex MaxAge();
}
