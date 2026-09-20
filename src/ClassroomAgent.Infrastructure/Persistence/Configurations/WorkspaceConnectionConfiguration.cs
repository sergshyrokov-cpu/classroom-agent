using ClassroomAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassroomAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table <c>workspace_connection</c> exactly as US-009 db-design §3 defines it: three business columns, the
/// singleton pattern US-005 established for <c>legitimacy_state</c>, and nine check constraints.
/// </summary>
/// <remarks>
/// <b>No column holds a key, a secret or a reference to either</b> — the service-account key lives in the
/// secret store and its reference in configuration (PC-9, SC-7). <c>ck_workspace_connection_email_domain</c>
/// makes the row agree with itself; BR-020, which compares against <c>legitimacy_state</c>, stays in
/// <c>Application</c> (db-design §3.1).
/// </remarks>
public sealed class WorkspaceConnectionConfiguration : IEntityTypeConfiguration<WorkspaceConnection>
{
    /// <summary>The shadow property of the <c>singleton</c> column; every row sets it to true.</summary>
    public const string Singleton = "Singleton";

    public void Configure(EntityTypeBuilder<WorkspaceConnection> builder)
    {
        builder.ToTable("workspace_connection", table =>
        {
            table.HasCheckConstraint("ck_workspace_connection_singleton", "singleton");
            table.HasCheckConstraint(
                "ck_workspace_connection_domain_length",
                "char_length(domain) BETWEEN 3 AND 253");
            table.HasCheckConstraint("ck_workspace_connection_domain_lowercase", "domain = lower(domain)");
            table.HasCheckConstraint(
                "ck_workspace_connection_domain_format",
                @"domain ~ '^[a-z0-9]([a-z0-9-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)+$'");
            table.HasCheckConstraint(
                "ck_workspace_connection_email_length",
                "char_length(impersonation_user_email) BETWEEN 3 AND 254");
            table.HasCheckConstraint(
                "ck_workspace_connection_email_lowercase",
                "impersonation_user_email = lower(impersonation_user_email)");
            table.HasCheckConstraint(
                "ck_workspace_connection_email_format",
                "impersonation_user_email ~ '^[^@[:space:]]+@[^@[:space:]]+$'");
            table.HasCheckConstraint(
                "ck_workspace_connection_email_domain",
                "split_part(impersonation_user_email, '@', 2) = domain");
        });

        builder.HasKey(c => c.Id).HasName("pk_workspace_connection");
        builder.Property(c => c.Id).UseIdentityByDefaultColumn();
        builder.Property<bool>(Singleton).IsRequired().HasDefaultValue(true);
        builder.Property(c => c.Domain).HasMaxLength(253).IsRequired();
        builder.Property(c => c.ImpersonationUserEmail).HasMaxLength(254).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();

        builder.HasIndex(Singleton).IsUnique().HasDatabaseName("uq_workspace_connection_singleton");
    }
}
