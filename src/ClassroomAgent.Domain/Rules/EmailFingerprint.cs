using System.Security.Cryptography;
using System.Text;
using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Domain.Rules;

/// <summary>
/// The salted fingerprint of an email (US-038 entity model §3; spec FR-010, VR-004): HMAC-SHA-256 keyed with the
/// installation's salt over the UTF-8 bytes of <see cref="WorkspaceConnection.NormalizeEmail"/>. The single place the
/// erasure, the lifting and the Meet load compute it, so all three agree.
/// </summary>
/// <remarks>Equality compares the bytes. <see cref="ToString"/> prints nothing of the value (spec S-05, S-06).</remarks>
public readonly record struct EmailFingerprint
{
    private EmailFingerprint(byte[] bytes)
    {
        Bytes = bytes;
    }

    public ReadOnlyMemory<byte> Bytes { get; }

    public static EmailFingerprint Of(string email, ErasureSalt salt)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(salt);

        var data = Encoding.UTF8.GetBytes(WorkspaceConnection.NormalizeEmail(email));
        return new EmailFingerprint(HMACSHA256.HashData(salt.Value, data));
    }

    public bool Equals(EmailFingerprint other) => Bytes.Span.SequenceEqual(other.Bytes.Span);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(Bytes.Span);
        return hash.ToHashCode();
    }

    public override string ToString() => nameof(EmailFingerprint);
}
