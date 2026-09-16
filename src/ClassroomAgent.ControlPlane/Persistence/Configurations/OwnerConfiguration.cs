using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassroomAgent.ControlPlane.Persistence.Configurations;

/// <summary>Table <c>owner</c> exactly as db-design §3 defines it (PC-4, PC-5).</summary>
public sealed class OwnerConfiguration : IEntityTypeConfiguration<Owner>
{
    private const string SingletonProperty = "Singleton";

    public void Configure(EntityTypeBuilder<Owner> builder)
    {
        builder.ToTable("owner", table =>
        {
            table.HasCheckConstraint("ck_owner_ui_language", "ui_language IN ('uk', 'en')");
            table.HasCheckConstraint("ck_owner_access_failed_count", "access_failed_count >= 0");
            table.HasCheckConstraint("ck_owner_singleton", "singleton = true");
        });

        builder.HasKey(o => o.Id).HasName("pk_owner");
        builder.Property(o => o.Id).UseIdentityByDefaultColumn();
        builder.Property(o => o.UserName).HasMaxLength(64).IsRequired();
        builder.Property(o => o.NormalizedUserName).HasMaxLength(64).IsRequired();
        builder.Property(o => o.PasswordHash).HasMaxLength(256).IsRequired();
        builder.Property(o => o.SecurityStamp).HasMaxLength(64).IsRequired();
        builder.Property(o => o.ConcurrencyStamp).HasMaxLength(64).IsRequired().IsConcurrencyToken();
        builder.Property(o => o.AccessFailedCount).IsRequired().HasDefaultValue(0);
        builder.Property(o => o.LockoutEnd);
        builder.Property(o => o.UiLanguage)
            .HasMaxLength(8)
            .IsRequired()
            .HasConversion(v => ToCode(v), code => FromCode(code));
        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.UpdatedAt).IsRequired();

        // Never set by code: the unset CLR value lets the database default apply (db-design §3.1).
        builder.Property<bool?>(SingletonProperty).IsRequired().HasDefaultValue(true);

        builder.HasIndex(o => o.NormalizedUserName).IsUnique().HasDatabaseName("uq_owner_normalized_user_name");
        builder.HasIndex(SingletonProperty).IsUnique().HasDatabaseName("uq_owner_singleton");
    }

    private static string ToCode(UiLanguage language) => language switch
    {
        UiLanguage.Uk => "uk",
        UiLanguage.En => "en",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
    };

    private static UiLanguage FromCode(string code) => code switch
    {
        "uk" => UiLanguage.Uk,
        "en" => UiLanguage.En,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };
}
