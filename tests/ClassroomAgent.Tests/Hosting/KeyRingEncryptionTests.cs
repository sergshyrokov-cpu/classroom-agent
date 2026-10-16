using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Xml.Linq;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClassroomAgent.Tests.Hosting;

/// <summary>
/// US-049 (spec FR-001 … FR-004, FR-006; AC-001, AC-002, AC-003, AC-005): what each host's Data Protection key ring
/// writes into and reads from its key directory, proved on the key files themselves (VR-002) — the host is built
/// through its <c>BuildApplication</c> and its key ring used directly, without serving a request. A DPAPI key made
/// undecryptable stands in for a key written under another Windows account (spec I-2; review m-3).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class KeyRingEncryptionTests
{
    private const string Purpose = "US-049.probe";

    public static TheoryData<BuiltHostKind> Hosts => new() { BuiltHostKind.Installation, BuiltHostKind.ControlPlane };

    /// <summary>Calibrates <see cref="KeyFile.IsEncryptedForCurrentUser"/> on ASP.NET Core's own DPAPI encryptor (VR-001).</summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void TheDpapiScopeCheck_TellsTheCurrentUserFromTheMachine(bool protectToLocalMachine, bool expected)
    {
        var encryptor = new DpapiXmlEncryptor(protectToLocalMachine, NullLoggerFactory.Instance);

        var encrypted = encryptor.Encrypt(new XElement("secret", "probe")).EncryptedElement;
        var blob = Convert.FromBase64String(encrypted.Descendants("value").Single().Value);
        var key = new KeyFile("probe", HasPlainKeyMaterial: false, KeyFile.DpapiDecryptorTypeName, blob);

        Assert.Equal(expected, key.IsEncryptedForCurrentUser);
    }

    /// <summary>AC-001 (FR-001, FR-002, VR-001, VR-002): the first key on an empty directory is DPAPI for the current user, with no plain key material.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task OnAnEmptyKeyDirectory_TheFirstKey_IsWrittenEncryptedForTheCurrentUser(BuiltHostKind host)
    {
        using var folders = new HostFolders();
        var keys = folders.Absolute("keys");
        await using (var app = Build(host, folders))
        {
            var protector = Protector(app);
            Assert.Equal("payload", protector.Unprotect(protector.Protect("payload")));
        }

        KeyRingFiles.AssertAllEncryptedForCurrentUser(keys);
    }

    /// <summary>
    /// AC-002 (FR-003, S-02; review m-3): the only key — the newest, still valid default key — cannot be decrypted. The
    /// host still works, refuses what that key protected, creates a new encrypted key, leaves the unreadable file as it
    /// is, and logs none of the key's bytes.
    /// </summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task AKeyNoAccountCanDecrypt_IsSkipped_AndANewEncryptedKeyIsCreated(BuiltHostKind host)
    {
        var ct = TestContext.Current.CancellationToken;
        using var folders = new HostFolders();
        var keys = folders.Absolute("keys");
        var earlier = KeyRingFiles.WriteKeyAndProtect(keys, BuiltHost.DataProtectionApplicationName(host), encrypted: true, Purpose, "earlier");
        var blobs = KeyRingFiles.MakeEveryKeyUndecryptable(keys);
        var before = KeyRingFiles.Snapshot(keys);

        await using (var app = Build(host, folders))
        {
            var protector = Protector(app);
            Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(earlier));
            Assert.Equal("later", protector.Unprotect(protector.Protect("later")));
        }

        KeyRingFiles.AssertUnchanged(keys, before);
        var created = KeyRingFiles.NewSince(keys, before);
        Assert.NotEmpty(created);
        Assert.All(created, key => Assert.True(key.IsEncryptedForCurrentUser, key.Describe()));
        var logs = await BuiltHost.ReadFilesAsync(folders.Absolute("logs"), ct);
        Assert.All(blobs, blob => Assert.DoesNotContain(logs, content => content.Contains(blob, StringComparison.Ordinal)));
    }

    /// <summary>
    /// AC-002 (FR-003; review m-3): beside an older readable key, the newest key — the one the key ring would make the
    /// default — cannot be decrypted. What it protected is refused, the host keeps protecting and unprotecting, and
    /// neither file is touched.
    /// </summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task AnUndecryptableNewestKey_BesideAReadableOlderOne_IsNotUsed(BuiltHostKind host)
    {
        using var folders = new HostFolders();
        var keys = folders.Absolute("keys");
        var applicationName = BuiltHost.DataProtectionApplicationName(host);
        var now = DateTimeOffset.UtcNow;
        KeyRingFiles.CreateKey(keys, applicationName, now.AddDays(-10), now.AddDays(80));
        var newest = KeyRingFiles.CreateKey(keys, applicationName, now.AddDays(-1), now.AddDays(89));
        var earlier = KeyRingFiles.Protect(keys, applicationName, Purpose, "earlier");
        Assert.Equal(newest, KeyRingFiles.KeyIdOf(earlier));
        KeyRingFiles.MakeUndecryptable(KeyRingFiles.PathOf(keys, newest));
        var before = KeyRingFiles.Snapshot(keys);

        await using (var app = Build(host, folders))
        {
            var protector = Protector(app);
            Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(earlier));
            var later = protector.Protect("later");
            Assert.NotEqual(newest, KeyRingFiles.KeyIdOf(later));
            Assert.Equal("later", protector.Unprotect(later));
        }

        KeyRingFiles.AssertUnchanged(keys, before);
        Assert.All(KeyRingFiles.NewSince(keys, before), key => Assert.True(key.IsEncryptedForCurrentUser, key.Describe()));
    }

    /// <summary>AC-003 (FR-006): a host restarted under the same account decrypts its own encrypted keys and needs no new one.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task RestartedUnderTheSameAccount_TheHostReadsItsOwnEncryptedKeys(BuiltHostKind host)
    {
        using var folders = new HostFolders();
        var keys = folders.Absolute("keys");
        string earlier;
        await using (var first = Build(host, folders))
        {
            earlier = Protector(first).Protect("earlier");
        }

        KeyRingFiles.AssertAllEncryptedForCurrentUser(keys);
        var before = KeyRingFiles.Snapshot(keys);

        await using (var second = Build(host, folders))
        {
            Assert.Equal("earlier", Protector(second).Unprotect(earlier));
        }

        KeyRingFiles.AssertUnchanged(keys, before);
        Assert.Empty(KeyRingFiles.NewSince(keys, before));
    }

    /// <summary>
    /// AC-005 (FR-004, OD-002 a): a still-valid key written in plain form before this Story is read and left exactly as
    /// it is; the next key the host creates is encrypted.
    /// </summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task AStillValidPlainKey_IsReadAndLeftAsItIs_AndTheNextKeyIsEncrypted(BuiltHostKind host)
    {
        using var folders = new HostFolders();
        var keys = folders.Absolute("keys");
        var earlier = KeyRingFiles.WriteKeyAndProtect(keys, BuiltHost.DataProtectionApplicationName(host), encrypted: false, Purpose, "earlier");
        Assert.All(KeyRingFiles.Read(keys), key => Assert.True(key.HasPlainKeyMaterial, key.Describe()));
        var before = KeyRingFiles.Snapshot(keys);

        await using (var app = Build(host, folders))
        {
            Assert.Equal("earlier", Protector(app).Unprotect(earlier));
            var now = DateTimeOffset.UtcNow;
            app.Services.GetRequiredService<IKeyManager>().CreateNewKey(now, now.AddDays(90));
        }

        KeyRingFiles.AssertUnchanged(keys, before);
        var created = KeyRingFiles.NewSince(keys, before);
        Assert.Single(created);
        Assert.True(created[0].IsEncryptedForCurrentUser, created[0].Describe());
    }

    private static WebApplication Build(BuiltHostKind host, HostFolders folders) =>
        BuiltHost.Build(host, runsAsService: false, folders.Absolute("logs"), folders.Absolute("keys"));

    private static IDataProtector Protector(WebApplication app) =>
        app.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose);
}
