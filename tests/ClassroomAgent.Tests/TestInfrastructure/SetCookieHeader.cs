namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>A parsed <c>Set-Cookie</c> header value.</summary>
public sealed record SetCookieHeader(string Name, string Value, IReadOnlyDictionary<string, string?> Attributes)
{
    public const string SessionCookieName = "__Host-cp-session";

    public const string AntiforgeryCookieName = "__Host-cp-antiforgery";

    public bool Has(string attribute) => Attributes.ContainsKey(attribute);

    public string? Get(string attribute) => Attributes.TryGetValue(attribute, out var value) ? value : null;

    public static SetCookieHeader Parse(string header)
    {
        var parts = header.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var separator = parts[0].IndexOf('=', StringComparison.Ordinal);
        var attributes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in parts.Skip(1))
        {
            var equals = part.IndexOf('=', StringComparison.Ordinal);
            if (equals < 0)
            {
                attributes[part] = null;
            }
            else
            {
                attributes[part[..equals]] = part[(equals + 1)..];
            }
        }

        return new SetCookieHeader(parts[0][..separator], parts[0][(separator + 1)..], attributes);
    }
}
