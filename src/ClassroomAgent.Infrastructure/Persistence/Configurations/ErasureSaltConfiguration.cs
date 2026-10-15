using ClassroomAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassroomAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table <c>erasure_salt</c> exactly as US-038 db-design §3 defines it: at most one row (the shadow <c>singleton</c>
/// of <see cref="SyncStateConfiguration"/>), 32 bytes, never changed. Not seeded: the first erasure creates it.
/// </summary>
public sealed class ErasureSaltConfiguration : IEntityTypeConfiguration<ErasureSalt>
{
    /// <summary>The shadow property of the <c>singleton</c> column; every row sets it to true.</summary>
    public const string Singleton = "Singleton";

    public void Configure(EntityTypeBuilder<ErasureSalt> builder)
    {
        builder.ToTable("erasure_salt", table =>
        {
            table.HasCheckConstraint("ck_erasure_salt_singleton", "singleton");
            table.HasCheckConstraint("ck_erasure_salt_values", $"octet_length(salt) = {ErasureSalt.Length}");
            table.HasCheckConstraint("ck_erasure_salt_immutable", "updated_at = created_at");
        });

        builder.HasKey(s => s.Id).HasName("pk_erasure_salt");
        builder.Property(s => s.Id).UseIdentityByDefaultColumn();
        builder.Property(s => s.Value).HasColumnName("salt").HasColumnType("bytea").IsRequired();
        builder.Property<bool>(Singleton).IsRequired().HasDefaultValue(true);
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();

        builder.HasIndex(Singleton).IsUnique().HasDatabaseName("uq_erasure_salt_singleton");
    }
}
