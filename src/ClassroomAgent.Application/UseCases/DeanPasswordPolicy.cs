using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The one password policy of SC-2, shared by every place US-012 sets a password — creation, reset, the forced
/// change and the voluntary change (spec FR-005, VR-002). It is a pure function of the password and the login:
/// no clock, no database, no configuration.
/// </summary>
public static class DeanPasswordPolicy
{
    /// <summary>The shortest password SC-2 accepts, in characters.</summary>
    public const int MinimumLength = 15;

    /// <summary>The longest password SC-2 accepts, in characters.</summary>
    public const int MaximumLength = 128;

    /// <summary>The shortest login or local part the containment rule applies to (SC-2 v65).</summary>
    public const int ShortestComparedSubstring = 4;

    /// <summary>The violation, or null when the password is acceptable.</summary>
    public static PasswordPolicyViolation? Check(string password, string login)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(login);

        // Length first, counted in characters and not bytes; spaces count and are never trimmed (spec FR-005).
        if (password.Length < MinimumLength)
        {
            return PasswordPolicyViolation.TooShort;
        }

        if (password.Length > MaximumLength)
        {
            return PasswordPolicyViolation.TooLong;
        }

        // No composition rule is applied on purpose: adding one would be a requirement nobody wrote (S-06).
        var localPart = LocalPartOf(login);
        if (Equals(password, login) || Equals(password, localPart))
        {
            return PasswordPolicyViolation.EqualsLogin;
        }

        if (Contains(password, login) || Contains(password, localPart))
        {
            return PasswordPolicyViolation.ContainsLogin;
        }

        return null;
    }

    private static string LocalPartOf(string login)
    {
        var at = login.IndexOf('@');
        return at > 0 ? login[..at] : login;
    }

    private static bool Equals(string password, string other) =>
        other.Length > 0 && string.Equals(password, other, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Containment is checked only for a string of at least <see cref="ShortestComparedSubstring"/> characters:
    /// a shorter one would forbid almost any password, so SC-2 v65 compares it for equality only.
    /// </summary>
    private static bool Contains(string password, string other) =>
        other.Length >= ShortestComparedSubstring
        && password.Contains(other, StringComparison.OrdinalIgnoreCase);
}
