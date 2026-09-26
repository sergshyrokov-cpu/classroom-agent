namespace ClassroomAgent.Application.Models;

/// <summary>
/// The result of one check (US-011 spec FR-001): the verdict and exactly eight steps in the fixed order. Never
/// stored (OD-003).
/// </summary>
public sealed record AccessCheckResult(AccessCheckVerdict Verdict, IReadOnlyList<AccessCheckStep> Steps)
{
    /// <summary>The outcomes of a step that name something wrong with the configuration (spec FR-005).</summary>
    public static bool IsConfigurationFailure(AccessCheckStepOutcome outcome) =>
        outcome is not (AccessCheckStepOutcome.Succeeded
            or AccessCheckStepOutcome.GoogleUnavailable
            or AccessCheckStepOutcome.NotAttempted);

    /// <summary>Spec FR-001: the verdict of a set of steps.</summary>
    public static AccessCheckResult Of(IReadOnlyList<AccessCheckStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        var verdict = steps.All(s => s.Outcome == AccessCheckStepOutcome.Succeeded)
            ? AccessCheckVerdict.AccessInPlace
            : steps.Any(s => IsConfigurationFailure(s.Outcome))
                ? AccessCheckVerdict.NotConfigured
                : AccessCheckVerdict.Inconclusive;
        return new AccessCheckResult(verdict, steps);
    }
}
