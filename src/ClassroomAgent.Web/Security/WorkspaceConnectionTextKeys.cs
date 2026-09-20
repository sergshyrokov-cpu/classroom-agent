namespace ClassroomAgent.Web.Security;

/// <summary>
/// The translation keys of the connection settings (US-009 spec FR-011). Named once, so the view, the
/// controller and the tests cannot drift; every key exists in both the Ukrainian and the English file.
/// </summary>
public static class WorkspaceConnectionTextKeys
{
    public const string Title = "WorkspaceConnection.Title";

    public const string DomainLabel = "WorkspaceConnection.Domain.Label";

    public const string DomainUnknown = "WorkspaceConnection.Domain.Unknown";

    public const string ImpersonationUserLabel = "WorkspaceConnection.ImpersonationUser.Label";

    public const string TechnicalAccountHint = "WorkspaceConnection.ImpersonationUser.Hint";

    public const string CheckAccessHint = "WorkspaceConnection.CheckAccess.Hint";

    public const string StateNotConfigured = "WorkspaceConnection.State.NotConfigured";

    public const string StateConfigured = "WorkspaceConnection.State.Configured";

    public const string StateDomainMismatch = "WorkspaceConnection.State.DomainMismatch";

    public const string SaveButton = "WorkspaceConnection.Save.Button";

    public const string Saved = "WorkspaceConnection.Saved";

    public const string RefusedDomainMismatch = "WorkspaceConnection.Refused.DomainMismatch";

    public const string RefusedImpersonationDomainMismatch = "WorkspaceConnection.Refused.ImpersonationDomainMismatch";

    public const string RefusedDomainNotConfirmed = "WorkspaceConnection.Refused.DomainNotConfirmed";

    public const string NavigationEntry = "Landing.Settings.WorkspaceConnection";
}
