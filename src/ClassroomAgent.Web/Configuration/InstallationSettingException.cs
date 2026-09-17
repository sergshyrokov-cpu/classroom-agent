namespace ClassroomAgent.Web.Configuration;

/// <summary>
/// A required installation setting is missing or invalid, so the host does not start (US-005 spec FR-001,
/// VR-001). The message names the setting and the rule — never the value (SC-10).
/// </summary>
public sealed class InstallationSettingException(string key, string message) : InvalidOperationException(message)
{
    public string Key { get; } = key;

    public static InstallationSettingException Missing(string key) =>
        new(key, $"The installation setting '{key}' is missing.");

    public static InstallationSettingException Invalid(string key, string rule) =>
        new(key, $"The installation setting '{key}' is invalid: {rule}.");
}
