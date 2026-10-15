using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>The host's real hasher, counting the verifications one request makes (US-048 spec FR-006, AC-009).</summary>
public sealed class CountingPasswordHasher(IPasswordHasher inner) : IPasswordHasher
{
    public int Verifications { get; private set; }

    public string Hash(string password) => inner.Hash(password);

    public bool Verify(string hash, string password)
    {
        Verifications++;
        return inner.Verify(hash, password);
    }
}
