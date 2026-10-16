namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-049: what one Data Protection key file in a key directory holds, read from its XML — whether plain key material
/// is present, which decryptor the key names, and the DPAPI blob when the key is DPAPI-encrypted (spec VR-002).
/// </summary>
public sealed record KeyFile(string Path, bool HasPlainKeyMaterial, string? DecryptorType, byte[]? DpapiBlob)
{
    /// <summary>The decryptor ASP.NET Core records for a key encrypted with Windows DPAPI.</summary>
    public const string DpapiDecryptorTypeName = "Microsoft.AspNetCore.DataProtection.XmlEncryption.DpapiXmlDecryptor";

    /// <summary>
    /// <c>CRYPTPROTECT_LOCAL_MACHINE</c>: the DPAPI blob's flags word carries it when the secret is bound to the
    /// machine rather than the user (spec VR-001).
    /// </summary>
    public const uint DpapiLocalMachineFlag = 0x4;

    /// <summary>The flags word of a DPAPI blob: version (4), provider GUID (16), master-key version (4), master-key GUID (16).</summary>
    private const int DpapiFlagsOffset = 40;

    /// <summary>The DPAPI flags of the blob; null when the key is not DPAPI-encrypted.</summary>
    public uint? DpapiFlags =>
        DpapiBlob is { Length: >= DpapiFlagsOffset + 4 } blob ? BitConverter.ToUInt32(blob, DpapiFlagsOffset) : null;

    /// <summary>Spec FR-001, FR-002, VR-001, VR-002: DPAPI-encrypted, bound to the current user, no plain key material.</summary>
    public bool IsEncryptedForCurrentUser =>
        !HasPlainKeyMaterial
        && DecryptorType is not null
        && DecryptorType.StartsWith(DpapiDecryptorTypeName, StringComparison.Ordinal)
        && DpapiFlags is { } flags
        && (flags & DpapiLocalMachineFlag) == 0;

    /// <summary>A one-line description for an assertion message — never the key material itself.</summary>
    public string Describe() =>
        $"{System.IO.Path.GetFileName(Path)}: plain={HasPlainKeyMaterial}, decryptor={DecryptorType ?? "none"}, " +
        $"dpapiFlags={(DpapiFlags is { } flags ? flags.ToString("X8", System.Globalization.CultureInfo.InvariantCulture) : "none")}";
}
