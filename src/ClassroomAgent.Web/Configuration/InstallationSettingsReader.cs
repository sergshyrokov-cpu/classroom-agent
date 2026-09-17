using System.Globalization;

namespace ClassroomAgent.Web.Configuration;

/// <summary>
/// Reads and validates the settings this Story requires (US-005 spec FR-001, VR-001, I-1, I-2; api-design §10).
/// The time zone, retention period and default language arrive with the Stories that use them.
/// </summary>
public static class InstallationSettingsReader
{
    public const string InstallationIdKey = "Installation:Id";

    public const string ControlPlaneAddressKey = "ControlPlane:Address";

    public const string PrivatePortKey = "Hosting:PrivatePort";

    public const string ConnectionStringKey = "ConnectionStrings:Installation";

    /// <summary>The ASP.NET Core public endpoint addresses, <c>;</c>-separated.</summary>
    public const string UrlsKey = "urls";

    /// <exception cref="InstallationSettingException">A setting is missing or breaks its rule.</exception>
    public static InstallationSettings Read(IConfiguration configuration) =>
        new(
            InstallationId(configuration),
            ControlPlaneAddress(configuration),
            PrivatePort(configuration),
            Required(configuration, ConnectionStringKey));

    /// <summary>The configured public endpoint addresses; empty when none is configured.</summary>
    public static IReadOnlyList<string> PublicUrls(IConfiguration configuration) =>
        (configuration[UrlsKey] ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static Guid InstallationId(IConfiguration configuration) =>
        Guid.TryParseExact(Required(configuration, InstallationIdKey), "D", out var id)
            ? id
            : throw InstallationSettingException.Invalid(InstallationIdKey, "expected a UUID in its canonical text form");

    private static Uri ControlPlaneAddress(IConfiguration configuration)
    {
        if (!Uri.TryCreate(Required(configuration, ControlPlaneAddressKey), UriKind.Absolute, out var address)
            || address.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrEmpty(address.Host)
            || address.UserInfo.Length > 0
            || address.Query.Length > 0
            || address.Fragment.Length > 0)
        {
            throw InstallationSettingException.Invalid(
                ControlPlaneAddressKey,
                "expected an absolute https address with a host and no user info, query or fragment");
        }

        return address;
    }

    private static int PrivatePort(IConfiguration configuration)
    {
        if (!int.TryParse(Required(configuration, PrivatePortKey), NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            || port is < 1 or > 65535)
        {
            throw InstallationSettingException.Invalid(PrivatePortKey, "expected an integer from 1 to 65535");
        }

        if (PublicUrls(configuration).Any(url => PortOf(url) == port))
        {
            throw InstallationSettingException.Invalid(PrivatePortKey, "the private port must differ from every public endpoint port");
        }

        return port;
    }

    private static int? PortOf(string url)
    {
        // Kestrel accepts '*' and '+' as "any host"; Uri does not.
        var normalized = url.Replace("://*", "://localhost", StringComparison.Ordinal).Replace("://+", "://localhost", StringComparison.Ordinal);
        return Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ? uri.Port : null;
    }

    private static string Required(IConfiguration configuration, string key) =>
        configuration[key] is { } value && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw InstallationSettingException.Missing(key);
}
