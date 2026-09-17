namespace ClassroomAgent.ControlPlane.Services;

/// <summary>
/// Result of <see cref="InstallationRegistry.RegisterAsync"/> (entity model §4): the new
/// identifier, or which of domain and client ID are already registered.
/// </summary>
public sealed record RegisterInstallationResult(Guid? Identifier, bool DomainTaken, bool ClientIdTaken)
{
    public static RegisterInstallationResult Registered(Guid identifier) => new(identifier, false, false);

    public static RegisterInstallationResult Conflict(bool domainTaken, bool clientIdTaken) => new(null, domainTaken, clientIdTaken);
}
