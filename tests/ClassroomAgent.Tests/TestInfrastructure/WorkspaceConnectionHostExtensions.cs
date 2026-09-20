using System.Net;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Drives the US-009 connection settings over HTTP: a host whose Control Plane answers the Admin login
/// check "allowed", a signed-in Admin, and the page and save of US-009 openapi. Nothing here reaches
/// Google — this Story adds no Google call at all (spec S-10).
/// </summary>
public static class WorkspaceConnectionHostExtensions
{
    /// <summary>A Control Plane channel that approves the Admin and answers nothing else (TC-4).</summary>
    public static ScriptedHttpHandler ApprovingChannel() =>
        ScriptedHttpHandler.AdminLoginCheckJson(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(true));

    /// <summary>
    /// A started installation in the given legitimacy state, with an Admin already signed in. The default
    /// seeds a recent successful check, so the installation knows its domain and is not read-only.
    /// </summary>
    public static async Task<(InstallationTestHost Host, FormClient Client, ScriptedHttpHandler Channel)> StartSignedInAsync(
        PostgreSqlFixture database,
        CancellationToken cancellationToken,
        ReadOnlyModeHost.Cause cause = ReadOnlyModeHost.Cause.NotReadOnly)
    {
        var channel = ApprovingChannel();
        var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, cancellationToken, cause);
        var (client, callback) = await host.SignInWithGoogleAsync(cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, callback.Status);
        return (host, client, channel);
    }

    /// <summary>Opens the settings page.</summary>
    public static Task<PageResponse> OpenSettingsAsync(this FormClient client, CancellationToken cancellationToken) =>
        client.GetAsync(WorkspaceConnectionTestData.Path, cancellationToken);

    /// <summary>
    /// Opens the page and posts the save with its antiforgery token. While the page does not exist yet it
    /// carries no token, so the token of the landing page is used instead — the POST then fails on the
    /// production behaviour under test, not on the fixture.
    /// </summary>
    public static async Task<PageResponse> SaveConnectionAsync(
        this FormClient client,
        string impersonationUserEmail,
        CancellationToken cancellationToken,
        bool withToken = true)
    {
        var page = await client.OpenSettingsAsync(cancellationToken);
        if (withToken && !Html.HasInput(page.Body, Html.AntiforgeryFieldName))
        {
            await client.GetAsync(SignInTestData.LandingPath, cancellationToken);
        }

        return await client.PostFormAsync(
            WorkspaceConnectionTestData.Path,
            [new(WorkspaceConnectionTestData.EmailField, impersonationUserEmail)],
            cancellationToken,
            withToken);
    }

    /// <summary>Every stored connection row, so "at most one" is an assertion and not an assumption.</summary>
    public static Task<IReadOnlyList<WorkspaceConnectionRow>> WorkspaceConnectionsAsync(
        this InstallationTestHost host,
        CancellationToken cancellationToken) =>
        host.QueryAsync(
            "SELECT id, domain, impersonation_user_email, created_at, updated_at FROM workspace_connection ORDER BY id",
            r => new WorkspaceConnectionRow(
                r.GetInt64(0),
                r.GetString(1),
                r.GetString(2),
                r.GetFieldValue<DateTimeOffset>(3),
                r.GetFieldValue<DateTimeOffset>(4)),
            cancellationToken);

    /// <summary>Seeds a connection row directly, for the change and mismatch scenarios.</summary>
    public static Task<int> InsertWorkspaceConnectionAsync(
        this InstallationTestHost host,
        CancellationToken cancellationToken,
        string domain = WorkspaceConnectionTestData.AllowedDomain,
        string? impersonationUserEmail = null,
        DateTimeOffset? stampedAt = null) =>
        host.ExecuteAsync(
            """
            INSERT INTO workspace_connection (domain, impersonation_user_email, created_at, updated_at)
            VALUES (@domain, @email, @stamp, @stamp)
            """,
            cancellationToken,
            ("domain", domain.ToLowerInvariant()),
            ("email", (impersonationUserEmail ?? "classroom-agent@" + domain).ToLowerInvariant()),
            ("stamp", stampedAt ?? host.Time.GetUtcNow()));

    /// <summary>The audit rows this Story writes, in order.</summary>
    public static async Task<IReadOnlyList<InstallationAuditRow>> ConnectionAuditRowsAsync(
        this InstallationTestHost host,
        CancellationToken cancellationToken)
    {
        var rows = await host.AuditRowsAsync(cancellationToken);
        return rows.Where(r => r.Action == WorkspaceConnectionTestData.Audit.Action).ToList();
    }
}

/// <summary>One row of <c>workspace_connection</c> (US-009 db-design §3).</summary>
public sealed record WorkspaceConnectionRow(
    long Id,
    string Domain,
    string ImpersonationUserEmail,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
