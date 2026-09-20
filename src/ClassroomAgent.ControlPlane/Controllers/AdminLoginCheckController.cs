using System.Text.Json;
using ClassroomAgent.Contracts;
using ClassroomAgent.ControlPlane.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// The service channel's Admin login check (US-008 spec FR-009; api-design §3): anonymous and
/// antiforgery-exempt under the existing SC-4 entry "Legitimacy check and Admin login check", POST only, JSON in
/// and out, protected by network isolation (SC-9). HTTP mapping only; the rule is in
/// <see cref="AdminLoginCheckService"/> (AD-3).
/// </summary>
/// <remarks>
/// The body is read here rather than by model binding, so every malformed request — wrong content type, bad
/// JSON, a wrong type, a broken rule — is the same <c>400 invalid_request</c>, and the rejected body is never
/// logged or echoed (SC-10), exactly as <see cref="LegitimacyCheckController"/> does.
/// </remarks>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[Route(ServiceChannel.AdminLoginCheckPath)]
public sealed class AdminLoginCheckController(AdminLoginCheckService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Check(CancellationToken cancellationToken)
    {
        if (await ReadInputAsync(cancellationToken) is not { } input)
        {
            return Outcome(StatusCodes.Status400BadRequest, ServiceOutcome.InvalidRequest);
        }

        var result = await service.CheckAsync(input.InstallationId!.Value, input.Email!, cancellationToken);
        return result switch
        {
            AdminLoginCheckResult.Allowed => Answer(allowed: true),
            AdminLoginCheckResult.NotAllowed => Answer(allowed: false),
            _ => Outcome(StatusCodes.Status404NotFound, ServiceOutcome.UnknownInstallation),
        };
    }

    private async Task<AdminLoginCheckInput?> ReadInputAsync(CancellationToken cancellationToken)
    {
        if (!Request.HasJsonContentType())
        {
            return null;
        }

        AdminLoginCheckRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<AdminLoginCheckRequest>(
                Request.Body,
                ServiceChannel.JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }

        if (request is null)
        {
            return null;
        }

        var input = AdminLoginCheckInput.From(request);
        return input.IsValid() ? input : null;
    }

    private static JsonResult Answer(bool allowed) =>
        new(new AdminLoginCheckResponse(allowed), ServiceChannel.JsonOptions);

    private static JsonResult Outcome(int statusCode, string outcome) =>
        new(new ServiceOutcome(outcome), ServiceChannel.JsonOptions) { StatusCode = statusCode };
}
