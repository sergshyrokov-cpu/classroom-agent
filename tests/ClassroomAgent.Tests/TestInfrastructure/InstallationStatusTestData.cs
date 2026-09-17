namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>Routes, element ids and notice values of the US-004 contract (docs/designs/api/US-004-openapi.yaml).</summary>
public static class InstallationStatusTestData
{
    public const string SuspendElement = "id=\"installation-suspend\"";

    public const string ResumeElement = "id=\"installation-resume\"";

    public const string NoticeElement = "id=\"installation-status-notice\"";

    public const string AlreadySuspended = "already-suspended";

    public const string AlreadyActive = "already-active";

    public static string DetailPath(Guid installation) => $"/installations/{installation:D}";

    public static string SuspensionPath(Guid installation) => $"/installations/{installation:D}/suspension";

    public static string ResumptionPath(Guid installation) => $"/installations/{installation:D}/resumption";

    public static string NoticePath(Guid installation, string notice) => $"{DetailPath(installation)}?notice={notice}";
}
