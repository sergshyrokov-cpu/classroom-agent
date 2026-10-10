using ClassroomAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ClassroomAgent.Infrastructure.Persistence;

/// <summary>Stamps <c>created_at</c> and <c>updated_at</c> in UTC from the injected clock (PC-6).</summary>
public sealed class TimestampInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    private const string CreatedAt = nameof(LegitimacyState.CreatedAt);

    private const string UpdatedAt = nameof(LegitimacyState.UpdatedAt);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            // US-008 db-design §7.1: the two new entities are stamped by the same interceptor. An audit row is
            // never Modified, so ck_audit_event_immutable holds (§4.2). US-014 db-design §3.1, §4.1, §5.1 add the
            // three tables of courses and rosters, whose created_at / updated_at are stamped the same way (PC-6).
            // US-015 db-design §3.1, §4.1 add course_work and submission, stamped the same way.
            // US-027 db-design §2 adds the template aggregate, stamped the same way.
            // US-031 db-design §2.1, §3.1 add meet_session and meet_participation, stamped the same way (PC-6).
            if (entry.Entity is not (LegitimacyState
                or AppUser
                or AuditEvent
                or Course
                or ClassroomParticipant
                or CourseMembership
                or CourseWork
                or Submission
                or ReportTemplate
                or ReportTemplateMark
                or ReportTemplateScaleRow
                or MeetSession
                or MeetParticipation))
            {
                continue;
            }

            if (entry.State == EntityState.Added)
            {
                entry.Property(CreatedAt).CurrentValue = now;
                entry.Property(UpdatedAt).CurrentValue = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(UpdatedAt).CurrentValue = now;
            }
        }
    }
}
