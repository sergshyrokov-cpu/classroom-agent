using System.Security.Cryptography;

namespace ClassroomAgent.Domain.Entities;

/// <summary>
/// The installation's salt for email fingerprints (US-038 entity model §2, db-design §3; spec FR-010): 32 random
/// bytes generated once by the installation itself and never changed — a new salt would make every exclusion
/// unmatchable. Never a configuration value, never shown, logged, exported or sent anywhere (spec S-05).
/// </summary>
/// <remarks>One row per installation database, through the shadow <c>singleton</c> of <c>SyncState</c>.</remarks>
public sealed class ErasureSalt
{
    public const int Length = 32;

    private ErasureSalt()
    {
        Value = [];
    }

    public long Id { get; private set; }

    /// <summary>Column <c>salt</c>.</summary>
    public byte[] Value { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>A new salt of <see cref="Length"/> bytes from a cryptographic random generator.</summary>
    public static ErasureSalt Generate() => new() { Value = RandomNumberGenerator.GetBytes(Length) };
}
