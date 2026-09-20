using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Contracts;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Infrastructure.ControlPlane;

/// <summary>
/// HTTP implementation of <see cref="IControlPlaneClient"/> (US-005 api-design §6): posts the contract
/// request to the Control Plane address and classifies the reply. The whole call — connecting, TLS, headers
/// and body — has 30 seconds on the injected clock (spec I-6). A <c>200</c> answer is validated before it is
/// used (VR-003). Only the category leaves this class: the response body, headers and exception text are
/// never returned or logged (SC-10). Certificate validation is the platform's.
/// </summary>
public sealed class ControlPlaneClient(HttpClient httpClient, TimeProvider timeProvider) : IControlPlaneClient
{
    public static readonly TimeSpan CallLimit = TimeSpan.FromSeconds(30);

    private const int DomainMinLength = 3;
    private const int DomainMaxLength = 253;
    private const int ClientIdMinLength = 10;
    private const int ClientIdMaxLength = 32;

    public async Task<ControlPlaneCheckReply> CheckAsync(
        Guid installationId,
        string applicationVersion,
        int contractVersion,
        CancellationToken cancellationToken)
    {
        using var limit = new CancellationTokenSource(CallLimit, timeProvider);
        using var call = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, limit.Token);
        try
        {
            using var content = JsonContent.Create(
                new LegitimacyCheckRequest(installationId, applicationVersion, contractVersion),
                options: ServiceChannel.JsonOptions);
            using var response = await httpClient.PostAsync(ServiceChannel.LegitimacyCheckPath, content, call.Token);
            var body = await response.Content.ReadAsByteArrayAsync(call.Token);
            return Classify(response.StatusCode, body);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (limit.IsCancellationRequested)
        {
            return Failure(CheckFailureCategory.Timeout);
        }
        catch (HttpRequestException)
        {
            // Connection refused, DNS or TLS failure, an untrusted certificate included.
            return Failure(CheckFailureCategory.Unreachable);
        }
    }

    /// <summary>
    /// The Admin login check (US-008 spec FR-008; api-design §2.4). It reuses the service-channel timeout rather
    /// than introducing a second one (spec I-8): a sign-in that waits longer than a background check would be
    /// worse, not better. Only the outcome leaves this class; the body is never returned or logged (SC-10).
    /// </summary>
    public async Task<AdminLoginCheckReply> CheckAdminLoginAsync(
        Guid installationId,
        string email,
        CancellationToken cancellationToken)
    {
        using var limit = new CancellationTokenSource(CallLimit, timeProvider);
        using var call = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, limit.Token);
        try
        {
            using var content = JsonContent.Create(
                new AdminLoginCheckRequest(installationId, email),
                options: ServiceChannel.JsonOptions);
            using var response = await httpClient.PostAsync(ServiceChannel.AdminLoginCheckPath, content, call.Token);
            var body = await response.Content.ReadAsByteArrayAsync(call.Token);
            return ClassifyLogin(response.StatusCode, body);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (limit.IsCancellationRequested)
        {
            return AdminLoginCheckReply.Unavailable;
        }
        catch (HttpRequestException)
        {
            return AdminLoginCheckReply.Unavailable;
        }
    }

    /// <summary>
    /// api-design §2.4. The load-bearing row is the last one: a <c>404</c> <b>without</b> the
    /// <c>unknown_installation</c> body — what a Control Plane too old to route this path returns — is
    /// <see cref="AdminLoginCheckReply.Unavailable"/>, never "not allowed". Both refuse the sign-in, but the audit
    /// categories differ, and an operator must not read a failed deployment as a revocation.
    /// </summary>
    private static AdminLoginCheckReply ClassifyLogin(HttpStatusCode status, byte[] body) => status switch
    {
        HttpStatusCode.OK => ParseAllowed(body) switch
        {
            true => AdminLoginCheckReply.Allowed,
            false => AdminLoginCheckReply.NotAllowed,
            null => AdminLoginCheckReply.Unavailable,
        },
        HttpStatusCode.NotFound when IsUnknownInstallationOutcome(body) => AdminLoginCheckReply.UnknownInstallation,
        _ => AdminLoginCheckReply.Unavailable,
    };

    /// <summary>The single <c>allowed</c> property, or null when the answer is not one this contract defines.</summary>
    private static bool? ParseAllowed(byte[] body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("allowed", out var allowed)
                && allowed.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? allowed.GetBoolean()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ControlPlaneCheckReply Classify(HttpStatusCode status, byte[] body) => status switch
    {
        HttpStatusCode.OK => (ControlPlaneCheckReply?)ParseAnswer(body) ?? Failure(CheckFailureCategory.UnparseableAnswer),
        HttpStatusCode.NotFound when IsUnknownInstallationOutcome(body) => Failure(CheckFailureCategory.UnknownInstallation),
        _ => Failure(CheckFailureCategory.ErrorAnswer),
    };

    private static ControlPlaneCheckReply.Answer? ParseAnswer(byte[] body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || StringProperty(root, "status") is not { } statusText
                || StringProperty(root, "compatibility") is not { } compatibilityText
                || StringProperty(root, "domain") is not { } domain
                || StringProperty(root, "clientId") is not { } clientId
                || StatusOf(statusText) is not { } status
                || CompatibilityOf(compatibilityText) is not { } compatibility
                || domain.Length is < DomainMinLength or > DomainMaxLength
                || clientId.Length is < ClientIdMinLength or > ClientIdMaxLength
                || !clientId.All(char.IsAsciiDigit))
            {
                return null;
            }

            return new ControlPlaneCheckReply.Answer(status, compatibility, domain, clientId);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsUnknownInstallationOutcome(byte[] body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && StringProperty(document.RootElement, "outcome") == ServiceOutcome.UnknownInstallation;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? StringProperty(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static InstallationStatus? StatusOf(string text) => text switch
    {
        WireStatus.Active => InstallationStatus.Active,
        WireStatus.Suspended => InstallationStatus.Suspended,
        _ => null,
    };

    private static CompatibilityState? CompatibilityOf(string text) => text switch
    {
        WireCompatibility.Supported => CompatibilityState.Supported,
        WireCompatibility.UpgradeRecommended => CompatibilityState.UpgradeRecommended,
        WireCompatibility.UpgradeRequired => CompatibilityState.UpgradeRequired,
        _ => null,
    };

    private static ControlPlaneCheckReply.Failure Failure(CheckFailureCategory category) => new(category);
}
