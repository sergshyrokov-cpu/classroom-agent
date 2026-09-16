using System.Security.Cryptography;

namespace ClassroomAgent.ControlPlane.Services;

/// <summary>
/// The one-time setup code: 26 Crockford Base32 characters (130 bits) from the OS
/// cryptographic RNG, shown in groups of 5, 5, 4, 4, 4, 4 separated by hyphens (OD-005).
/// </summary>
public class SetupCodeGenerator : ISetupCodeGenerator
{
    private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private static readonly int[] GroupLengths = [5, 5, 4, 4, 4, 4];

    public string Generate()
    {
        var characters = RandomNumberGenerator.GetString(CrockfordAlphabet, GroupLengths.Sum());
        var groups = new List<string>(GroupLengths.Length);
        var start = 0;
        foreach (var length in GroupLengths)
        {
            groups.Add(characters.Substring(start, length));
            start += length;
        }

        return string.Join('-', groups);
    }
}
