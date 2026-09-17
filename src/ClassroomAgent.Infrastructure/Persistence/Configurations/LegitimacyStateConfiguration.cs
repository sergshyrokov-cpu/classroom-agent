using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassroomAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table <c>legitimacy_state</c> exactly as US-005 db-design §4 defines it. The shadow <c>singleton</c>
/// column, unique and always true, keeps the table at zero or one row.
/// </summary>
public sealed class LegitimacyStateConfiguration : IEntityTypeConfiguration<LegitimacyState>
{
    /// <summary>The shadow property of the <c>singleton</c> column; every row sets it to true.</summary>
    public const string Singleton = "Singleton";

    public void Configure(EntityTypeBuilder<LegitimacyState> builder)
    {
        builder.ToTable("legitimacy_state", table =>
        {
            table.HasCheckConstraint("ck_legitimacy_state_singleton", "singleton");
            table.HasCheckConstraint("ck_legitimacy_state_status", "status IN ('active', 'suspended')");
            table.HasCheckConstraint(
                "ck_legitimacy_state_compatibility",
                "compatibility IN ('supported', 'upgrade_recommended', 'upgrade_required')");
            table.HasCheckConstraint("ck_legitimacy_state_domain_length", "char_length(domain) BETWEEN 3 AND 253");
            table.HasCheckConstraint("ck_legitimacy_state_client_id_format", "client_id ~ '^[0-9]{10,32}$'");
        });

        builder.HasKey(s => s.Id).HasName("pk_legitimacy_state");
        builder.Property(s => s.Id).UseIdentityByDefaultColumn();
        builder.Property<bool>(Singleton).IsRequired().HasDefaultValue(true);
        builder.Property(s => s.LastSuccessfulCheckAt);
        builder.Property(s => s.Status)
            .HasMaxLength(16)
            .IsRequired()
            .HasConversion(v => StatusCode(v), code => StatusFromCode(code));
        builder.Property(s => s.Compatibility)
            .HasMaxLength(24)
            .IsRequired()
            .HasConversion(v => CompatibilityCode(v), code => CompatibilityFromCode(code));
        builder.Property(s => s.Domain).HasMaxLength(253).IsRequired();
        builder.Property(s => s.ClientId).HasMaxLength(32).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();

        builder.HasIndex(Singleton).IsUnique().HasDatabaseName("uq_legitimacy_state_singleton");
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

    private static string CompatibilityCode(CompatibilityState value) => value switch
    {
        CompatibilityState.Supported => "supported",
        CompatibilityState.UpgradeRecommended => "upgrade_recommended",
        CompatibilityState.UpgradeRequired => "upgrade_required",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static CompatibilityState CompatibilityFromCode(string code) => code switch
    {
        "supported" => CompatibilityState.Supported,
        "upgrade_recommended" => CompatibilityState.UpgradeRecommended,
        "upgrade_required" => CompatibilityState.UpgradeRequired,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };
}
