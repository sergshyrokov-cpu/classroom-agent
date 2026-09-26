namespace ClassroomAgent.Application.Models;

/// <summary>
/// Which rule of the SC-2 password policy a submitted password breaks (US-012 spec FR-005, VR-002). The closed
/// list is the policy itself: a rule outside it would be a requirement nobody wrote.
/// </summary>
public enum PasswordPolicyViolation
{
    /// <summary>Fewer than 15 characters (spec FR-005).</summary>
    TooShort,

    /// <summary>More than 128 characters (spec FR-005).</summary>
    TooLong,

    /// <summary>Equal to the login, compared case-insensitively (spec FR-005).</summary>
    EqualsLogin,

    /// <summary>
    /// Contains the login or the part of the email before <c>@</c>, compared case-insensitively. Checked only
    /// when that string is at least 4 characters long (spec FR-005, SC-2 v65).
    /// </summary>
    ContainsLogin,

    /// <summary>Equal to the temporary password it replaces (spec VR-003, BR-014 v64).</summary>
    EqualsTemporaryPassword,
}
