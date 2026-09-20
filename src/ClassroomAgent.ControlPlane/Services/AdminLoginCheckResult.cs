namespace ClassroomAgent.ControlPlane.Services;

/// <summary>The outcome of one Admin login check (US-008 spec FR-009). Carries nothing else (SC-12, S-16).</summary>
public enum AdminLoginCheckResult
{
    Allowed,
    NotAllowed,
    UnknownInstallation,
}
