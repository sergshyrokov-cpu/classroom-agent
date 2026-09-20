using System.Text;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-008 helpers over a started Control Plane: calling the Admin login check on the service channel
/// (spec FR-009; api-design 3). Posted without a session and without an antiforgery token, exactly as an
/// installation does (SC-4 service-channel exemption).
/// </summary>
public static class AdminLoginCheckHostExtensions
{
    public static async Task<PageResponse> PostAdminLoginCheckAsync(
        this ControlPlaneTestHost host,
        string? body,
        CancellationToken cancellationToken,
        string contentType = "application/json")
    {
        using var client = host.CreateClient();
        var content = body is null ? null : new StringContent(body, Encoding.UTF8, contentType);
        return await client.SendAsync(HttpMethod.Post, AdminLoginCheckTestData.Path, content, cancellationToken);
    }

    public static Task<PageResponse> PostAdminLoginCheckAsync(
        this ControlPlaneTestHost host,
        Guid installationId,
        string email,
        CancellationToken cancellationToken) =>
        host.PostAdminLoginCheckAsync(AdminLoginCheckTestData.RequestJson(installationId, email), cancellationToken);

    /// <summary>The <c>allowed</c> property of a <c>200</c> answer; fails when the body has any other shape.</summary>
    public static bool AllowedOf(PageResponse response)
    {
        var properties = LegitimacyCheckHostExtensions.JsonProperties(response);
        Assert.Equal(new[] { "allowed" }, properties.Keys.Order(StringComparer.Ordinal));
        return bool.Parse(properties["allowed"]);
    }
}
