namespace ClassroomAgent.ControlPlane.Services;

/// <summary>Result of <see cref="OwnerSignInService.SignInAsync"/> (entity model §5).</summary>
public sealed record OwnerSignInResult(OwnerSignInOutcome Outcome, OwnerSessionDto? Session);
