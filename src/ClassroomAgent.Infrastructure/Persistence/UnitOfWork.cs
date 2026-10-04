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

    private static bool IsUniqueEmailViolation(DbUpdateException failure) =>
        IsUniqueViolationOf(failure, UniqueEmailIndex);

    private static bool IsUniqueViolationOf(DbUpdateException failure, string constraintName) =>
        failure.InnerException is PostgresException { SqlState: UniqueViolation } postgres
        && string.Equals(postgres.ConstraintName, constraintName, StringComparison.Ordinal);
}
