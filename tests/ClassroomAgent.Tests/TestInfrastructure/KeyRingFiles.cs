using System.Runtime.Versioning;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-049: reads and prepares a host's Data Protection key directory. A key ring the test builds itself writes keys
/// exactly as a host before or after this Story would (plain, or DPAPI for the current user), and a DPAPI key made
/// undecryptable stands in for one written under another Windows account (spec I-2).
/// </summary>
[SupportedOSPlatform("windows")]
public static class KeyRingFiles
{
    private const string KeyFilePattern = "key-*.xml";

    /// <summary>Every key file in the directory, by name; none when it does not exist.</summary>
    public static IReadOnlyList<KeyFile> Read(string directory) =>
        [.. HostFolders.Files(directory, KeyFilePattern).Order(StringComparer.Ordinal).Select(ReadFile)];

    /// <summary>The exact bytes of every key file, by full path — to prove a file was left as it was.</summary>
    public static IReadOnlyDictionary<string, byte[]> Snapshot(string directory) =>
        HostFolders.Files(directory, KeyFilePattern).ToDictionary(path => path, File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);

    /// <summary>Asserts the directory holds at least one key and every key is encrypted for the current user.</summary>
    public static void AssertAllEncryptedForCurrentUser(string directory)
    {
        var keys = Read(directory);
        Assert.NotEmpty(keys);
        Assert.All(keys, key => Assert.True(key.IsEncryptedForCurrentUser, key.Describe()));
    }

    /// <summary>
    /// Writes one key into <paramref name="directory"/> with a key ring of the given application name — DPAPI for the
    /// current user, or plain as a host before US-049 wrote it — and returns <paramref name="payload"/> protected with it.
    /// </summary>
    public static string WriteKeyAndProtect(
        string directory,
        string applicationName,
        bool encrypted,
        string purpose,
        string payload)
    {
        var services = new ServiceCollection();
        var builder = services.AddDataProtection()
            .SetApplicationName(applicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(directory));
        if (encrypted)
        {
            builder.ProtectKeysWithDpapi(protectToLocalMachine: false);
        }

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IDataProtectionProvider>().CreateProtector(purpose).Protect(payload);
    }

    /// <summary>
    /// Creates one DPAPI-encrypted key (current user) with the given dates in the directory, the way a host's key ring
    /// does when it rolls a key, and returns its id.
    /// </summary>
    public static Guid CreateKey(string directory, string applicationName, DateTimeOffset activation, DateTimeOffset expiration)
    {
        using var provider = EncryptedKeyRing(directory, applicationName);
        return provider.GetRequiredService<IKeyManager>().CreateNewKey(activation, expiration).KeyId;
    }

    /// <summary>Protects a payload with the default key a DPAPI key ring of the given application resolves in the directory.</summary>
    public static string Protect(string directory, string applicationName, string purpose, string payload)
    {
        using var provider = EncryptedKeyRing(directory, applicationName);
        return provider.GetRequiredService<IDataProtectionProvider>().CreateProtector(purpose).Protect(payload);
    }

    /// <summary>The id of the key a protected payload names: a 4-byte magic header, then the key id.</summary>
    public static Guid KeyIdOf(string protectedPayload) =>
        new(WebEncoders.Base64UrlDecode(protectedPayload).AsSpan(4, 16));

    /// <summary>The file a key ring writes for a key id.</summary>
    public static string PathOf(string directory, Guid keyId) =>
        Path.Combine(directory, $"key-{keyId:D}.xml");

    /// <summary>The key files present now that were not in <paramref name="before"/>.</summary>
    public static IReadOnlyList<KeyFile> NewSince(string directory, IReadOnlyDictionary<string, byte[]> before) =>
        [.. Read(directory).Where(key => !before.ContainsKey(key.Path))];

    /// <summary>Asserts every file of <paramref name="before"/> is still there with exactly the same bytes (spec FR-003, FR-004).</summary>
    public static void AssertUnchanged(string directory, IReadOnlyDictionary<string, byte[]> before)
    {
        var now = Snapshot(directory);
        Assert.All(before, file =>
        {
            Assert.True(now.ContainsKey(file.Key), $"{Path.GetFileName(file.Key)} was removed.");
            Assert.True(file.Value.AsSpan().SequenceEqual(now[file.Key]), $"{Path.GetFileName(file.Key)} was rewritten.");
        });
    }

    /// <summary>
    /// Corrupts the tail of every DPAPI blob in the directory, so DPAPI refuses to decrypt it under any account — the
    /// failure a host sees for a key written under another Windows account (spec I-2). Fails when a key in the
    /// directory is not DPAPI-encrypted: there is nothing another account would fail to read.
    /// </summary>
    /// <returns>The base64 text of every corrupted blob, for asserting it never reaches a log.</returns>
    public static IReadOnlyList<string> MakeEveryKeyUndecryptable(string directory)
    {
        var paths = HostFolders.Files(directory, KeyFilePattern);
        Assert.NotEmpty(paths);
        return [.. paths.Select(MakeUndecryptable)];
    }

    /// <summary>Corrupts the DPAPI blob of one key file (see <see cref="MakeEveryKeyUndecryptable"/>) and returns its new text.</summary>
    public static string MakeUndecryptable(string path)
    {
        var document = XDocument.Load(path);
        var value = DpapiValueElement(document)
            ?? throw new Xunit.Sdk.XunitException(
                $"{Path.GetFileName(path)} carries no DPAPI-encrypted key to make undecryptable: {ReadFile(path).Describe()}");
        var blob = Convert.FromBase64String(value.Value.Trim());
        for (var i = blob.Length - 16; i < blob.Length; i++)
        {
            blob[i] ^= 0xFF;
        }

        value.Value = Convert.ToBase64String(blob);
        document.Save(path);
        return value.Value;
    }

    private static ServiceProvider EncryptedKeyRing(string directory, string applicationName)
    {
        var services = new ServiceCollection();
        services.AddDataProtection()
            .SetApplicationName(applicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(directory))
            .ProtectKeysWithDpapi(protectToLocalMachine: false);
        return services.BuildServiceProvider();
    }

    private static KeyFile ReadFile(string path)
    {
        var document = XDocument.Load(path);
        var hasPlain = document.Descendants().Any(element => element.Name.LocalName == "masterKey");
        var decryptor = document.Descendants()
            .Where(element => element.Name.LocalName == "encryptedSecret")
            .Select(element => (string?)element.Attribute("decryptorType"))
            .FirstOrDefault();
        var value = DpapiValueElement(document);
        return new KeyFile(path, hasPlain, decryptor, value is null ? null : Convert.FromBase64String(value.Value.Trim()));
    }

    private static XElement? DpapiValueElement(XDocument document) =>
        document.Descendants()
            .Where(element => element.Name.LocalName == "encryptedSecret"
                && ((string?)element.Attribute("decryptorType"))?.StartsWith(KeyFile.DpapiDecryptorTypeName, StringComparison.Ordinal) == true)
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "value");
}
