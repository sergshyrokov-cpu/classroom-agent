namespace ClassroomAgent.Application.Ports;

/// <summary>
/// The configured secret store (SC-7; US-008 OD-004). A reference in configuration names a secret; the store
/// resolves it. The secret itself is never in configuration, in the database, in a DTO, in a view or in a log.
/// </summary>
public interface ISecretStore
{
    /// <summary>The secret that reference names, or null when the store holds none under it.</summary>
    string? Resolve(string reference);
}
