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
            if (entry.Entity is not LegitimacyState)
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
