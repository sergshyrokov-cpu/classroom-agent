using System.Net;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The US-040 header check (spec VR-001): a response satisfies the rule when its <c>Cache-Control</c> value, split on
/// commas and trimmed, contains the directive <c>no-store</c>. Other directives beside it are allowed (spec FR-006).
/// </summary>
public static class NoStore
{
    private static readonly string[] SkippedMethods = ["HEAD", "OPTIONS"];

    public static bool IsIn(string? cacheControl) =>
        cacheControl is not null
        && cacheControl.Split(',').Any(d => string.Equals(d.Trim(), "no-store", StringComparison.OrdinalIgnoreCase));

    /// <summary>The <c>Cache-Control</c> value of a page response, every occurrence joined; null when absent.</summary>
    public static string? Of(PageResponse response)
    {
        var values = response.Headers
            .Where(h => string.Equals(h.Key, "Cache-Control", StringComparison.OrdinalIgnoreCase))
            .Select(h => h.Value)
            .ToList();
        return values.Count == 0 ? null : string.Join(", ", values);
    }

    public static string? Of(InstallationTestHost.RawResponse response) =>
        response.Headers.TryGetValue("Cache-Control", out var value) ? value : null;

    public static void Assert(PageResponse response, string what) =>
        Xunit.Assert.True(IsIn(Of(response)), $"{what} → {(int)response.Status}: Cache-Control is '{Of(response) ?? "(absent)"}'.");

    public static void Assert(InstallationTestHost.RawResponse response, string what) =>
        Xunit.Assert.True(IsIn(Of(response)), $"{what} → {(int)response.Status}: Cache-Control is '{Of(response) ?? "(absent)"}'.");

    /// <summary>
    /// The methods to send to an endpoint (spec VR-004): each method it declares except HEAD and OPTIONS, or GET and
    /// POST for an endpoint that accepts any method (a Razor page, the fallback).
    /// </summary>
    public static IReadOnlyList<string> MethodsOf(HostEndpoint endpoint) =>
        endpoint.Methods is null
            ? ["GET", "POST"]
            : endpoint.Methods.Where(m => !SkippedMethods.Contains(m, StringComparer.OrdinalIgnoreCase)).ToList();

    /// <summary>Sends every method of every endpoint and lists each response without <c>no-store</c>.</summary>
    public static async Task<List<string>> MissingAsync(
        IEnumerable<HostEndpoint> endpoints,
        Func<string, string, Task<(HttpStatusCode Status, string? CacheControl)>> send)
    {
        var missing = new List<string>();
        foreach (var endpoint in endpoints)
        {
            foreach (var method in MethodsOf(endpoint))
            {
                var (status, cacheControl) = await send(method, endpoint.SamplePath);
                if (!IsIn(cacheControl))
                {
                    missing.Add($"{method} {endpoint.SamplePath} → {(int)status}: '{cacheControl ?? "(absent)"}'");
                }
            }
        }

        return missing;
    }

    /// <summary>The URL paths of every file in a host's <c>wwwroot</c> (spec VR-003).</summary>
    public static IReadOnlyList<string> StaticFilesOf(string project)
    {
        var webRoot = Path.Combine(StaticFiles.RepositoryRoot(), "src", project, "wwwroot");
        var files = Directory.EnumerateFiles(webRoot, "*", SearchOption.AllDirectories)
            .Select(f => "/" + Path.GetRelativePath(webRoot, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
        Xunit.Assert.NotEmpty(files);
        return files;
    }
}
