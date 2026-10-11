using Serilog.Core;
using Serilog.Events;

namespace ClassroomAgent.ControlPlane.Security;

/// <summary>
/// US-045 (trebovaniya v89 §8; DC-10, SC-10; OD-001 — the same rule as the installation): a line written inside a
/// request names the route of the page serving it, never the address the caller sent. The request log scope puts the
/// raw path into <c>RequestPath</c>; this replaces it with the route template of the selected endpoint, or with
/// <see cref="Unmatched"/> when no page serves the request (spec FR-002, FR-003, §8).
/// </summary>
public sealed class RequestRouteEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    /// <summary>Spec FR-003: the one marker of a line written while no page serves the request.</summary>
    public const string Unmatched = "(unmatched)";

    private const string RequestPathProperty = "RequestPath";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (!logEvent.Properties.ContainsKey(RequestPathProperty))
        {
            return;
        }

        logEvent.AddOrUpdateProperty(new LogEventProperty(RequestPathProperty, new ScalarValue(Route())));
    }

    private string Route()
    {
        try
        {
            return httpContextAccessor.HttpContext?.GetEndpoint() is RouteEndpoint endpoint
                   && endpoint.Metadata.GetMetadata<UnmatchedRequestMetadata>() is null
                   && endpoint.RoutePattern.RawText is { Length: > 0 } template
                ? "/" + template.TrimStart('/')
                : Unmatched;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Spec §8: fail closed — the line is kept, and never falls back to the raw path.
            return Unmatched;
        }
    }
}
