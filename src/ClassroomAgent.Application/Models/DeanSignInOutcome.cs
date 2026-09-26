namespace ClassroomAgent.Application.Models;

/// <summary>
/// The result of one sign-in attempt (US-012 spec FR-012). It carries the account id only where one was
/// found — and never the login that was typed, which SC-11 forbids recording anywhere.
/// </summary>
public sealed record DeanSignInOutcome(DeanSignInResult Result, long? AccountId);
