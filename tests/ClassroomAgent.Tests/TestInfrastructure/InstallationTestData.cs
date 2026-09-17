namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>Synthetic installation values for the US-002 tests (TC-4: no real school, domain or client ID).</summary>
public static class InstallationTestData
{
    public const string Name = "Ліцей №1";

    public const string Domain = "school-one.example.test";

    public const string ClientId = "100000000000000000001";

    public const string OtherName = "Гімназія №2";

    public const string OtherDomain = "school-two.example.test";

    public const string OtherClientId = "200000000000000000002";

    /// <summary>A syntactically valid UUID that no test installation ever has.</summary>
    public const string UnknownIdentifier = "0f8fad5b-d9cb-469f-a165-70867728950e";

    /// <summary>The time format of every Control Plane time in <c>uk</c> (OD-003).</summary>
    public static string UkrainianUtcTime(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString("dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture) + " UTC";

    public static IReadOnlyList<KeyValuePair<string, string>> RegisterFields(
        string name = Name,
        string domain = Domain,
        string clientId = ClientId) =>
    [
        new("name", name),
        new("domain", domain),
        new("clientId", clientId),
    ];

    public static IReadOnlyList<KeyValuePair<string, string>> NameFields(string name) => [new("name", name)];

    public static IReadOnlyList<KeyValuePair<string, string>> ClientIdFields(string clientId) => [new("clientId", clientId)];
}
