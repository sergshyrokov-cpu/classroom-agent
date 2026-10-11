using ClassroomAgent.Web.Security;
using Serilog.Core;
using Serilog.Events;

namespace ClassroomAgent.Web.Configuration;

/// <summary>
/// Removes the request path, which the host's request log scope adds to every event, from a refusal whose path holds
/// the refused value (US-021 spec FR-013, S-05; SC-10): the course card's 404 names the parameter and the outcome,
/// never the value.
/// </summary>
public sealed class RefusedValueRedaction : ILogEventEnricher
{
    private const string RequestPathProperty = "RequestPath";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (logEvent.Properties.TryGetValue("EventId", out var eventId)
            && eventId is StructureValue structure
            && structure.Properties.Any(p =>
                p.Name == "Id" && p.Value is ScalarValue { Value: int id } && id == CourseCardLog.RefusedEventId))
        {
            logEvent.RemovePropertyIfPresent(RequestPathProperty);
        }
    }
}
