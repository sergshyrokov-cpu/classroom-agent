using System.Text.Json;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The Admin login check wire contract (US-008 openapi <c>AdminLoginCheckRequest</c> /
/// <c>AdminLoginCheckResponse</c>). The email travels in the body, never in the address
/// (<c>trebovaniya.md</c> section 8, v64).
/// </summary>
public static class AdminLoginCheckTestData
{
    public const string Path = "/service/v1/admin-login-checks";

    public static string RequestJson(Guid installationId, string email) =>
        JsonSerializer.Serialize(new
        {
            installationId = installationId.ToString("D"),
            email,
        });

    /// <summary>The Control Plane's answer, as the installation's client must read it.</summary>
    public static string AnswerJson(bool allowed) =>
        JsonSerializer.Serialize(new { allowed });

    public static string OutcomeJson(string outcome) =>
        JsonSerializer.Serialize(new { outcome });

    /// <summary>An email of exactly that many characters, in the school's domain where it fits.</summary>
    public static string EmailOfLength(int length)
    {
        const string domain = "@school-one.example.test";
        var localPart = new string('a', Math.Max(1, length - domain.Length));
        return localPart + domain;
    }
}
