namespace ClassroomAgent.Application.Models;

/// <summary>
/// What the check-access use case returns (US-011 spec FR-006, AD-9): the result of a carried-out check, or the
/// refusal of an unusable connection. The read-only refusal is <c>ReadOnlyModeException</c>, not an outcome.
/// </summary>
public sealed class RunAccessCheckOutcome
{
    private RunAccessCheckOutcome(AccessCheckResult? result, AccessCheckRefusal? refusal)
    {
        Result = result;
        Refusal = refusal;
    }

    public AccessCheckResult? Result { get; }

    public AccessCheckRefusal? Refusal { get; }

    public static RunAccessCheckOutcome Ran(AccessCheckResult result) => new(result, null);

    public static RunAccessCheckOutcome Refused(AccessCheckRefusal refusal) => new(null, refusal);
}
