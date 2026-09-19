using System.Globalization;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Synthetic push values and paths for the US-006 tests (api-design §2, §3, §7; test strategy §3).
/// The implementation uses the same paths, field name and JSON property; renaming one changes both sides.
/// </summary>
public static class PushTestData
{
    /// <summary>The receiver path on the installation's private port, and the path the sender posts to.</summary>
    public const string StatusPushPath = "/service/v1/status-pushes";

    /// <summary>The form field of the push address on the registration and push address pages.</summary>
    public const string FieldName = "pushAddress";

    public const string Address = "http://10.0.0.5:8081";

    public const string OtherAddress = "http://10.0.0.6:8081";

    /// <summary>The name the Control Plane's HTTP client for pushes is registered under (test strategy §3).</summary>
    public const string HttpClientName = "status-push";

    /// <summary>The installation's new required setting: the address its private port listens on (spec FR-001).</summary>
    public const string PrivateAddressKey = "Hosting:PrivateAddress";

    /// <summary>The value meaning "all addresses" (spec I-1, `trebovaniya.md` v76).</summary>
    public const string AllAddresses = "*";

    public static string PushAddressPath(Guid installation) =>
        string.Create(CultureInfo.InvariantCulture, $"/installations/{installation:D}/push-address");

    public static string DetailPath(Guid installation) =>
        string.Create(CultureInfo.InvariantCulture, $"/installations/{installation:D}");

    public static IReadOnlyList<KeyValuePair<string, string>> AddressFields(string address) =>
        [new(FieldName, address)];

    /// <summary>The wire body of a push (api-design §3).</summary>
    public static string Body(Guid installationId) =>
        string.Create(CultureInfo.InvariantCulture, $$"""{"installationId":"{{installationId:D}}"}""");

    /// <summary>Translation keys the Story adds (api-design §7).</summary>
    public static IReadOnlyList<string> TranslationKeys { get; } =
    [
        "Installation.PushAddress.Label",
        "Installation.PushAddress.Hint",
        "Installation.PushAddress.NotSet",
        "Installation.PushAddress.MissingWarning",
        "Installation.PushAddress.Change",
        "Installation.PushAddress.Title",
        "Installation.PushAddress.ClearNote",
        "Installation.PushAddress.Length",
        "Installation.PushAddress.Format",
        "Installation.PushAddress.Scheme",
        "Installation.PushAddress.Extra",
        "Installation.PushAddress.Host",
        "Installation.PushAddress.Port",
    ];
}
