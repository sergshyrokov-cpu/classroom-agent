using System.Text.Json;
using ClassroomAgent.Contracts;
using ClassroomAgent.ControlPlane.Persistence;
using ClassroomAgent.ControlPlane.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// The service channel's legitimacy check (US-005 spec FR-002, FR-004; api-design §5): anonymous and
/// antiforgery-exempt as the SC-4 "Legitimacy check" entries, POST only, JSON in and out. HTTP mapping only;
/// the rules are in <see cref="LegitimacyCheckService"/>. The body is read here, not by model binding, so
/// every malformed request — wrong content type, bad JSON, a number sent as a string — is the same
/// <c>400</c>, and the rejected body is never logged or echoed (SC-10).
/// </summary>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[Route(ServiceChannel.LegitimacyCheckPath)]
public sealed class LegitimacyCheckController(LegitimacyCheckService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Check(CancellationToken cancellationToken)
    {
        if (await ReadInputAsync(cancellationToken) is not { } input
            || !InstallationVersion.TryParse(input.ApplicationVersion, out var applicationVersion))
        {
            return Outcome(StatusCodes.Status400BadRequest, ServiceOutcome.InvalidRequest);
        }

        var result = await service.CheckAsync(input.InstallationId!.Value, applicationVersion, input.ContractVersion!.Value, cancellationToken);
        return result is LegitimacyCheckResult.Known known
            ? new JsonResult(
                new LegitimacyCheckResponse(
                    WireStatusOf(known.Status),
                    WireCompatibilityOf(known.Compatibility),
                    known.Domain,
                    known.ClientId),
                ServiceChannel.JsonOptions)
            : Outcome(StatusCodes.Status404NotFound, ServiceOutcome.UnknownInstallation);
    }

    private async Task<LegitimacyCheckInput?> ReadInputAsync(CancellationToken cancellationToken)
    {
        if (!Request.HasJsonContentType())
        {
            return null;
        }

        LegitimacyCheckRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<LegitimacyCheckRequest>(Request.Body, ServiceChannel.JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }

        if (request is null)
        {
            return null;
        }

        var input = LegitimacyCheckInput.From(request);
        return input.IsValid() ? input : null;
    }

    private static JsonResult Outcome(int statusCode, string outcome) =>
        new(new ServiceOutcome(outcome), ServiceChannel.JsonOptions) { StatusCode = statusCode };

    private static string WireStatusOf(InstallationStatus status) => status switch
    {
        InstallationStatus.Active => WireStatus.Active,
        InstallationStatus.Suspended => WireStatus.Suspended,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    private static string WireCompatibilityOf(CompatibilityState compatibility) => compatibility switch
    {
        CompatibilityState.Supported => WireCompatibility.Supported,
        CompatibilityState.UpgradeRecommended => WireCompatibility.UpgradeRecommended,
        CompatibilityState.UpgradeRequired => WireCompatibility.UpgradeRequired,
        _ => throw new ArgumentOutOfRangeException(nameof(compatibility), compatibility, null),
    };
}
