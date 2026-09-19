namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>The push address form (US-006 api-design §7). The value is optional: empty clears the address.</summary>
public sealed class ChangeInstallationPushAddressRequest
{
    [InstallationPushAddress]
    public string? PushAddress { get; set; }
}
