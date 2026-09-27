using System.Globalization;
using System.Net;
using System.Net.Sockets;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;

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

    /// <summary>The address the private port listens on: an IP literal, or <c>*</c> for all addresses (US-006 VR-003).</summary>
    public const string PrivateAddressKey = "Hosting:PrivateAddress";

    /// <summary>The documented "all addresses" value; no other wildcard is accepted (spec I-1).</summary>
    public const string AllAddresses = "*";

    public const string ConnectionStringKey = "ConnectionStrings:Installation";

    /// <summary>The ASP.NET Core public endpoint addresses, <c>;</c>-separated.</summary>
    public const string UrlsKey = "urls";

    /// <summary>US-008 spec FR-001, VR-003: the Data Protection key ring directory (SC-7, S-18).</summary>
    public const string DataProtectionKeyDirectoryKey = "DataProtection:KeyDirectory";

    /// <summary>US-008 spec FR-001, VR-001: the school public base address; the redirect URI is built from it.</summary>
    public const string PublicBaseAddressKey = "Installation:PublicBaseAddress";

    /// <summary>US-008 spec FR-001, VR-002: the OAuth client id. Not a secret, never logged.</summary>
    public const string OAuthClientIdKey = "GoogleOAuth:ClientId";

    /// <summary>US-008 spec FR-001, VR-002: the name of the secret holding the OAuth client secret (OD-004).</summary>
    public const string OAuthClientSecretReferenceKey = "GoogleOAuth:ClientSecretReference";

    /// <summary>
    /// US-011 spec FR-016, I-1: the name of the secret holding the service-account key. Optional — its absence is the
    /// check's <c>KeyUnavailable</c>, not a refusal to start. Never logged (SC-7).
    /// </summary>
    public const string ServiceAccountKeyReferenceKey = "Google:ServiceAccountKeyReference";

    /// <summary>US-008 spec FR-001, VR-004: optional; Ukrainian when absent (NFR-073).</summary>
    public const string DefaultLanguageKey = "Ui:DefaultLanguage";

    /// <summary>US-013 spec FR-013, VR-001: the optional synchronization run interval, in minutes.</summary>
    public const string SyncIntervalKey = "Sync:IntervalMinutes";

    /// <summary>US-013 spec I-6: one hour when <see cref="SyncIntervalKey"/> is unset, as DC-3 documents it.</summary>
    public static readonly TimeSpan DefaultSyncInterval = TimeSpan.FromMinutes(60);

    /// <summary>US-013 spec VR-001: the permitted range of <see cref="SyncIntervalKey"/>, in whole minutes.</summary>
    public const int MinimumSyncIntervalMinutes = 1;

    /// <summary>US-013 spec VR-001: a day is the longest interval the setting accepts.</summary>
    public const int MaximumSyncIntervalMinutes = 1440;

    /// <exception cref="InstallationSettingException">A setting is missing or breaks its rule.</exception>
    public static InstallationSettings Read(IConfiguration configuration, ISecretStore secretStore) =>
        new(
            InstallationId(configuration),
            ControlPlaneAddress(configuration),
            PrivatePort(configuration),
            PrivateAddress(configuration),
            Required(configuration, ConnectionStringKey),
            KeyDirectory(configuration),
            PublicBaseAddress(configuration),
            Required(configuration, OAuthClientIdKey).Trim(),
            OAuthClientSecret(configuration, secretStore),
            DefaultLanguage(configuration),
            ServiceAccountKeyReference(configuration),
            SyncInterval(configuration));

    /// <summary>US-011 spec VR-004: trimmed; blank counts as absent. Resolved per check, not here (spec FR-016).</summary>
    private static string? ServiceAccountKeyReference(IConfiguration configuration) =>
        configuration[ServiceAccountKeyReferenceKey]?.Trim() is { Length: > 0 } reference ? reference : null;

    /// <summary>
    /// VR-003: a path the process can create if absent and write to. A file where a directory belongs, or a path
    /// the process cannot create, stops the start.
    /// </summary>
    private static string KeyDirectory(IConfiguration configuration)
    {
        var directory = Required(configuration, DataProtectionKeyDirectoryKey).Trim();
        if (File.Exists(directory))
        {
            throw InstallationSettingException.Invalid(
                DataProtectionKeyDirectoryKey,
                "expected a directory, but a file exists at that path");
        }

        try
        {
            System.IO.Directory.CreateDirectory(directory);
        }
        catch (Exception failure)
            when (failure is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw InstallationSettingException.Invalid(
                DataProtectionKeyDirectoryKey,
                "expected a directory path the process can create and write to");
        }

        return directory;
    }

    /// <summary>
    /// VR-001: absolute <c>https</c>, a host, an optional port, and nothing else - no path, no query, no
    /// fragment, no user info. The Google redirect URI is built from it, so a forgeable source is a finding (v78).
    /// </summary>
    private static Uri PublicBaseAddress(IConfiguration configuration)
    {
        if (!Uri.TryCreate(Required(configuration, PublicBaseAddressKey), UriKind.Absolute, out var address)
            || address.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrEmpty(address.Host)
            || address.UserInfo.Length > 0
            || address.Query.Length > 0
            || address.Fragment.Length > 0
            || address.AbsolutePath != "/")
        {
            throw InstallationSettingException.Invalid(
                PublicBaseAddressKey,
                "expected an absolute https address with a host and no path, query, fragment or user info");
        }

        return address;
    }

    /// <summary>
    /// OD-004, option 1: the configured reference names an environment variable, and the secret is read from it
    /// once at start-up. A reference naming nothing stops the start - fail fast on a missing secret, which FR-001
    /// permits. A reference naming a <em>wrong</em> secret does not: that surfaces as a failed sign-in, so a
    /// mistyped secret cannot take the school read-only views down with it (spec I-5). Neither the reference nor
    /// the secret is ever logged (SC-7, SC-10).
    /// </summary>
    private static string OAuthClientSecret(IConfiguration configuration, ISecretStore secretStore)
    {
        var reference = Required(configuration, OAuthClientSecretReferenceKey).Trim();
        return secretStore.Resolve(reference)
            ?? throw InstallationSettingException.Invalid(
                OAuthClientSecretReferenceKey,
                "the configured secret store holds no secret under that reference");
    }

    /// <summary>
    /// US-013 spec FR-013, VR-001: optional, in whole minutes - blank or absent yields the one-hour default so an
    /// installation that never sets it still synchronizes; present, it must fall inside the day it bounds so a
    /// mistyped value cannot silently stop synchronization from ever running.
    /// </summary>
    private static TimeSpan SyncInterval(IConfiguration configuration)
    {
        if (configuration[SyncIntervalKey] is not { } configured || string.IsNullOrWhiteSpace(configured))
        {
            return DefaultSyncInterval;
        }

        if (!int.TryParse(configured, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            || minutes < MinimumSyncIntervalMinutes
            || minutes > MaximumSyncIntervalMinutes)
        {
            throw InstallationSettingException.Invalid(SyncIntervalKey, "expected an integer from 1 to 1440");
        }

        return TimeSpan.FromMinutes(minutes);
    }

    /// <summary>VR-004: <c>uk</c> or <c>en</c>, case-insensitive, or absent - Ukrainian when absent.</summary>
    private static UiLanguage DefaultLanguage(IConfiguration configuration)
    {
        if (configuration[DefaultLanguageKey] is not { } configured)
        {
            return UiLanguage.Uk;
        }

        return configured.Trim().ToLowerInvariant() switch
        {
            "uk" => UiLanguage.Uk,
            "en" => UiLanguage.En,
            _ => throw InstallationSettingException.Invalid(DefaultLanguageKey, "expected uk or en"),
        };
    }

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

    /// <summary>
    /// VR-003: an IPv4 or IPv6 literal (brackets optional), or exactly <c>*</c>. A host name is refused —
    /// Kestrel binds addresses and resolving a name at startup would make the binding depend on DNS (spec I-1).
    /// </summary>
    private static string PrivateAddress(IConfiguration configuration)
    {
        var address = Required(configuration, PrivateAddressKey);
        if (address == AllAddresses)
        {
            return address;
        }

        if (Literal(address) is not { } literal)
        {
            throw InstallationSettingException.Invalid(
                PrivateAddressKey,
                "expected an IPv4 or IPv6 address literal, or * for all addresses");
        }

        return Bind(literal);
    }

    /// <summary>The parsed IP literal, with the brackets of an IPv6 address removed; null when it is none.</summary>
    private static IPAddress? Literal(string address)
    {
        var bare = address.StartsWith('[') && address.EndsWith(']') ? address[1..^1] : address;
        return IPAddress.TryParse(bare, out var literal) ? literal : null;
    }

    /// <summary>The literal as Kestrel writes it in a URL: IPv6 in brackets.</summary>
    private static string Bind(IPAddress literal) =>
        literal.AddressFamily == AddressFamily.InterNetworkV6 ? "[" + literal.ToString() + "]" : literal.ToString();

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
