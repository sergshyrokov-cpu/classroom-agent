namespace ClassroomAgent.ControlPlane.Persistence;

/// <summary>
/// The single Owner account of the service (entity model §2.1). Created only by the
/// first-run setup; lockout is always enabled and is not a column.
/// </summary>
public class Owner
{
    public long Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string NormalizedUserName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string SecurityStamp { get; set; } = string.Empty;

    public string ConcurrencyStamp { get; set; } = string.Empty;

    public int AccessFailedCount { get; set; }

    public DateTimeOffset? LockoutEnd { get; set; }

    public UiLanguage UiLanguage { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
