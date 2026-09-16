using System.Net;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>A captured response of a Control Plane page.</summary>
public sealed record PageResponse(
    HttpStatusCode Status,
    string? Location,
    string Body,
    IReadOnlyList<string> SetCookies,
    IReadOnlyList<KeyValuePair<string, string>> Headers)
{
    /// <summary>The body with HTML character references decoded, for text assertions.</summary>
    public string Text => WebUtility.HtmlDecode(Body);

    /// <summary>The redirect target as path and query, whether the header is absolute or relative.</summary>
    public string? LocationPath =>
        Location is null ? null
        : Uri.TryCreate(Location, UriKind.Absolute, out var absolute) && absolute.Scheme.StartsWith("http", StringComparison.Ordinal)
            ? absolute.PathAndQuery
            : Location;

    public string? SetCookie(string name) =>
        SetCookies.FirstOrDefault(c => c.StartsWith(name + "=", StringComparison.Ordinal));
}
