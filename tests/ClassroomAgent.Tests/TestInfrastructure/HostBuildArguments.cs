using System.Globalization;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-047: settings passed to a host's <c>BuildApplication</c> as command-line arguments, the way a console start
/// receives them. Only configuration travels this way — the service-mode flag is a parameter of its own (VR-002).
/// </summary>
public static class HostBuildArguments
{
    /// <summary>The environment every US-047 host is built in: not <c>Development</c>, so no console sink (DC-10).</summary>
    public const string EnvironmentName = "Test";

    public static string[] From(IReadOnlyDictionary<string, string?> settings) =>
    [
        string.Create(CultureInfo.InvariantCulture, $"--environment={EnvironmentName}"),
        .. settings
            .Where(setting => setting.Value is not null)
            .Select(setting => string.Create(CultureInfo.InvariantCulture, $"--{setting.Key}={setting.Value}")),
    ];

    /// <summary>The installation's required settings, with the given log and key directories; no database is reached at build.</summary>
    public static Dictionary<string, string?> Installation(string? logDirectory, string? keyDirectory)
    {
        InstallationConfigurationKeys.PlaceDefaultOAuthSecret();
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [InstallationConfigurationKeys.InstallationId] = Guid.NewGuid().ToString("D"),
            [InstallationConfigurationKeys.ControlPlaneAddress] = InstallationConfigurationKeys.ControlPlaneAddressValue,
            [InstallationConfigurationKeys.PrivatePort] = InstallationConfigurationKeys.PrivatePortValue.ToString(CultureInfo.InvariantCulture),
            [PushTestData.PrivateAddressKey] = InstallationConfigurationKeys.PrivateAddressValue,
            [InstallationConfigurationKeys.ConnectionString] = UnreachedConnectionString,
            [InstallationConfigurationKeys.LogDirectory] = logDirectory,
            [InstallationConfigurationKeys.DataProtectionKeyDirectory] = keyDirectory,
            [InstallationConfigurationKeys.PublicBaseAddress] = InstallationConfigurationKeys.PublicBaseAddressValue,
            [InstallationConfigurationKeys.OAuthClientId] = InstallationConfigurationKeys.OAuthClientIdValue,
            [InstallationConfigurationKeys.OAuthClientSecretReference] = InstallationConfigurationKeys.OAuthClientSecretReferenceValue,
            ["Retention:Years"] = "5",
            [JournalTestData.TimeZoneKey] = JournalTestData.KyivZoneId,
        };
    }

    /// <summary>The Control Plane's required settings, with the given connection string, log and key directories.</summary>
    public static Dictionary<string, string?> ControlPlane(string? logDirectory, string? keyDirectory, string connectionString = UnreachedConnectionString) =>
        new(StringComparer.Ordinal)
        {
            [ConfigurationKeys.ConnectionString] = connectionString,
            [ConfigurationKeys.LogDirectory] = logDirectory,
            [ConfigurationKeys.DataProtectionKeyDirectory] = keyDirectory,
        };

    /// <summary>A syntactically valid connection string no test opens: building a host connects to nothing.</summary>
    public const string UnreachedConnectionString = "Host=127.0.0.1;Port=1;Database=us047-never-opened;Username=none";
}
