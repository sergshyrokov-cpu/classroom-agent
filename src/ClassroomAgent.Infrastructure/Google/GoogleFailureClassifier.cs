using System.Net;
using ClassroomAgent.Application.Models;
using Google;
using Google.Apis.Auth.OAuth2.Responses;

namespace ClassroomAgent.Infrastructure.Google;

/// <summary>
/// The one place Google's refusals are mapped onto the closed list of the access check's outcomes (US-011 spec FR-005),
/// shared by <see cref="GoogleAccessProbe"/> and <see cref="GoogleClassroomReader"/> so a refusal is diagnosed the same
/// way by both (US-017 spec FR-001). Nothing a refusal says beyond its code and reason is kept.
/// </summary>
internal static class GoogleFailureClassifier
{
    /// <summary>Spec FR-005, token endpoint: Google's OAuth error codes and the descriptions it documents for them.</summary>
    public static AccessCheckStepOutcome ClassifyTokenError(TokenResponseException refusal)
    {
        if (IsUnavailable(refusal.StatusCode))
        {
            return AccessCheckStepOutcome.GoogleUnavailable;
        }

        var error = refusal.Error?.Error ?? string.Empty;
        var description = refusal.Error?.ErrorDescription ?? string.Empty;
        return error switch
        {
            "unauthorized_client" or "access_denied" => AccessCheckStepOutcome.ScopeNotAuthorized,
            "invalid_grant" when description.Contains("Invalid JWT Signature", StringComparison.OrdinalIgnoreCase) =>
                AccessCheckStepOutcome.KeyRejected,
            "invalid_grant" when description.Contains("Invalid email or User ID", StringComparison.OrdinalIgnoreCase)
                || description.Contains("invalid_user", StringComparison.OrdinalIgnoreCase) =>
                AccessCheckStepOutcome.TechnicalAccountUnknown,
            "invalid_client" => AccessCheckStepOutcome.KeyRejected,
            _ => AccessCheckStepOutcome.GoogleUnavailable,
        };
    }

    /// <summary>Spec FR-005, API reads: a disabled API is the Owner's; any other 403 is the account's rights.</summary>
    public static AccessCheckStepOutcome ClassifyApiError(GoogleApiException refusal)
    {
        if (IsUnavailable(refusal.HttpStatusCode))
        {
            return AccessCheckStepOutcome.GoogleUnavailable;
        }

        if (refusal.HttpStatusCode != HttpStatusCode.Forbidden)
        {
            return AccessCheckStepOutcome.GoogleUnavailable;
        }

        var reasons = (refusal.Error?.Errors ?? []).Select(e => e.Reason ?? string.Empty).ToList();
        var content = refusal.Error?.ErrorResponseContent ?? string.Empty;
        var disabled = reasons.Contains("accessNotConfigured", StringComparer.Ordinal)
            || reasons.Contains("SERVICE_DISABLED", StringComparer.Ordinal)
            || content.Contains("\"SERVICE_DISABLED\"", StringComparison.Ordinal)
            || content.Contains("\"accessNotConfigured\"", StringComparison.Ordinal);
        return disabled ? AccessCheckStepOutcome.ApiNotEnabled : AccessCheckStepOutcome.TechnicalAccountCannotRead;
    }

    /// <summary>Google answered <c>429</c> or <c>5xx</c>: a failure that may pass by itself.</summary>
    public static bool IsUnavailable(HttpStatusCode? status) =>
        status is HttpStatusCode.TooManyRequests || (status is { } code && (int)code >= 500);
}
