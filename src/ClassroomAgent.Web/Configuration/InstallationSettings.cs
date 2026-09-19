namespace ClassroomAgent.Web.Configuration;

/// <summary>
/// The installation's validated settings of US-005 spec FR-001 (api-design §10) and the private-port
/// address of US-006 spec FR-001. None of them is stored in the database.
/// </summary>
public sealed record InstallationSettings(
    Guid InstallationId,
    Uri ControlPlaneAddress,
    int PrivatePort,
    string PrivateAddress,
    string ConnectionString);
