using System.Security.Cryptography;
using System.Text;

namespace ClassroomAgent.ControlPlane.Services;

/// <summary>
/// Compares a submitted setup code with the current one: hyphens, whitespace and letter
/// case are ignored, and the comparison runs in constant time (OD-005).
/// </summary>
public static class SetupCodeComparer
{
    public static bool Matches(string expected, string? submitted)
    {
        if (string.IsNullOrWhiteSpace(submitted))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(Normalize(expected));
        var submittedBytes = Encoding.UTF8.GetBytes(Normalize(submitted));
        return CryptographicOperations.FixedTimeEquals(expectedBytes, submittedBytes);
    }

    private static string Normalize(string code)
    {
        var builder = new StringBuilder(code.Length);
        foreach (var character in code)
        {
            if (character != '-' && !char.IsWhiteSpace(character))
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.ToString();
    }
}
