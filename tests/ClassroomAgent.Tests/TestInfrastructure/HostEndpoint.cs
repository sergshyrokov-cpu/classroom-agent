using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>One routed endpoint of the host, as the TC-5 enumeration tests see it.</summary>
public sealed partial record HostEndpoint(RouteEndpoint Endpoint)
{
    private static readonly string[] SafeMethods = ["GET", "HEAD"];

    private static readonly string[] UnsafeMethods = ["POST", "PUT", "PATCH", "DELETE"];

    /// <summary>The route pattern without slashes at the ends, constraints or defaults, lower case.</summary>
    public string Pattern =>
        ParameterDetail().Replace(Endpoint.RoutePattern.RawText ?? string.Empty, "{$1}").Trim('/').ToLowerInvariant();

    /// <summary>The HTTP methods the endpoint accepts; null when it accepts any (e.g. a Razor page).</summary>
    public IReadOnlyList<string>? Methods => Endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.ToList();

    public bool AllowsAnonymous => Endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;

    /// <summary><c>MapFallback</c> registers its endpoint with the last possible order.</summary>
    public bool IsFallback => Endpoint.Order == int.MaxValue;

    /// <summary>A static-file endpoint: a literal path ending in a file name, read-only methods.</summary>
    public bool IsStaticFile =>
        Methods is { Count: > 0 } methods
        && methods.All(m => SafeMethods.Contains(m, StringComparer.OrdinalIgnoreCase))
        && Endpoint.RoutePattern.Parameters.Count == 0
        && Pattern.Split('/')[^1].Contains('.', StringComparison.Ordinal);

    /// <summary>The state-changing methods this endpoint accepts; POST for an endpoint accepting any method.</summary>
    public IReadOnlyList<string> UnsafeMethodsAccepted =>
        Methods is null
            ? ["POST"]
            : Methods.Where(m => UnsafeMethods.Contains(m, StringComparer.OrdinalIgnoreCase)).ToList();

    public bool IsExemptFromAntiforgery =>
        Endpoint.Metadata.OfType<IAntiforgeryMetadata>().Any(m => !m.RequiresValidation)
        || Endpoint.Metadata.OfType<IgnoreAntiforgeryTokenAttribute>().Any();

    /// <summary>A concrete request path for the pattern.</summary>
    public string SamplePath =>
        "/" + ParameterName().Replace(Pattern, m => m.Groups[1].Value == "statuscode" ? "404" : "sample");

    public override string ToString() => $"{Pattern} [{string.Join(",", Methods ?? ["*"])}]";

    public static IReadOnlyList<HostEndpoint> All(IServiceProvider services) =>
        services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => new HostEndpoint(e))
            .ToList();

    [GeneratedRegex(@"\{\**([^}:=?]+)[^}]*\}")]
    private static partial Regex ParameterDetail();

    [GeneratedRegex(@"\{([^}]+)\}")]
    private static partial Regex ParameterName();
}
