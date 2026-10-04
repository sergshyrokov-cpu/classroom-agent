using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.Ports;

/// <summary>The school's created report templates (US-027 db-design §5.3, entity model §4).</summary>
public interface IReportTemplateRepository
{
    /// <summary>T1 — every created template with its author's email, if the account still exists.</summary>
    Task<IReadOnlyList<ReportTemplateListRecord>> ListAsync(CancellationToken cancellationToken);

    /// <summary>T2 — one template with its marks and scale rows; tracked when <paramref name="forUpdate"/>.</summary>
    Task<ReportTemplate?> GetAsync(long id, bool forUpdate, CancellationToken cancellationToken);

    /// <summary>T3 — whether another created template has this normalized name.</summary>
    Task<bool> NameExistsAsync(string normalizedName, long? exceptId, CancellationToken cancellationToken);

    void Add(ReportTemplate template);

    /// <summary>Marks the root modified so its last-change time is stamped (db-design §2.4).</summary>
    void MarkChanged(ReportTemplate template);

    void Remove(ReportTemplate template);
}
