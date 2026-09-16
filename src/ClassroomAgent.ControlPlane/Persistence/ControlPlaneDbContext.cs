using ClassroomAgent.ControlPlane.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.ControlPlane.Persistence;

/// <summary>
/// The Control Plane's own database context (db-design §2, AD-1). Used only from
/// <c>ControlPlane.Services</c> (AD-3).
/// </summary>
public class ControlPlaneDbContext(DbContextOptions<ControlPlaneDbContext> options) : DbContext(options)
{
    public DbSet<Owner> Owners => Set<Owner>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OwnerConfiguration());
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
    }
}
