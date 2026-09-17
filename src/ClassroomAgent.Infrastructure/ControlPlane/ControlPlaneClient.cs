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
