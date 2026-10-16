using System.Net;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The US-050 header check (spec FR-003, FR-004, FR-005; VR-001, VR-002): each of the four browser security headers
/// occurs exactly once on a response, with exactly the value the Specification fixes for the host.
/// </summary>
public static class SecurityHeaders
{
    public const string XFrameOptions = "X-Frame-Options";

    public const string XContentTypeOptions = "X-Content-Type-Options";

    public const string ReferrerPolicy = "Referrer-Policy";

    public const string ContentSecurityPolicy = "Content-Security-Policy";

    /// <summary>The origin of the authorization endpoint the installation's Google handler redirects to (spec FR-004, I-2).</summary>
    public const string GoogleSignInOrigin = "https://accounts.google.com";

    /// <summary>Spec FR-004, the installation (both ports), exact.</summary>
    public const string InstallationCsp =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; font-src 'self'; object-src 'none'; "
        + "base-uri 'self'; form-action 'self' https://accounts.google.com; frame-ancestors 'none'";

    /// <summary>Spec FR-004, the Control Plane, exact.</summary>
    public const string ControlPlaneCsp =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; font-src 'self'; object-src 'none'; "
        + "base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    public static IEnumerable<KeyValuePair<string, string>> Of(PageResponse response) => response.Headers;

    public static IEnumerable<KeyValuePair<string, string>> Of(BinaryResponse response) => response.Headers;

    /// <summary>A raw response keeps one entry per header name; several values arrive joined, which never equals the expected value.</summary>
    public static IEnumerable<KeyValuePair<string, string>> Of(InstallationTestHost.RawResponse response) => response.Headers;

    /// <summary>Every value a response carries for a header, case-insensitive name.</summary>
    public static IReadOnlyList<string> ValuesOf(IEnumerable<KeyValuePair<string, string>> headers, string name) =>
        headers.Where(h => string.Equals(h.Key, name, StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).ToList();

    /// <summary>What is wrong with a response's four headers against a host's CSP; empty when the response complies.</summary>
    public static IReadOnlyList<string> Problems(IEnumerable<KeyValuePair<string, string>> headers, string csp)
    {
        var list = headers.ToList();
        var expected = new (string Name, string Value)[]
        {
            (XFrameOptions, "DENY"),
            (XContentTypeOptions, "nosniff"),
            (ReferrerPolicy, "same-origin"),
            (ContentSecurityPolicy, csp),
        };

        var problems = new List<string>();
        foreach (var (name, value) in expected)
        {
            var values = ValuesOf(list, name);
            if (values.Count != 1 || !string.Equals(values[0], value, StringComparison.Ordinal))
            {
                var actual = values.Count == 0 ? "(absent)" : string.Join(" | ", values.Select(v => $"'{v}'"));
                problems.Add($"{name}: expected exactly one '{value}', got {actual}");
            }
        }

        return problems;
    }

    public static void Assert(IEnumerable<KeyValuePair<string, string>> headers, string csp, string what)
    {
        var problems = Problems(headers, csp);
        Xunit.Assert.True(problems.Count == 0, $"{what}: {string.Join("; ", problems)}");
    }

    public static void Assert(PageResponse response, string csp, string what) =>
        Assert(Of(response), csp, $"{what} → {(int)response.Status}");

    public static void Assert(BinaryResponse response, string csp, string what) =>
        Assert(Of(response), csp, $"{what} → {(int)response.Status}");

    public static void Assert(InstallationTestHost.RawResponse response, string csp, string what) =>
        Assert(Of(response), csp, $"{what} → {(int)response.Status}");

    /// <summary>Sends every method of every endpoint (US-040 enumeration, spec VR-004) and lists each non-compliant response.</summary>
    public static async Task<List<string>> MissingAsync(
        IEnumerable<HostEndpoint> endpoints,
        string csp,
        Func<string, string, Task<(HttpStatusCode Status, IEnumerable<KeyValuePair<string, string>> Headers)>> send)
    {
        var missing = new List<string>();
        foreach (var endpoint in endpoints)
        {
            foreach (var method in NoStore.MethodsOf(endpoint))
            {
                var (status, headers) = await send(method, endpoint.SamplePath);
                var problems = Problems(headers, csp);
                if (problems.Count > 0)
                {
                    missing.Add($"{method} {endpoint.SamplePath} → {(int)status}: {string.Join("; ", problems)}");
                }
            }
        }

        return missing;
    }

    /// <summary>
    /// The directives of a CSP value: name → its source list, in order. A directive named twice is reported by
    /// <see cref="DuplicateDirectives"/>.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Directives(string csp)
    {
        var directives = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in Parts(csp))
        {
            directives.TryAdd(part[0], part.Skip(1).ToList());
        }

        return directives;
    }

    public static IReadOnlyList<string> DuplicateDirectives(string csp) =>
        Parts(csp).GroupBy(p => p[0], StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

    private static IEnumerable<string[]> Parts(string csp) =>
        csp.Split(';')
            .Select(d => d.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(p => p.Length > 0);
}
