namespace ClassroomAgent.ControlPlane.Services;

/// <summary>Outcome of a first-run setup submission (entity model §5).</summary>
public enum FirstRunSetupOutcome
{
    Created,
    WrongSetupCode,
    AlreadyExists,
}
