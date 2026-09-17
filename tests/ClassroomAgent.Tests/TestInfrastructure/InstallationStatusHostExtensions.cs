namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>US-004 helpers: posting the suspend and resume confirmations with a valid antiforgery token.</summary>
public static class InstallationStatusHostExtensions
{
    /// <summary>
    /// Posts the suspend confirmation. The token is taken from the registration form, which exists whatever the
    /// installation's status, so the post can also be sent when the confirmation page itself would redirect.
    /// </summary>
    public static Task<PageResponse> SuspendInstallationAsync(
        this FormClient owner,
        Guid installation,
        CancellationToken cancellationToken) =>
        owner.PostFromPageAsync(
            "/installations/new",
            InstallationStatusTestData.SuspensionPath(installation),
            [],
            cancellationToken);

    /// <summary>Posts the resume confirmation with a fresh token (see <see cref="SuspendInstallationAsync"/>).</summary>
    public static Task<PageResponse> ResumeInstallationAsync(
        this FormClient owner,
        Guid installation,
        CancellationToken cancellationToken) =>
        owner.PostFromPageAsync(
            "/installations/new",
            InstallationStatusTestData.ResumptionPath(installation),
            [],
            cancellationToken);

    /// <summary>Makes sure the client holds an antiforgery token without sending anything.</summary>
    public static Task<PageResponse> LoadTokenAsync(this FormClient owner, CancellationToken cancellationToken) =>
        owner.GetAsync("/installations/new", cancellationToken);
}
