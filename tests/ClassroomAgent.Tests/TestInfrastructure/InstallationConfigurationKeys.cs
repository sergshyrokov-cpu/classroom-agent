namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Configuration keys the installation host reads and the tests set (US-005 api-design §10, test
/// strategy §3). The implementation uses the same keys; renaming one changes both sides.
/// </summary>
public static class InstallationConfigurationKeys
{
    public const string InstallationId = "Installation:Id";

    public const string ControlPlaneAddress = "ControlPlane:Address";

    public const string PrivatePort = "Hosting:PrivatePort";

    public const string ConnectionString = "ConnectionStrings:Installation";

    /// <summary>The log file directory — the same key as the Control Plane's (US-001), not validated by US-005.</summary>
    public const string LogDirectory = "LogFile:Directory";

    /// <summary>The ASP.NET Core public endpoint addresses; the private port must differ from their ports (VR-001).</summary>
    public const string Urls = "urls";

    /// <summary>Control Plane: optional minimum supported installation version (spec FR-005).</summary>
    public const string MinimumSupportedVersion = "Compatibility:MinimumSupportedVersion";

    /// <summary>Control Plane: optional recommended installation version (spec FR-005).</summary>
    public const string RecommendedVersion = "Compatibility:RecommendedVersion";

    public const int PrivatePortValue = 8081;

    public const string ControlPlaneAddressValue = "https://control-plane.test";
}
