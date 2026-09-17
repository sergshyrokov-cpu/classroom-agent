using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassroomAgent.ControlPlane.Persistence.Configurations;

/// <summary>
/// Table <c>instance_license_check</c> exactly as US-005 db-design §3 defines it. The unique index name is
/// the contract the service maps to a lost race of two first calls (db-design §3.2).
/// </summary>
public sealed class InstanceLicenseCheckConfiguration : IEntityTypeConfiguration<InstanceLicenseCheck>
{
    public const string InstallationUniqueIndex = "uq_instance_license_check_installation_id";

    public void Configure(EntityTypeBuilder<InstanceLicenseCheck> builder)
    {
        builder.ToTable("instance_license_check", table =>
        {
            table.HasCheckConstraint(
                "ck_instance_license_check_application_version",
                @"application_version ~ '^(0|[1-9][0-9]{0,5})\.(0|[1-9][0-9]{0,5})\.(0|[1-9][0-9]{0,5})$'");
            table.HasCheckConstraint("ck_instance_license_check_contract_version", "contract_version BETWEEN 1 AND 999999");
            table.HasCheckConstraint("ck_instance_license_check_answered_status", "answered_status IN ('active', 'suspended')");
            table.HasCheckConstraint(
                "ck_instance_license_check_answered_compatibility",
                "answered_compatibility IN ('supported', 'upgrade_recommended', 'upgrade_required')");
        });

        builder.HasKey(c => c.Id).HasName("pk_instance_license_check");
        builder.Property(c => c.Id).UseIdentityByDefaultColumn();
        builder.Property(c => c.InstallationId).IsRequired();
        builder.Property(c => c.AnsweredAt).IsRequired();
        builder.Property(c => c.ApplicationVersion).HasMaxLength(20).IsRequired();
        builder.Property(c => c.ContractVersion).IsRequired();
        builder.Property(c => c.AnsweredStatus)
            .HasMaxLength(16)
            .IsRequired()
            .HasConversion(v => StatusCode(v), code => StatusFromCode(code));
        builder.Property(c => c.AnsweredCompatibility)
            .HasMaxLength(24)
            .IsRequired()
            .HasConversion(v => CompatibilityCode(v), code => CompatibilityFromCode(code));
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();

        builder.HasOne<Installation>()
            .WithOne()
            .HasForeignKey<InstanceLicenseCheck>(c => c.InstallationId)
            .HasConstraintName("fk_instance_license_check_installation")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.InstallationId).IsUnique().HasDatabaseName(InstallationUniqueIndex);
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
