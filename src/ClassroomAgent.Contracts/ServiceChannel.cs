using System.Text.Json;

namespace ClassroomAgent.Contracts;

/// <summary>How both sides put the contract on the wire (US-005 api-design §2, §4; DC-12).</summary>
public static class ServiceChannel
{
    /// <summary>The legitimacy check endpoint, relative to the Control Plane address.</summary>
    public const string LegitimacyCheckPath = "service/v1/legitimacy-checks";

    /// <summary>The status-change push receiver, relative to an installation's push address (US-006 api-design §2).</summary>
    public const string StatusPushPath = "service/v1/status-pushes";

    /// <summary>
    /// camelCase property names, case-sensitive, numbers never read from strings; unknown properties are
    /// ignored so the contract can grow additively. Read-only.
    /// </summary>
    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
