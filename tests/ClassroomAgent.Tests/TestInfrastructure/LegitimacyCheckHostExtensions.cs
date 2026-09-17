using System.Text;
using System.Text.Json;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>US-005 helpers over a started Control Plane: calling the service channel and reading <c>instance_license_check</c>.</summary>
public static class LegitimacyCheckHostExtensions
{
    /// <summary>Posts a raw body to the check endpoint without a session or antiforgery token, as an installation does.</summary>
    public static async Task<PageResponse> PostCheckAsync(
        this ControlPlaneTestHost host,
        string? body,
        CancellationToken cancellationToken,
        string contentType = "application/json")
    {
        using var client = host.CreateClient();
        var content = body is null ? null : new StringContent(body, Encoding.UTF8, contentType);
        return await client.SendAsync(HttpMethod.Post, LegitimacyCheckTestData.Path, content, cancellationToken);
    }

    public static Task<PageResponse> PostCheckAsync(
        this ControlPlaneTestHost host,
        Guid installationId,
        CancellationToken cancellationToken,
        string applicationVersion = LegitimacyCheckTestData.ApplicationVersion,
        int contractVersion = 1) =>
        host.PostCheckAsync(
            LegitimacyCheckTestData.RequestJson(installationId, applicationVersion, contractVersion),
            cancellationToken);

    /// <summary>The body as a JSON object: property name → string or raw value.</summary>
    public static IReadOnlyDictionary<string, string> JsonProperties(PageResponse response)
    {
        using var document = JsonDocument.Parse(response.Body);
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        return document.RootElement.EnumerateObject().ToDictionary(
            p => p.Name,
            p => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText(),
            StringComparer.Ordinal);
    }

    public static Task<IReadOnlyList<InstanceLicenseCheckRow>> InstanceLicenseChecksAsync(
        this ControlPlaneTestHost host,
        CancellationToken cancellationToken) =>
        host.QueryAsync(
            "SELECT id, installation_id, answered_at, application_version, contract_version, answered_status, answered_compatibility, created_at, updated_at FROM instance_license_check ORDER BY id",
            r => new InstanceLicenseCheckRow(
                r.GetInt64(0),
                r.GetInt64(1),
                r.GetFieldValue<DateTimeOffset>(2),
                r.GetString(3),
                r.GetInt32(4),
                r.GetString(5),
                r.GetString(6),
                r.GetFieldValue<DateTimeOffset>(7),
                r.GetFieldValue<DateTimeOffset>(8)),
            cancellationToken);

    public static async Task<long> InstallationInternalIdAsync(
        this ControlPlaneTestHost host,
        Guid identifier,
        CancellationToken cancellationToken) =>
        await host.ScalarAsync<long>(
            "SELECT id FROM installation WHERE identifier = @identifier",
            cancellationToken,
            ("identifier", identifier));

    /// <summary>Inserts a last-check row directly — for detail-page and schema scenarios.</summary>
    public static async Task InsertInstanceLicenseCheckAsync(
        this ControlPlaneTestHost host,
        Guid installationIdentifier,
        CancellationToken cancellationToken,
        DateTimeOffset answeredAt,
        string applicationVersion = "1.0.0",
        int contractVersion = 1,
        string status = "active",
        string compatibility = "supported")
    {
        var id = await host.InstallationInternalIdAsync(installationIdentifier, cancellationToken);
        await host.ExecuteAsync(
            """
            INSERT INTO instance_license_check (installation_id, answered_at, application_version, contract_version, answered_status, answered_compatibility, created_at, updated_at)
            VALUES (@installationId, @answeredAt, @applicationVersion, @contractVersion, @status, @compatibility, @answeredAt, @answeredAt)
            """,
            cancellationToken,
            ("installationId", id),
            ("answeredAt", answeredAt),
            ("applicationVersion", applicationVersion),
            ("contractVersion", contractVersion),
            ("status", status),
            ("compatibility", compatibility));
    }

    /// <summary>Stops the host and parses its log files.</summary>
    public static async Task<IReadOnlyList<LogEvent>> ReadLogEventsAsync(
        this ControlPlaneTestHost host,
        CancellationToken cancellationToken) =>
        LogEvent.Parse(await host.ReadLogFilesAsync(cancellationToken));
}
