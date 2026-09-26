using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The one password policy of SC-2, shared by every place US-012 sets a password — creation, reset, the forced
/// change and the voluntary change (spec FR-005, VR-002). It is a pure function of the password and the login:
/// no clock, no database, no configuration.
/// </summary>
/// <remarks>US-012 TEST_WRITING skeleton (OD-005) — IMPLEMENTATION writes the body.</remarks>
public static class DeanPasswordPolicy
{
    /// <summary>The shortest password SC-2 accepts, in characters.</summary>
    public const int MinimumLength = 15;

    /// <summary>The longest password SC-2 accepts, in characters.</summary>
    public const int MaximumLength = 128;

    /// <summary>The shortest login or local part the containment rule applies to (SC-2 v65).</summary>
    public const int ShortestComparedSubstring = 4;

    /// <summary>The violation, or null when the password is acceptable.</summary>
    public static PasswordPolicyViolation? Check(string password, string login) =>
        throw new NotImplementedException("US-012 IMPLEMENTATION (spec FR-005).");
}
