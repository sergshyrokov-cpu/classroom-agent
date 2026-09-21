using System.Security.Cryptography;
using System.Text.Json;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// A service-account key in Google's JSON key format, generated at run time with a fresh RSA key pair. It belongs to
/// no Google project, authorises nothing and is never written to disk or committed (AGENTS.md Hard Stops; TC-4).
/// </summary>
public static class SyntheticServiceAccountKey
{
    public const string ClientEmail = "school-one-agent@synthetic-project.iam.gserviceaccount.com";

    public const string ClientId = "100000000000000000001";

    public const string TokenUri = "https://oauth2.googleapis.com/token";

    /// <summary>A new key as the JSON text the secret store would hold.</summary>
    public static string Create()
    {
        using var rsa = RSA.Create(2048);
        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "service_account",
            ["project_id"] = "synthetic-project",
            ["private_key_id"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(20)).ToLowerInvariant(),
            ["private_key"] = rsa.ExportPkcs8PrivateKeyPem() + "\n",
            ["client_email"] = ClientEmail,
            ["client_id"] = ClientId,
            ["auth_uri"] = "https://accounts.google.com/o/oauth2/auth",
            ["token_uri"] = TokenUri,
        });
    }
}
