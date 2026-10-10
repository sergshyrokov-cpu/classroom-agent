using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Ports;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClassroomAgent.Infrastructure.Persistence;

/// <summary>Commits what the repositories staged in this scope's <see cref="ClassroomAgentDbContext"/> (AD-7).</summary>
public sealed class UnitOfWork(ClassroomAgentDbContext db) : IUnitOfWork
{
    /// <summary>The unique index of US-008 db-design §3.1, named so the translation below cannot catch another one.</summary>
    private const string UniqueEmailIndex = "uq_app_user_normalized_email";

    /// <summary>The unique index of US-027 db-design §2.1.</summary>
    private const string UniqueTemplateNameIndex = "uq_report_template_normalized_name";

    /// <summary>The unique index of US-032 db-design §2.2.</summary>
    private const string UniqueMeetingCodeLinkIndex = "uq_meeting_code_link_meeting_code";

    private const string UniqueViolation = "23505";

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException failure) when (IsUniqueEmailViolation(failure))
        {
            // Translated here so Application never sees a provider type (AD-3, AD-4; spec I-10).
            throw new UniqueEmailViolationException(failure);
        }
        catch (DbUpdateException failure) when (IsUniqueViolationOf(failure, UniqueTemplateNameIndex))
        {
            throw new UniqueReportTemplateNameViolationException(failure);
        }
        catch (DbUpdateConcurrencyException failure) when (failure.Entries.Any(IsMeetingCodeLink))
        {
            // A concurrency-stamp mismatch or a row removed under the writer (US-032 db-design §7).
            DetachFailedMeetCodeEntries();
            throw new MeetingCodeLinkConflictException(failure);
        }
        catch (DbUpdateException failure) when (IsUniqueViolationOf(failure, UniqueMeetingCodeLinkIndex))
        {
            DetachFailedMeetCodeEntries();
            throw new MeetingCodeLinkConflictException(failure);
        }
        catch (DbUpdateException) when (HasPendingMeetCodeEntries())
        {
            // US-032 spec FR-006: a link that failed to save for any other reason must not be retried by the run's own
            // failure save (SyncState.FailRun), so the failed link and its audit row leave the tracker before rethrowing.
            DetachFailedMeetCodeEntries();
            throw;
        }
    }

    private bool HasPendingMeetCodeEntries() =>
        db.ChangeTracker.Entries<ClassroomAgent.Domain.Entities.MeetingCodeLink>()
            .Any(e => e.State is EntityState.Added or EntityState.Modified);

    /// <summary>
    /// After a conflict the entries that failed to save are detached so a later save in the same scope (the run keeps
    /// saving its sync state after the linking step) does not retry them. Not <c>ChangeTracker.Clear()</c>: the
    /// run's tracked <c>SyncState</c> must stay tracked.
    /// </summary>
    private void DetachFailedMeetCodeEntries()
    {
        foreach (var entry in db.ChangeTracker.Entries<ClassroomAgent.Domain.Entities.MeetingCodeLink>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }

        foreach (var entry in db.ChangeTracker.Entries<ClassroomAgent.Domain.Entities.AuditEvent>()
                     .Where(e => e.State == EntityState.Added)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (db.Database.CurrentTransaction is not null)
        {
            await work(cancellationToken);
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await work(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static bool IsMeetingCodeLink(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry) =>
        entry.Entity is ClassroomAgent.Domain.Entities.MeetingCodeLink;

    private static bool IsUniqueEmailViolation(DbUpdateException failure) =>
        IsUniqueViolationOf(failure, UniqueEmailIndex);

    private static bool IsUniqueViolationOf(DbUpdateException failure, string constraintName) =>
        failure.InnerException is PostgresException { SqlState: UniqueViolation } postgres
        && string.Equals(postgres.ConstraintName, constraintName, StringComparison.Ordinal);
}
