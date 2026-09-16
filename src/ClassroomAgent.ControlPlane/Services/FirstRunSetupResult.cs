namespace ClassroomAgent.ControlPlane.Services;

/// <summary>Result of <see cref="FirstRunSetupService.CreateOwnerAsync"/> (entity model §5).</summary>
public sealed record FirstRunSetupResult(FirstRunSetupOutcome Outcome, OwnerSessionDto? Session);
