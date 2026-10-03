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

    /// <summary>US-009 entity model §3.3: the connection to the school's Workspace domain.</summary>
    public DbSet<WorkspaceConnection> WorkspaceConnections => Set<WorkspaceConnection>();

    /// <summary>US-013 entity model §1: the state of background synchronization, one row (db-design §3).</summary>
    public DbSet<SyncState> SyncStates => Set<SyncState>();

    /// <summary>US-014 entity model §1: the school's Classroom courses (db-design §3).</summary>
    public DbSet<Course> Courses => Set<Course>();

    /// <summary>US-014 entity model §3: the people synchronization brought in (db-design §4).</summary>
    public DbSet<ClassroomParticipant> ClassroomParticipants => Set<ClassroomParticipant>();

    /// <summary>US-014 entity model §4: one person's participation in one course (db-design §5).</summary>
    public DbSet<CourseMembership> CourseMemberships => Set<CourseMembership>();

    /// <summary>US-015 entity model §1: a course's items — assignments and materials (db-design §3).</summary>
    public DbSet<CourseWork> CourseWorks => Set<CourseWork>();

    /// <summary>US-015 entity model §4: one student's submission of one item (db-design §4).</summary>
    public DbSet<Submission> Submissions => Set<Submission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new LegitimacyStateConfiguration());
        modelBuilder.ApplyConfiguration(new AppUserConfiguration());
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
        modelBuilder.ApplyConfiguration(new WorkspaceConnectionConfiguration());
        modelBuilder.ApplyConfiguration(new SyncStateConfiguration());
        modelBuilder.ApplyConfiguration(new CourseConfiguration());
        modelBuilder.ApplyConfiguration(new ClassroomParticipantConfiguration());
        modelBuilder.ApplyConfiguration(new CourseMembershipConfiguration());
        modelBuilder.ApplyConfiguration(new CourseWorkConfiguration());
        modelBuilder.ApplyConfiguration(new SubmissionConfiguration());
    }
}
