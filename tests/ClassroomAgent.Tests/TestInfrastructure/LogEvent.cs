using System.Text.Json;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// One line of a Serilog compact JSON log file (DC-10). The level is <c>Information</c> when the line
/// carries no <c>@l</c>. <see cref="EventName"/> is the name of the <c>EventId</c> the host logged with —
/// the event names are fixed by the US-005 test strategy §3.
/// </summary>
public sealed record LogEvent(string Level, string? EventName, string Line, JsonElement Json)
{
    public string? Property(string name) =>
        Json.TryGetProperty(name, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText()
            : null;

    /// <summary>Every JSON line of the files, in file order.</summary>
    public static IReadOnlyList<LogEvent> Parse(IEnumerable<string> fileContents)
    {
        var events = new List<LogEvent>();
        foreach (var content in fileContents)
        {
            foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                using var document = JsonDocument.Parse(line);
                var json = document.RootElement.Clone();
                var level = json.TryGetProperty("@l", out var l) ? l.GetString() ?? "Information" : "Information";
                string? eventName = null;
                if (json.TryGetProperty("EventId", out var eventId)
                    && eventId.ValueKind == JsonValueKind.Object
                    && eventId.TryGetProperty("Name", out var name))
                {
                    eventName = name.GetString();
                }

                events.Add(new LogEvent(level, eventName, line, json));
            }
        }

        return events;
    }
}
