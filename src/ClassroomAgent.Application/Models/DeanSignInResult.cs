namespace ClassroomAgent.Application.Models;

/// <summary>
/// Which step of the six-step sequence ended the attempt (US-012 spec FR-012; <c>trebovaniya.md</c> §2 v66,
/// SC-2). The order of the members is the order of the steps, and it is load-bearing: a caller must not be able
/// to tell <see cref="UnknownLogin"/>, <see cref="LockedOut"/> and <see cref="WrongPassword"/> apart (spec S-05).
/// </summary>
public enum DeanSignInResult
{
    /// <summary>Step 1: no account with that normalized email.</summary>
    UnknownLogin,

    /// <summary>Step 2: a lockout is in force; the password is not verified at all.</summary>
    LockedOut,

    /// <summary>Step 3: the password does not match the stored hash.</summary>
    WrongPassword,

    /// <summary>Step 4: the account is disabled; only reachable with the correct password and no lockout.</summary>
    AccountDisabled,

    /// <summary>Step 5: the password is temporary, so the forced change comes before a session.</summary>
    TemporaryPassword,

    /// <summary>Step 6: signed in.</summary>
    SignedIn,
}
