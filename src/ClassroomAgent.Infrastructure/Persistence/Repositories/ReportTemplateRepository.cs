using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence.Repositories;

/// <summary>
/// The school's created report templates over PostgreSQL (US-027 db-design §5.3, T1 … T3). It stages changes only;
/// the unit of work saves them.
/// </summary>
public sealed class ReportTemplateRepository(ClassroomAgentDbContext db) : IReportTemplateRepository
{
    /// <summary>T1 — every template with its author's email; the join is manual because there is no foreign key.</summary>
    public async Task<IReadOnlyList<ReportTemplateListRecord>> ListAsync(CancellationToken cancellationToken) =>
        await (
            from t in db.ReportTemplates.AsNoTracking()
            join u in db.AppUsers.AsNoTracking() on t.AuthorId equals u.Id into authors
            from author in authors.DefaultIfEmpty()
            select new ReportTemplateListRecord(t.Id, t.Name, t.UpdatedAt, author == null ? null : author.Email))
            .ToListAsync(cancellationToken);

    /// <summary>T2 — the root with its marks and scale rows, tracked only for a change or a delete.</summary>
    public async Task<ReportTemplate?> GetAsync(long id, bool forUpdate, CancellationToken cancellationToken)
    {
        var query = db.ReportTemplates
            .Include(t => t.Marks)
            .Include(t => t.ScaleRows)
            .AsSplitQuery();
        if (!forUpdate)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    /// <summary>T3 — whether another template already holds the normalized name.</summary>
    public async Task<bool> NameExistsAsync(string normalizedName, long? exceptId, CancellationToken cancellationToken) =>
        await db.ReportTemplates
            .AsNoTracking()
            .AnyAsync(t => t.NormalizedName == normalizedName && (exceptId == null || t.Id != exceptId), cancellationToken);

    public void Add(ReportTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        db.ReportTemplates.Add(template);
    }

    /// <summary>
    /// A change that touches only child rows leaves the root unchanged, so the interceptor would not stamp
    /// <c>updated_at</c>; marking the root modified makes it the template's "last change" (db-design §2.4).
    /// </summary>
    public void MarkChanged(ReportTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        db.Entry(template).State = EntityState.Modified;
    }

    public void Remove(ReportTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        db.ReportTemplates.Remove(template);
    }
}
