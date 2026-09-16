namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Configuration keys the Control Plane reads and the tests set (test strategy §3).
/// The implementation uses the same keys; renaming one changes both sides.
/// </summary>
public static class ConfigurationKeys
{
    public const string ConnectionString = "ConnectionStrings:ControlPlane";

    public const string DataProtectionKeyDirectory = "DataProtection:KeyDirectory";

    public const string LogDirectory = "LogFile:Directory";
}
