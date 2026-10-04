namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-019 fixed names: the path, the return pages, the text keys, the audit codes and the log events of
/// docs/designs/api/US-019-openapi.yaml, docs/designs/database/US-019-db-design.md and the test strategy.
/// </summary>
public static class SynchronizationRequestTestData
{
    /// <summary>openapi <c>POST /synchronization/requests</c>.</summary>
    public const string Path = "/synchronization/requests";

    /// <summary>openapi <c>x-return-page-by-role</c>, Admin.</summary>
    public const string AdminReturnPage = WorkspaceConnectionTestData.Path;

    /// <summary>openapi <c>x-return-page-by-role</c>, Dean.</summary>
    public const string DeanReturnPage = SignInTestData.LandingPath;

    /// <summary>The policy of spec FR-007 (api-design §2.6).</summary>
    public const string Policy = "StartSynchronization";

    /// <summary>The password of a Dean whose password is not temporary, so their session is not restricted.</summary>
    public const string DeanPassword = DeanAccountTestData.NewPassword;

    public static class TextKeys
    {
        public const string Button = "Synchronization.Request.Button";
        public const string Requested = "Synchronization.Request.Requested";
        public const string RequestedAfterCurrentWork = "Synchronization.Request.RequestedAfterCurrentWork";
        public const string ConnectionNotUsableAdmin = "Synchronization.Request.ConnectionNotUsableAdmin";
        public const string ConnectionNotUsableDean = "Synchronization.Request.ConnectionNotUsableDean";

        public static readonly string[] All =
        [
            Button,
            Requested,
            RequestedAfterCurrentWork,
            ConnectionNotUsableAdmin,
            ConnectionNotUsableDean,
        ];
    }

    /// <summary>The database codes of db-design §2.2.</summary>
    public static class Audit
    {
        public const string Action = "synchronization_requested";
        public const string Succeeded = "succeeded";
        public const string Refused = "refused";
        public const string ReadOnlyMode = "read_only_mode";
        public const string ConnectionNotUsable = "connection_not_usable";
        public const string ActorTypeAppUser = "app_user";
        public const string RoleAdmin = "admin";
        public const string RoleDean = "dean";
    }

    /// <summary>The log event names of spec FR-010.</summary>
    public static class LogEvents
    {
        public const string Requested = "SynchronizationRequested";
        public const string Refused = "SynchronizationRequestRefused";
    }

    /// <summary>
    /// The markup the pages render: the form posting to <see cref="Path"/> carries <see cref="FormId"/>, and the
    /// one-time message carries <see cref="MessageId"/>.
    /// </summary>
    public static class Markup
    {
        public const string FormId = "synchronization-request-form";
        public const string MessageId = "synchronization-request-message";
    }
}
