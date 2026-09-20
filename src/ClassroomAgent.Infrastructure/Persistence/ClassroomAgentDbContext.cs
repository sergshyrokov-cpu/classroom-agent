using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence;

/// <summary>
/// The installation database context (US-005 entity model §3.4; PC-1): one database per school, never
/// shared with the Control Plane or another school (AD-1). Its schema changes only by migrations applied
/// at deployment (PC-2).
/// </summary>
public sealed class ClassroomAgentDbContext(DbContextOptions<ClassroomAgentDbContext> options) : DbContext(options)
{
    public DbSet<LegitimacyState> LegitimacyStates => Set<LegitimacyState>();

    /// <summary>US-008 entity model §3.3: the installation's accounts.</summary>
    public DbSet<AppUser> AppUsers => Set<AppUser>();

    /// <summary>US-008 entity model §3.3: the installation's own audit trail.</summary>
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new LegitimacyStateConfiguration());
        modelBuilder.ApplyConfiguration(new AppUserConfiguration());
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
    }
}
