namespace ClassroomAgent.Application.Ports;

/// <summary>
/// Hashing and verification of a Dean's password (US-012 spec FR-018, OD-002). The implementation in
/// <c>Infrastructure</c> wraps <c>IPasswordHasher&lt;AppUser&gt;</c> from
/// <c>Microsoft.Extensions.Identity.Core</c>, so no Identity type crosses into <c>Application</c> or
/// <c>Domain</c> (AD-4).
/// </summary>
public interface IPasswordHasher
{
    /// <summary>The hash to store. The plaintext is never stored, logged or returned (SC-10).</summary>
    string Hash(string password);

    /// <summary>Whether the password matches the stored hash.</summary>
    bool Verify(string hash, string password);
}
