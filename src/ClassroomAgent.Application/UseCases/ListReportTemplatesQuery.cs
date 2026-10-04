using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>The template list (US-027 spec FR-002). Reads only; works in read-only mode.</summary>
public sealed class ListReportTemplatesQuery(IReportTemplateRepository templates, SchoolTimeZone schoolTimeZone)
{
    public async Task<ReportTemplateListPageModel> ExecuteAsync(
        CultureInfo uiCulture,
        ReportTemplateConfirmationKey? confirmation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uiCulture);

        var records = await templates.ListAsync(cancellationToken);

        // FR-002: by name in the collation of the UI language, ties by internal id. The port is unordered.
        var collation = uiCulture.CompareInfo;
        var created = records
            .OrderBy(r => r.Name, Comparer<string>.Create((a, b) => collation.Compare(a, b, CompareOptions.None)))
            .ThenBy(r => r.Id)
            .Select(r => new ReportTemplateListItem(
                ReportTemplateReference.Of(r.Id),
                false,
                r.Name,
                r.AuthorEmail is null ? ReportAuthorState.AccountDeleted : ReportAuthorState.Known,
                r.AuthorEmail,
                TimeZoneInfo.ConvertTime(r.UpdatedAt, schoolTimeZone.Zone).DateTime,
                true));

        var items = new List<ReportTemplateListItem>
        {
            new(ReportTemplateReference.AcademicJournal, true, null, null, null, null, false),
        };
        items.AddRange(created);

        return new ReportTemplateListPageModel(items, confirmation, null);
    }
}
