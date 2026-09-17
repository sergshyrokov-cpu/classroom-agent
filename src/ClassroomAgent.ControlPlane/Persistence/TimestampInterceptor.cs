using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ClassroomAgent.ControlPlane.Persistence;

/// <summary>
/// Stamps <c>created_at</c> and <c>updated_at</c> in UTC (PC-6) and refuses any change or
/// deletion of an <see cref="AuditEvent"/> before anything is saved (db-design §4.2).
/// </summary>
public sealed class TimestampInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
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
            if (entry.Entity is AuditEvent && entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException("Audit events are never updated or deleted.");
            }

            if (entry.Entity is not (Owner or AuditEvent or Installation))
            {
                continue;
            }

            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(Owner.CreatedAt)).CurrentValue = now;
                entry.Property(nameof(Owner.UpdatedAt)).CurrentValue = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(Owner.UpdatedAt)).CurrentValue = now;
            }
        }
    }
}
