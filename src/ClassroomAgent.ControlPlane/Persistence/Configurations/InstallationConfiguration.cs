using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassroomAgent.ControlPlane.Persistence.Configurations;

/// <summary>
/// Table <c>installation</c> exactly as US-002 db-design §3 defines it. The unique index
/// names are the contract the services map to conflicts; the triggers live in the migration.
/// </summary>
public sealed class InstallationConfiguration : IEntityTypeConfiguration<Installation>
{
    public const string DomainUniqueIndex = "uq_installation_domain";

    public const string ClientIdUniqueIndex = "uq_installation_client_id";

    /// <summary>
    /// US-006 db-design §3.1: the canonical push address shape — <c>http://</c>, a lower-case DNS name,
    /// IPv4 literal or bracketed IPv6 literal, an explicit port without leading zeros, nothing else. The
    /// finer rules (length, port range, label structure) stay in the request validation (VR-001).
    /// </summary>
    public const string PushAddressFormatCheck = "ck_installation_push_address_format";

    public const string PushAddressFormatExpression =
        "push_address IS NULL OR push_address ~ '^http://(\\[[0-9a-f:.]+\\]|[a-z0-9.-]{1,253}):[1-9][0-9]{0,4}$'";

    public void Configure(EntityTypeBuilder<Installation> builder)
    {
        builder.ToTable("installation", table =>
        {
            table.HasCheckConstraint("ck_installation_name_length", "char_length(name) BETWEEN 1 AND 200");
            table.HasCheckConstraint("ck_installation_domain_lower", "domain = lower(domain)");
            table.HasCheckConstraint(
                "ck_installation_domain_format",
                "domain ~ '^[a-z0-9.-]{3,253}$' AND position('.' in domain) > 0");
            table.HasCheckConstraint("ck_installation_client_id_format", "client_id ~ '^[0-9]{10,32}$'");
            table.HasCheckConstraint("ck_installation_status", "status IN ('active', 'suspended')");
            table.HasCheckConstraint(PushAddressFormatCheck, PushAddressFormatExpression);
        });

        builder.HasKey(i => i.Id).HasName("pk_installation");
        builder.Property(i => i.Id).UseIdentityByDefaultColumn();
        builder.Property(i => i.Identifier).IsRequired().ValueGeneratedNever();
        builder.Property(i => i.Name).HasMaxLength(200).IsRequired();
        builder.Property(i => i.Domain).HasMaxLength(253).IsRequired();
        builder.Property(i => i.ClientId).HasMaxLength(32).IsRequired();
        builder.Property(i => i.Status)
            .HasMaxLength(16)
            .IsRequired()
            .HasConversion(v => StatusCode(v), code => StatusFromCode(code));
        builder.Property(i => i.PushAddress).HasMaxLength(255);
        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.UpdatedAt).IsRequired();

        builder.HasIndex(i => i.Identifier).IsUnique().HasDatabaseName("uq_installation_identifier");
        builder.HasIndex(i => i.Domain).IsUnique().HasDatabaseName(DomainUniqueIndex);
        builder.HasIndex(i => i.ClientId).IsUnique().HasDatabaseName(ClientIdUniqueIndex);
    }

    private static string StatusCode(InstallationStatus value) => value switch
    {
        InstallationStatus.Active => "active",
        InstallationStatus.Suspended => "suspended",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static InstallationStatus StatusFromCode(string code) => code switch
    {
        "active" => InstallationStatus.Active,
        "suspended" => InstallationStatus.Suspended,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };
}
