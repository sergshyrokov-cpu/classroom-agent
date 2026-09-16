namespace ClassroomAgent.ControlPlane.Services;

/// <summary>
/// Remembers that the Owner account exists. Only "exists" is cached — the account can
/// never disappear — so "no Owner" is always re-read from the database (api-design §3).
/// </summary>
public sealed class OwnerExistenceCache
{
    private volatile bool _exists;

    public bool Exists => _exists;

    public void MarkExists() => _exists = true;
}
