namespace ClassroomAgent.ControlPlane.Services;

/// <summary>
/// Outcome of an Owner sign-in (entity model §5). A refusal carries no reason, so
/// the three refusals stay indistinguishable to presentation (FR-008).
/// </summary>
public enum OwnerSignInOutcome
{
    SignedIn,
    Refused,
}
