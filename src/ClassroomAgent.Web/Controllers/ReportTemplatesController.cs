using System.Globalization;
using System.Security.Claims;
using ClassroomAgent.Application.Authorization;
using ClassroomAgent.Application.Localization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace ClassroomAgent.Web.Controllers;

/// <summary>
/// The report templates: list, new, copy, change and delete (US-027 openapi; spec FR-001, FR-002, FR-007 … FR-010,
/// FR-012). The list is under <c>UseReportTemplates</c>, every form and save under <c>EditReportTemplates</c>
/// (api-design §2.10).
/// </summary>
/// <remarks>
/// HTTP mapping only (AD-3): the host binds the posted pairs and renders; every rule — the reference, the built-in,
/// the field validation, the read-only guard — is in <c>Application</c>. A refused reference answers with the list and
/// its message (api-design §2.5). A save or delete ends in Post-Redirect-Get with the confirmation in TempData; a
/// <see cref="ClassroomAgent.Application.Exceptions.ReadOnlyModeException"/> is left to the host's exception handler,
/// which answers <c>409</c> (api-design §2.6).
/// </remarks>
public sealed class ReportTemplatesController(
    ListReportTemplatesQuery list,
    GetReportTemplateFormQuery forms,
    SaveReportTemplateUseCase save,
    DeleteReportTemplateUseCase delete,
    IStringLocalizer<SharedResource> text,
    ILogger<ReportTemplatesController> logger) : Controller
{
    [HttpGet(SignInRoutes.ReportTemplates)]
    [Authorize(Policy = InstallationPolicies.UseReportTemplates)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        await ListAsync(ConfirmationOf(TempData[SignInRoutes.ReportTemplateMessageTempDataKey] as string), null, cancellationToken);

    [HttpGet(SignInRoutes.ReportTemplateNew)]
    [Authorize(Policy = InstallationPolicies.EditReportTemplates)]
    public IActionResult New() => View("Form", forms.New());

    [HttpGet(SignInRoutes.ReportTemplateCopy)]
    [Authorize(Policy = InstallationPolicies.EditReportTemplates)]
    public async Task<IActionResult> Copy(string? templateRef, CancellationToken cancellationToken)
    {
        // Spec FR-008, I-3: the host passes the built-in name and the "(copy)" suffix in the actor's UI language.
        var result = await forms.CopyAsync(
            templateRef,
            text[ReportTemplateTextKeys.BuiltInName].Value,
            text[ReportTemplateTextKeys.CopySuffix].Value,
            cancellationToken);
        return await FormOrListAsync(result, cancellationToken);
    }

    [HttpGet(SignInRoutes.ReportTemplateEdit)]
    [Authorize(Policy = InstallationPolicies.EditReportTemplates)]
    public async Task<IActionResult> Edit(string? templateRef, CancellationToken cancellationToken) =>
        await FormOrListAsync(await forms.EditAsync(templateRef, cancellationToken), cancellationToken);

    [HttpGet(SignInRoutes.ReportTemplateDeletion)]
    [Authorize(Policy = InstallationPolicies.EditReportTemplates)]
    public async Task<IActionResult> Deletion(string? templateRef, CancellationToken cancellationToken)
    {
        var result = await forms.DeletionAsync(templateRef, cancellationToken);
        return result.Page is { } page
            ? View("Delete", page)
            : await RefusedAsync(result.Outcome, cancellationToken);
    }

    [HttpPost(SignInRoutes.ReportTemplates)]
    [Authorize(Policy = InstallationPolicies.EditReportTemplates)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var result = await save.CreateAsync(
            ActorId(), ActorRole(), FormInput(), HttpContext.TraceIdentifier, cancellationToken);
        return await AnswerSaveAsync(result, ReportTemplateConfirmationKey.Created, "created", cancellationToken);
    }

    [HttpPost(SignInRoutes.ReportTemplateChange)]
    [Authorize(Policy = InstallationPolicies.EditReportTemplates)]
    public async Task<IActionResult> Change(string? templateRef, CancellationToken cancellationToken)
    {
        var result = await save.ChangeAsync(
            ActorId(), ActorRole(), templateRef, FormInput(), HttpContext.TraceIdentifier, cancellationToken);
        return await AnswerSaveAsync(result, ReportTemplateConfirmationKey.Changed, "changed", cancellationToken);
    }

    [HttpPost(SignInRoutes.ReportTemplateDeletion)]
    [Authorize(Policy = InstallationPolicies.EditReportTemplates)]
    public async Task<IActionResult> Delete(string? templateRef, CancellationToken cancellationToken)
    {
        var outcome = await delete.ExecuteAsync(
            ActorId(), ActorRole(), templateRef, HttpContext.TraceIdentifier, cancellationToken);
        if (outcome != ReportTemplateOutcome.Succeeded)
        {
            return await RefusedAsync(outcome, cancellationToken);
        }

        // The reference is known to be a created template's id here; it is logged as the id it is.
        if (long.TryParse(templateRef, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            ReportTemplateLog.Written(logger, id, "deleted", ActorId());
        }

        TempData[SignInRoutes.ReportTemplateMessageTempDataKey] = nameof(ReportTemplateConfirmationKey.Deleted);
        return Redirect(SignInRoutes.ReportTemplates);
    }

    private async Task<IActionResult> AnswerSaveAsync(
        ReportTemplateSaveResult result,
        ReportTemplateConfirmationKey confirmation,
        string action,
        CancellationToken cancellationToken)
    {
        switch (result.Outcome)
        {
            case ReportTemplateOutcome.Succeeded:
                ReportTemplateLog.Written(logger, result.TemplateId ?? 0, action, ActorId());
                TempData[SignInRoutes.ReportTemplateMessageTempDataKey] = confirmation.ToString();
                return Redirect(SignInRoutes.ReportTemplates);

            case ReportTemplateOutcome.FieldsInvalid when result.Form is { } form:
                // Spec FR-019: the field and the rule, never the value.
                ReportTemplateLog.Rejected(
                    logger,
                    HttpContext.TraceIdentifier,
                    string.Join(", ", form.FieldErrors.Select(e => e.Field + ":" + e.Key)));
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return View("Form", form);

            case ReportTemplateOutcome.FormMalformed:
                // Api-design §2.5: a tampered form is the translated error page, with no new string (as US-039).
                ReportTemplateLog.Refused(logger, HttpContext.TraceIdentifier, nameof(ReportTemplateOutcome.FormMalformed));
                return ErrorController.Page(HttpContext, StatusCodes.Status400BadRequest, SignInRoutes.ReportTemplates);

            default:
                return await RefusedAsync(result.Outcome, cancellationToken);
        }
    }

    private async Task<IActionResult> FormOrListAsync(ReportTemplateFormResult result, CancellationToken cancellationToken) =>
        result.Form is { } form ? View("Form", form) : await RefusedAsync(result.Outcome, cancellationToken);

    /// <summary>Api-design §2.5: a refused reference re-renders the list with the message and the status.</summary>
    private async Task<IActionResult> RefusedAsync(ReportTemplateOutcome outcome, CancellationToken cancellationToken)
    {
        var (status, message) = outcome switch
        {
            ReportTemplateOutcome.ReferenceMalformed =>
                (StatusCodes.Status400BadRequest, ReportTemplateReferenceMessageKey.TemplateMalformed),
            ReportTemplateOutcome.BuiltInNotChangeable =>
                (StatusCodes.Status400BadRequest, ReportTemplateReferenceMessageKey.BuiltInNotChangeable),
            ReportTemplateOutcome.NotFound =>
                (StatusCodes.Status404NotFound, ReportTemplateReferenceMessageKey.TemplateNotFound),
            _ => throw new InvalidOperationException("The outcome is not a refused reference."),
        };

        ReportTemplateLog.Refused(logger, HttpContext.TraceIdentifier, message.ToString());
        Response.StatusCode = status;
        return await ListAsync(null, message, cancellationToken);
    }

    private async Task<IActionResult> ListAsync(
        ReportTemplateConfirmationKey? confirmation,
        ReportTemplateReferenceMessageKey? message,
        CancellationToken cancellationToken)
    {
        var page = await list.ExecuteAsync(CultureInfo.CurrentUICulture, confirmation, cancellationToken);
        return View("Index", page with { MessageKey = message });
    }

    private static ReportTemplateConfirmationKey? ConfirmationOf(string? stored) =>
        Enum.TryParse<ReportTemplateConfirmationKey>(stored, out var key) && Enum.IsDefined(key) ? key : null;

    /// <summary>Spec FR-009: every posted name/value pair, in order — the use case decides what is malformed.</summary>
    private ReportTemplateFormInput FormInput() =>
        new(Request.Form
            .SelectMany(field => field.Value.Select(value => new KeyValuePair<string, string?>(field.Key, value)))
            .ToList());

    /// <summary>Spec VR-008: the signed-in account from the session, never a request value; the policy guarantees one.</summary>
    private long ActorId() =>
        long.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException(
                "An authorized request carries no account identifier."),
            CultureInfo.InvariantCulture);

    private AppRole ActorRole() =>
        Enum.TryParse<AppRole>(User.FindFirstValue(ClaimTypes.Role), out var role) && Enum.IsDefined(role)
            ? role
            : throw new InvalidOperationException("An authorized request carries no known role.");
}
