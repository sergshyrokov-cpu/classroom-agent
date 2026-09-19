using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// VR-001 for the push address (US-006 api-design §7): the rules in their fixed order, each reported as its
/// own translation key, and the canonical form the database stores — <c>http://</c>, host in lower case
/// (IPv6 bracketed), an explicit port in decimal without leading zeros, nothing else.
/// </summary>
public static class PushAddressRules
{
    public const int MaximumLength = 255;

    private const int MaximumHostLength = 253;

    private const int MaximumLabelLength = 63;

    private const string SchemeSeparator = "://";

    private const string Scheme = "http";

    /// <summary>
    /// Checks one entered value. An empty or whitespace-only value means "not set" and succeeds with a null
    /// canonical form.
    /// </summary>
    /// <returns>The translation key of the first failing rule, or null when the value is accepted.</returns>
    public static string? Validate(string? entered, out string? canonical)
    {
        canonical = null;
        var address = (entered ?? string.Empty).Trim();
        if (address.Length == 0)
        {
            return null;
        }

        if (address.Length > MaximumLength)
        {
            return "Installation.PushAddress.Length";
        }

        var separator = address.IndexOf(SchemeSeparator, StringComparison.Ordinal);
        if (separator <= 0 || separator + SchemeSeparator.Length == address.Length || !IsSchemeShaped(address[..separator]))
        {
            return "Installation.PushAddress.Format";
        }

        if (!address[..separator].Equals(Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return "Installation.PushAddress.Scheme";
        }

        var rest = address[(separator + SchemeSeparator.Length)..];
        var authorityEnd = rest.IndexOfAny(['/', '?', '#']);
        var authority = authorityEnd < 0 ? rest : rest[..authorityEnd];
        var tail = authorityEnd < 0 ? string.Empty : rest[authorityEnd..];
        if (authority.Contains('@', StringComparison.Ordinal) || tail.Length > 1 || (tail.Length == 1 && tail[0] != '/'))
        {
            return "Installation.PushAddress.Extra";
        }

        var key = Split(authority, out var host, out var port);
        if (key is not null)
        {
            return key;
        }

        canonical = string.Create(CultureInfo.InvariantCulture, $"{Scheme}{SchemeSeparator}{host}:{port}");
        return null;
    }

    /// <summary>Splits the authority into a canonical host and port, or names the rule it breaks.</summary>
    private static string? Split(string authority, out string host, out int port)
    {
        host = string.Empty;
        port = 0;
        string portText;

        if (authority.StartsWith('['))
        {
            var close = authority.IndexOf(']', StringComparison.Ordinal);
            if (close < 0
                || !IPAddress.TryParse(authority[1..close], out var literal)
                || literal.AddressFamily != AddressFamily.InterNetworkV6)
            {
                return "Installation.PushAddress.Host";
            }

            var after = authority[(close + 1)..];
            if (after.Length > 0 && after[0] != ':')
            {
                return "Installation.PushAddress.Host";
            }

            host = "[" + literal.ToString() + "]";
            portText = after.Length == 0 ? string.Empty : after[1..];
        }
        else
        {
            var colon = authority.IndexOf(':', StringComparison.Ordinal);

            // More than one colon means an IPv6 literal written without brackets (spec I-2).
            if (colon >= 0 && authority.IndexOf(':', colon + 1) >= 0)
            {
                return "Installation.PushAddress.Host";
            }

            var name = colon < 0 ? authority : authority[..colon];
            if (!IsHostName(name))
            {
                return "Installation.PushAddress.Host";
            }

            host = name.ToLowerInvariant();
            portText = colon < 0 ? string.Empty : authority[(colon + 1)..];
        }

        return int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out port) && port is >= 1 and <= 65535
            ? null
            : "Installation.PushAddress.Port";
    }

    private static bool IsSchemeShaped(string scheme) =>
        char.IsAsciiLetter(scheme[0]) && scheme.All(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.');

    /// <summary>A DNS host name; an IPv4 literal satisfies the same shape.</summary>
    private static bool IsHostName(string name) =>
        name.Length is > 0 and <= MaximumHostLength && name.Split('.').All(IsWellFormedLabel);

    private static bool IsWellFormedLabel(string label) =>
        label.Length is > 0 and <= MaximumLabelLength
        && label[0] != '-'
        && label[^1] != '-'
        && label.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
}
