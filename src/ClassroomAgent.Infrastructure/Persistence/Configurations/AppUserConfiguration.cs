using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassroomAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table <c>app_user</c> exactly as US-008 db-design §3 defines it. Two of its check constraints are
/// load-bearing together: <c>ck_app_user_role_sign_in_method</c> and <c>ck_app_user_password_hash</c> make an
/// Admin row carrying any password hash — an empty string included — impossible in the database itself
/// (S-04, BR-010, PC-9).
/// </summary>
public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("app_user", table =>
        {
            table.HasCheckConstraint("ck_app_user_role", "role IN ('admin', 'dean')");
            table.HasCheckConstraint("ck_app_user_sign_in_method", "sign_in_method IN ('google', 'password')");
            table.HasCheckConstraint(
                "ck_app_user_role_sign_in_method",
                "(role = 'admin') = (sign_in_method = 'google')");
            table.HasCheckConstraint(
                "ck_app_user_password_hash",
                "(sign_in_method = 'password') = (password_hash IS NOT NULL)");
            table.HasCheckConstraint("ck_app_user_ui_language", "ui_language IN ('uk', 'en')");
            table.HasCheckConstraint("ck_app_user_access_failed_count", "access_failed_count >= 0");
            table.HasCheckConstraint(
                "ck_app_user_email_lowercase",
                "email = lower(email) AND normalized_email = lower(normalized_email)");
        });

        builder.HasKey(u => u.Id).HasName("pk_app_user");
        builder.Property(u => u.Id).UseIdentityByDefaultColumn();
        builder.Property(u => u.Email).HasMaxLength(254).IsRequired();
        builder.Property(u => u.NormalizedEmail).HasMaxLength(254).IsRequired();
        builder.Property(u => u.Role)
            .HasMaxLength(16)
            .IsRequired()
            .HasConversion(v => RoleCode(v), code => RoleFromCode(code));
        builder.Property(u => u.SignInMethod)
            .HasMaxLength(16)
            .IsRequired()
            .HasConversion(v => SignInMethodCode(v), code => SignInMethodFromCode(code));

        // Nullable, and null for every Admin (S-04).
        builder.Property(u => u.PasswordHash).HasMaxLength(256);
        builder.Property(u => u.SecurityStamp).HasMaxLength(64).IsRequired();
        builder.Property(u => u.ConcurrencyStamp).HasMaxLength(64).IsRequired().IsConcurrencyToken();
        builder.Property(u => u.AccessFailedCount).IsRequired().HasDefaultValue(0);
        builder.Property(u => u.LockoutEnd);
        builder.Property(u => u.UiLanguage)
            .HasMaxLength(8)
            .IsRequired()
            .HasConversion(v => LanguageCode(v), code => LanguageFromCode(code));
        builder.Property(u => u.IsDisabled).IsRequired().HasDefaultValue(false);
        builder.Property(u => u.LastSuccessfulSignInAt);
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.UpdatedAt).IsRequired();

        // One account per address, enforced by the database and not only by a check before insert (AC-007). It
        // is also the only lookup this Story performs, so PC-7 needs no second index; an index on
        // last_successful_sign_in_at belongs to the retention purge (EPIC-10), with its query.
        builder.HasIndex(u => u.NormalizedEmail).IsUnique().HasDatabaseName("uq_app_user_normalized_email");
    }

    private static string RoleCode(AppRole value) => value switch
    {
        AppRole.Admin => "admin",
        AppRole.Dean => "dean",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static AppRole RoleFromCode(string code) => code switch
    {
        "admin" => AppRole.Admin,
        "dean" => AppRole.Dean,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };

    private static string SignInMethodCode(SignInMethod value) => value switch
    {
        SignInMethod.Google => "google",
        SignInMethod.Password => "password",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static SignInMethod SignInMethodFromCode(string code) => code switch
    {
        "google" => SignInMethod.Google,
        "password" => SignInMethod.Password,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };

    private static string LanguageCode(UiLanguage value) => value switch
    {
        UiLanguage.Uk => "uk",
        UiLanguage.En => "en",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static UiLanguage LanguageFromCode(string code) => code switch
    {
        "uk" => UiLanguage.Uk,
        "en" => UiLanguage.En,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };
}
