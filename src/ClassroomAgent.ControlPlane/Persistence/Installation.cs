namespace ClassroomAgent.ControlPlane.Persistence;

/// <summary>
/// A school registered by the Owner (entity model §2.1). The identifier and the domain
/// never change after registration, and nothing deletes an installation (db-design §3.3).
/// </summary>
public class Installation
{
    private Installation()
    {
    }

    public long Id { get; private set; }

    public Guid Identifier { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Domain { get; private set; } = string.Empty;

    public string ClientId { get; private set; } = string.Empty;

    public InstallationStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>A new active installation; the values are already validated by the request rules.</summary>
    public static Installation Register(string name, string domain, string clientId) =>
        new()
        {
            Identifier = Guid.NewGuid(),
            Name = name,
            Domain = domain.ToLowerInvariant(),
            ClientId = clientId,
            Status = InstallationStatus.Active,
        };

    public void Rename(string name) => Name = name;

    public void ChangeClientId(string clientId) => ClientId = clientId;
}
