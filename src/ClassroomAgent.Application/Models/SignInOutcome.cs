namespace ClassroomAgent.Application.Models;

/// <summary>
/// The decision of one sign-in attempt (US-008 spec FR-010): the account to issue a session for, or the refusal
/// as data. It holds no HTTP concept and no user-visible string (AD-3, AD-6, AD-9): a refused sign-in is an
/// expected outcome, never an exception.
/// </summary>
public sealed record SignInOutcome(SignInRefusal? Refusal, SignedInUser? User)
{
    public bool Succeeded => Refusal is null;

    public static SignInOutcome Success(SignedInUser user) => new(null, user);

    public static SignInOutcome Refused(SignInRefusal refusal) => new(refusal, null);
}
