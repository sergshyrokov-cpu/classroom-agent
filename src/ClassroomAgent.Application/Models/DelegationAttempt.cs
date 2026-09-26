namespace ClassroomAgent.Application.Models;

/// <summary>
/// The answer to one delegated-token request: its outcome and, only when it is
/// <see cref="AccessCheckStepOutcome.Succeeded"/>, the token (US-011 spec FR-002).
/// </summary>
public sealed record DelegationAttempt(AccessCheckStepOutcome Outcome, DelegatedToken? Token);
