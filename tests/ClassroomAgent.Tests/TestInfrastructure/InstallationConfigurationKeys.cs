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

    /// <summary>US-008 spec FR-001, VR-003: the Data Protection key ring directory. Required from US-008 on.</summary>
    public const string DataProtectionKeyDirectory = "DataProtection:KeyDirectory";

    /// <summary>US-008 spec FR-001, VR-001: the school's public base address; the redirect URI is built from it.</summary>
    public const string PublicBaseAddress = "Installation:PublicBaseAddress";

    /// <summary>US-008 spec FR-001, VR-002: the installation's OAuth client id. Not a secret; never logged.</summary>
    public const string OAuthClientId = "GoogleOAuth:ClientId";

    /// <summary>US-008 spec FR-001, VR-002: a reference into the secret store, never the secret itself (SC-7).</summary>
    public const string OAuthClientSecretReference = "GoogleOAuth:ClientSecretReference";

    /// <summary>US-008 spec FR-001, VR-004: optional; Ukrainian when absent (NFR-073).</summary>
    public const string DefaultLanguage = "Ui:DefaultLanguage";

    public const int PrivatePortValue = 8081;

    /// <summary>The school's public address in tests — synthetic, matching <see cref="InstallationTestData.Domain"/> (TC-4).</summary>
    public const string PublicBaseAddressValue = "https://school-one.example.test";

    /// <summary>A synthetic OAuth client id, shaped like Google's but belonging to no project (TC-4).</summary>
    public const string OAuthClientIdValue = "100000000000-abcdefghijklmnopqrstuvwxyz012345.apps.googleusercontent.com";

    /// <summary>A reference, not a secret: naming a secret that only the test environment would hold (SC-7).</summary>
    public const string OAuthClientSecretReferenceValue = "installation-oauth-client-secret";

    /// <summary>
    /// The synthetic secret the default reference resolves to. OD-004 (option 1) makes the reference the name of an
    /// environment variable and an absent one stop the start, so the fixture places it — once for the whole test
    /// process, with the same name and value everywhere, so tests running in parallel cannot disagree.
    /// </summary>
    public const string OAuthClientSecretValue = "synthetic-oauth-client-secret-for-tests";

    /// <summary>Places <see cref="OAuthClientSecretValue"/> under <see cref="OAuthClientSecretReferenceValue"/>.</summary>
    public static void PlaceDefaultOAuthSecret() =>
        Environment.SetEnvironmentVariable(OAuthClientSecretReferenceValue, OAuthClientSecretValue);

    /// <summary>The address the private port listens on in tests (US-006 spec FR-001).</summary>
    public const string PrivateAddressValue = "127.0.0.1";

    public const string ControlPlaneAddressValue = "https://control-plane.test";
}
