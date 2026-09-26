namespace ClassroomAgent.Application.Models;

/// <summary>
/// One step of a check as the result carries it (US-011 spec FR-001; openapi <c>AccessCheckStep</c>). It holds
/// nothing Google returned — only which step it was and how it ended (spec S-08).
/// </summary>
/// <param name="Kind">What the step did.</param>
/// <param name="Scope">The full scope URI of a delegation step; null for a read.</param>
/// <param name="Outcome">How it ended (spec FR-005).</param>
/// <param name="NotAttemptedBecause">For <see cref="AccessCheckStepOutcome.NotAttempted"/>, the outcome that made it impossible.</param>
public sealed record AccessCheckStep(
    AccessCheckStepKind Kind,
    string? Scope,
    AccessCheckStepOutcome Outcome,
    AccessCheckStepOutcome? NotAttemptedBecause);
