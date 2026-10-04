using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The new, copy and change forms and the delete confirmation (US-027 spec FR-007 … FR-010). Reads only; works in
/// read-only mode. Translated texts — the built-in name and the " (copy)" suffix — come from the host in the
/// actor's UI language (spec I-3).
/// </summary>
public sealed class GetReportTemplateFormQuery(IReportTemplateRepository templates)
{
    /// <summary>FR-007: the new form with its defaults.</summary>
    public ReportTemplateFormPageModel New() =>
        ReportTemplateFormMapper.Page(ReportTemplateFormMode.Create, null, ReportTemplateFormMapper.Defaults(), []);

    /// <summary>FR-008: the copy form of any template, built-in included; saved through create.</summary>
    public async Task<ReportTemplateFormResult> CopyAsync(
        string? templateRef,
        string builtInName,
        string copySuffix,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builtInName);
        ArgumentNullException.ThrowIfNull(copySuffix);

        switch (ReportTemplateReference.Parse(templateRef, out var id))
        {
            case ReportTemplateReference.Kind.BuiltIn:
                return Form(
                    ReportTemplateFormMode.Copy,
                    null,
                    ReportTemplateFormMapper.FromSettings(builtInName + copySuffix, BuiltInReportTemplates.AcademicJournal));
            case ReportTemplateReference.Kind.Created:
                var template = await templates.GetAsync(id, false, cancellationToken);
                return template is null
                    ? Refused(ReportTemplateOutcome.NotFound)
                    : Form(
                        ReportTemplateFormMode.Copy,
                        null,
                        ReportTemplateFormMapper.FromSettings(template.Name + copySuffix, template.ToSettings()));
            default:
                return Refused(ReportTemplateOutcome.ReferenceMalformed);
        }
    }

    /// <summary>FR-009: the change form of a created template; the built-in is refused (FR-006).</summary>
    public async Task<ReportTemplateFormResult> EditAsync(string? templateRef, CancellationToken cancellationToken)
    {
        var (outcome, template) = await FindCreatedAsync(templateRef, cancellationToken);
        return template is null
            ? Refused(outcome)
            : Form(
                ReportTemplateFormMode.Change,
                ReportTemplateReference.Of(template.Id),
                ReportTemplateFormMapper.FromSettings(template.Name, template.ToSettings()));
    }

    /// <summary>FR-010: the delete confirmation page of a created template.</summary>
    public async Task<ReportTemplateDeletionResult> DeletionAsync(string? templateRef, CancellationToken cancellationToken)
    {
        var (outcome, template) = await FindCreatedAsync(templateRef, cancellationToken);
        return template is null
            ? new ReportTemplateDeletionResult(outcome, null)
            : new ReportTemplateDeletionResult(
                ReportTemplateOutcome.Succeeded,
                new ReportTemplateDeletePageModel(ReportTemplateReference.Of(template.Id), template.Name));
    }

    private async Task<(ReportTemplateOutcome Outcome, ReportTemplate? Template)> FindCreatedAsync(
        string? templateRef,
        CancellationToken cancellationToken)
    {
        switch (ReportTemplateReference.Parse(templateRef, out var id))
        {
            case ReportTemplateReference.Kind.BuiltIn:
                return (ReportTemplateOutcome.BuiltInNotChangeable, null);
            case ReportTemplateReference.Kind.Created:
                var template = await templates.GetAsync(id, false, cancellationToken);
                return template is null
                    ? (ReportTemplateOutcome.NotFound, null)
                    : (ReportTemplateOutcome.Succeeded, template);
            default:
                return (ReportTemplateOutcome.ReferenceMalformed, null);
        }
    }

    private static ReportTemplateFormResult Form(
        ReportTemplateFormMode mode,
        string? reference,
        ReportTemplateFormValues values) =>
        new(ReportTemplateOutcome.Succeeded, ReportTemplateFormMapper.Page(mode, reference, values, []));

    private static ReportTemplateFormResult Refused(ReportTemplateOutcome outcome) => new(outcome, null);
}
