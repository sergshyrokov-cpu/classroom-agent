namespace ClassroomAgent.ControlPlane.Services;

/// <summary>How <see cref="AllowedAdminRegistry.AddAsync"/> ended (US-003 entity model §4).</summary>
public enum AddAllowedAdminOutcome
{
    Added,
    NotFound,
    WrongDomain,
    Taken,
}
