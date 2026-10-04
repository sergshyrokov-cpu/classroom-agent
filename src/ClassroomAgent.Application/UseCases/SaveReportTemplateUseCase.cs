using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.Validation;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Creates (also from the copy form) and changes a template (US-027 spec FR-007 … FR-009, FR-013, FR-016). Order of
/// api-design §2.6: guard → reference → built-in → existence → form shape → fields → write with its audit row.
/// </summary>
public sealed class SaveReportTemplateUseCase(
    IReadOnlyModeGuard readOnlyMode,
    IReportTemplateRepository templates,
    IAuditEventRepository auditEvents,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope,
    TimeProvider timeProvider)
{
    public const string CreateOperation = "ReportTemplate.Create";

    public const string ChangeOperation = "ReportTemplate.Change";

    public async Task<ReportTemplateSaveResult> CreateAsync(
        long actorId,
        AppRole actorRole,
        ReportTemplateFormInput form,
        string? requestId,
        CancellationToken cancellationToken)
    {
        await ReportTemplateRefusalAudit.EnsureAllowedAsync(
            readOnlyMode,
            auditEvents,
            unitOfWork,
            writeScope,
            timeProvider,
            CreateOperation,
            AuditAction.ReportTemplateCreated,
            actorId,
            actorRole,
            null,
            requestId,
            cancellationToken);

        if (!ReportTemplateFormReader.TryRead(form, out var parsed) || parsed is null)
        {
            return Refused(ReportTemplateOutcome.FormMalformed);
        }

        var errors = await ValidateAsync(parsed, null, cancellationToken);
        if (errors.Count > 0)
        {
            return Invalid(ReportTemplateFormMode.Create, null, parsed, errors);
        }

        var template = ReportTemplate.Create(parsed.Name, ReportTemplateFormValidator.ToSettings(parsed), actorId);
        var now = timeProvider.GetUtcNow();
        try
        {
            // db-design §2.4: the audit row needs the generated id, so the template is saved first, in one transaction.
            await unitOfWork.ExecuteInTransactionAsync(
                async token =>
                {
                    templates.Add(template);
                    await unitOfWork.SaveChangesAsync(token);
                    auditEvents.Add(AuditEvent.ReportTemplateWritten(
                        actorId,
                        actorRole,
                        AuditAction.ReportTemplateCreated,
                        template.Id,
                        now,
                        requestId));
                    await unitOfWork.SaveChangesAsync(token);
                },
                cancellationToken);
        }
        catch (UniqueReportTemplateNameViolationException)
        {
            return Invalid(ReportTemplateFormMode.Create, null, parsed, [NameNotUnique()]);
        }

        return new ReportTemplateSaveResult(ReportTemplateOutcome.Succeeded, template.Id, null);
    }

    public async Task<ReportTemplateSaveResult> ChangeAsync(
        long actorId,
        AppRole actorRole,
        string? templateRef,
        ReportTemplateFormInput form,
        string? requestId,
        CancellationToken cancellationToken)
    {
        await ReportTemplateRefusalAudit.EnsureAllowedAsync(
            readOnlyMode,
            auditEvents,
            unitOfWork,
            writeScope,
            timeProvider,
            ChangeOperation,
            AuditAction.ReportTemplateChanged,
            actorId,
            actorRole,
            templateRef,
            requestId,
            cancellationToken);

        switch (ReportTemplateReference.Parse(templateRef, out var id))
        {
            case ReportTemplateReference.Kind.Malformed:
                return Refused(ReportTemplateOutcome.ReferenceMalformed);
            case ReportTemplateReference.Kind.BuiltIn:
                return Refused(ReportTemplateOutcome.BuiltInNotChangeable);
        }

        var template = await templates.GetAsync(id, true, cancellationToken);
        if (template is null)
        {
            return Refused(ReportTemplateOutcome.NotFound);
        }

        if (!ReportTemplateFormReader.TryRead(form, out var parsed) || parsed is null)
        {
            return Refused(ReportTemplateOutcome.FormMalformed);
        }

        var reference = ReportTemplateReference.Of(template.Id);
        var errors = await ValidateAsync(parsed, template.Id, cancellationToken);
        if (errors.Count > 0)
        {
            return Invalid(ReportTemplateFormMode.Change, reference, parsed, errors);
        }

        template.Change(parsed.Name, ReportTemplateFormValidator.ToSettings(parsed));
        var now = timeProvider.GetUtcNow();
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async token =>
                {
                    templates.MarkChanged(template);
                    auditEvents.Add(AuditEvent.ReportTemplateWritten(
                        actorId,
                        actorRole,
                        AuditAction.ReportTemplateChanged,
                        template.Id,
                        now,
                        requestId));
                    await unitOfWork.SaveChangesAsync(token);
                },
                cancellationToken);
        }
        catch (UniqueReportTemplateNameViolationException)
        {
            return Invalid(ReportTemplateFormMode.Change, reference, parsed, [NameNotUnique()]);
        }

        return new ReportTemplateSaveResult(ReportTemplateOutcome.Succeeded, template.Id, null);
    }

    /// <summary>Field rules, then the name's uniqueness among created templates (VR-001), excluding the template itself.</summary>
    private async Task<List<ReportTemplateFieldError>> ValidateAsync(
        ParsedReportTemplateForm parsed,
        long? exceptId,
        CancellationToken cancellationToken)
    {
        var errors = ReportTemplateFormValidator.Validate(parsed);
        if (!errors.Any(e => e.Field == "name")
            && await templates.NameExistsAsync(ReportTemplate.Normalize(parsed.Name), exceptId, cancellationToken))
        {
            errors.Add(NameNotUnique());
        }

        return errors;
    }

    private static ReportTemplateFieldError NameNotUnique() =>
        new("name", ReportTemplateFieldErrorKey.NameNotUnique, null);

    private static ReportTemplateSaveResult Refused(ReportTemplateOutcome outcome) => new(outcome, null, null);

    private static ReportTemplateSaveResult Invalid(
        ReportTemplateFormMode mode,
        string? reference,
        ParsedReportTemplateForm parsed,
        IReadOnlyList<ReportTemplateFieldError> errors) =>
        new(
            ReportTemplateOutcome.FieldsInvalid,
            null,
            ReportTemplateFormMapper.Page(mode, reference, parsed.ToValues(), errors));
}
