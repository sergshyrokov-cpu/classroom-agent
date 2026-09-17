using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassroomAgent.ControlPlane.Persistence.Configurations;

/// <summary>
/// Table <c>allowed_admin</c> exactly as US-003 db-design §3 defines it. The unique index name
/// is the contract the service maps to a conflict; the triggers live in the migration.
/// </summary>
public sealed class AllowedAdminConfiguration : IEntityTypeConfiguration<AllowedAdmin>
{
    public const string InstallationEmailUniqueIndex = "uq_allowed_admin_installation_email";

    public void Configure(EntityTypeBuilder<AllowedAdmin> builder)
    {
        builder.ToTable("allowed_admin", table =>
        {
            table.HasCheckConstraint("ck_allowed_admin_email_lower", "email = lower(email)");
            table.HasCheckConstraint(
                "ck_allowed_admin_email_format",
                "char_length(email) BETWEEN 3 AND 254 AND email ~ '^[a-z0-9._''-]{1,64}@[a-z0-9.-]+$'");
        });

        builder.HasKey(a => a.Id).HasName("pk_allowed_admin");
        builder.Property(a => a.Id).UseIdentityByDefaultColumn();
        builder.Property(a => a.Identifier).IsRequired().ValueGeneratedNever();
        builder.Property(a => a.InstallationId).IsRequired();
        builder.Property(a => a.Email).HasMaxLength(254).IsRequired();
        builder.Property(a => a.AddedByOwnerId).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt).IsRequired();

        builder.HasOne<Installation>()
            .WithMany()
            .HasForeignKey(a => a.InstallationId)
            .HasConstraintName("fk_allowed_admin_installation")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Owner>()
            .WithMany()
            .HasForeignKey(a => a.AddedByOwnerId)
            .HasConstraintName("fk_allowed_admin_added_by_owner")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.Identifier).IsUnique().HasDatabaseName("uq_allowed_admin_identifier");
        builder.HasIndex(a => new { a.InstallationId, a.Email }).IsUnique().HasDatabaseName(InstallationEmailUniqueIndex);
        builder.HasIndex(a => a.AddedByOwnerId).HasDatabaseName("ix_allowed_admin_added_by_owner_id");
    }
}
