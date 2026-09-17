using System.Text.Json;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>Synthetic service-channel requests for the US-005 Control Plane tests (api-design §4, §5).</summary>
public static class LegitimacyCheckTestData
{
    public const string Path = "/service/v1/legitimacy-checks";

    public const string ApplicationVersion = "1.0.0";

    public static string RequestJson(Guid installationId, string applicationVersion = ApplicationVersion, int contractVersion = 1) =>
        JsonSerializer.Serialize(new
        {
            installationId = installationId.ToString("D"),
            applicationVersion,
            contractVersion,
        });
}
