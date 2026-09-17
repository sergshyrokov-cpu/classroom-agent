namespace ClassroomAgent.ControlPlane.Persistence;

/// <summary>
/// An email permitted to be Admin of one installation, with who added it and when (US-003
/// entity model §2.1). Never updated; revocation deletes the row (db-design §3.3).
/// </summary>
public class AllowedAdmin
{
    private AllowedAdmin()
    {
    }

    public long Id { get; private set; }

    public Guid Identifier { get; private set; }

    public long InstallationId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public long AddedByOwnerId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>A new entry; the email is already validated and in the installation's domain.</summary>
    public static AllowedAdmin Add(long installationId, string email, long addedByOwnerId) =>
        new()
        {
            Identifier = Guid.NewGuid(),
            InstallationId = installationId,
            Email = email.ToLowerInvariant(),
            AddedByOwnerId = addedByOwnerId,
        };
}
