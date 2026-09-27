using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Drives the US-012 screens over HTTP: a host with an Admin signed in for the management screen, and the
/// Dean's own sign-in by password — the journey no helper could make before this Story, which is why the
/// forbidden-role tests of US-009, US-010 and US-011 had to build a principal by hand (carried US-010 F-1).
/// Nothing here reaches Google (spec S-14).
/// </summary>
public static class DeanAccountHostExtensions
{
    /// <summary>A Control Plane channel that approves the Admin and answers nothing else (TC-4).</summary>
    public static ScriptedHttpHandler ApprovingChannel() =>
        ScriptedHttpHandler.AdminLoginCheckJson(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(true));

    /// <summary>A started installation with an Admin signed in, in the given legitimacy state.</summary>
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

    /// <summary>Opens the Dean accounts screen.</summary>
    public static Task<PageResponse> OpenDeanAccountsAsync(this FormClient client, CancellationToken cancellationToken) =>
        client.GetAsync(DeanAccountTestData.Paths.Deans, cancellationToken);

    /// <summary>
    /// Opens the screen for its antiforgery token and posts the creation form. While the screen does not exist
    /// yet it carries no token, so the landing page's token is used instead — the POST then fails on the
    /// production behaviour under test, not on the fixture (the US-009 pattern).
    /// </summary>
    public static async Task<PageResponse> CreateDeanAsync(
        this FormClient client,
        string email,
        string temporaryPassword,
        CancellationToken cancellationToken,
        bool withToken = true)
    {
        await client.OpenForTokenAsync(DeanAccountTestData.Paths.Deans, cancellationToken);
        return await client.PostFormAsync(
            DeanAccountTestData.Paths.Deans,
            [
                new(DeanAccountTestData.Fields.Email, email),
                new(DeanAccountTestData.Fields.TemporaryPassword, temporaryPassword),
            ],
            cancellationToken,
            withToken);
    }

    /// <summary>Posts the state form of one account.</summary>
    public static async Task<PageResponse> SetDeanStateAsync(
        this FormClient client,
        long deanId,
        string desiredState,
        CancellationToken cancellationToken,
        bool withToken = true)
    {
        await client.OpenForTokenAsync(DeanAccountTestData.Paths.Deans, cancellationToken);
        return await client.PostFormAsync(
            DeanAccountTestData.Paths.State(deanId),
            [new(DeanAccountTestData.Fields.DesiredState, desiredState)],
            cancellationToken,
            withToken);
    }

    /// <summary>Posts a password reset for one account.</summary>
    public static async Task<PageResponse> ResetDeanPasswordAsync(
        this FormClient client,
        long deanId,
        string temporaryPassword,
        CancellationToken cancellationToken,
        bool withToken = true)
    {
        await client.OpenForTokenAsync(DeanAccountTestData.Paths.Deans, cancellationToken);
        return await client.PostFormAsync(
            DeanAccountTestData.Paths.Password(deanId),
            [new(DeanAccountTestData.Fields.TemporaryPassword, temporaryPassword)],
            cancellationToken,
            withToken);
    }

    /// <summary>Opens the sign-in page and posts the Dean's email and password (US-012 openapi POST /sign-in).</summary>
    public static async Task<PageResponse> SignInAsDeanAsync(
        this FormClient client,
        string email,
        string password,
        CancellationToken cancellationToken,
        bool withToken = true)
    {
        await client.GetAsync(DeanAccountTestData.Paths.SignIn, cancellationToken);
        return await client.PostFormAsync(
            DeanAccountTestData.Paths.SignIn,
            [
                new(DeanAccountTestData.Fields.Email, email),
                new(DeanAccountTestData.Fields.Password, password),
            ],
            cancellationToken,
            withToken);
    }

    /// <summary>Posts the forced change of a temporary password.</summary>
    public static async Task<PageResponse> CompleteForcedChangeAsync(
        this FormClient client,
        string newPassword,
        CancellationToken cancellationToken,
        bool withToken = true)
    {
        await client.OpenForTokenAsync(DeanAccountTestData.Paths.ForcedChange, cancellationToken);
        return await client.PostFormAsync(
            DeanAccountTestData.Paths.ForcedChange,
            [new(DeanAccountTestData.Fields.NewPassword, newPassword)],
            cancellationToken,
            withToken);
    }

    /// <summary>Posts the Dean's own password change.</summary>
    public static async Task<PageResponse> ChangeOwnPasswordAsync(
        this FormClient client,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken,
        bool withToken = true)
    {
        await client.OpenForTokenAsync(DeanAccountTestData.Paths.OwnPassword, cancellationToken);
        return await client.PostFormAsync(
            DeanAccountTestData.Paths.OwnPassword,
            [
                new(DeanAccountTestData.Fields.CurrentPassword, currentPassword),
                new(DeanAccountTestData.Fields.NewPassword, newPassword),
            ],
            cancellationToken,
            withToken);
    }

    /// <summary>
    /// A Dean row already in the table, with a password this suite knows. The hash is written as the future
    /// implementation would write it, through the same port name the composition root will bind.
    /// </summary>
    public static Task<int> InsertDeanAsync(
        this InstallationTestHost host,
        CancellationToken cancellationToken,
        string email = DeanAccountTestData.DeanEmail,
        string password = DeanAccountTestData.TemporaryPassword,
        bool passwordIsTemporary = true,
        bool isDisabled = false,
        DateTimeOffset? lastSuccessfulSignInAt = null) =>
        host.ExecuteAsync(
            """
            INSERT INTO app_user (email, normalized_email, role, sign_in_method, password_hash, security_stamp,
                                  concurrency_stamp, access_failed_count, ui_language, is_disabled,
                                  password_is_temporary, last_successful_sign_in_at, created_at, updated_at)
            VALUES (@email, @email, 'dean', 'password', @passwordHash, @securityStamp, @concurrencyStamp,
                    0, 'uk', @isDisabled, @temporary, @lastSignIn, @stamp, @stamp)
            """,
            cancellationToken,
            ("email", email.ToLowerInvariant()),
            ("passwordHash", HashOf(host, password)),
            ("securityStamp", Guid.NewGuid().ToString("N")),
            ("concurrencyStamp", Guid.NewGuid().ToString("N")),
            ("isDisabled", isDisabled),
            ("temporary", passwordIsTemporary),
            ("lastSignIn", lastSuccessfulSignInAt),
            ("stamp", host.Time.GetUtcNow()));

    /// <summary>
    /// The hash the production code would store, produced by the host's own hasher port — so a seeded account
    /// signs in exactly as a created one does, and the fixture cannot drift from the implementation.
    /// </summary>
    private static string HashOf(InstallationTestHost host, string password)
    {
        using var scope = host.CreateScope();
        return scope.ServiceProvider
            .GetRequiredService<ClassroomAgent.Application.Ports.IPasswordHasher>()
            .Hash(password);
    }

    /// <summary>
    /// Opens a page only to pick up its antiforgery token, falling back to the landing page while the page does
    /// not exist yet, so a red-phase POST fails on the behaviour under test rather than on a missing token.
    /// </summary>
    private static async Task OpenForTokenAsync(
        this FormClient client,
        string path,
        CancellationToken cancellationToken)
    {
        await client.GetAsync(path, cancellationToken);
        if (client.LastToken is null)
        {
            await client.GetAsync(DeanAccountTestData.Paths.Landing, cancellationToken);
        }
    }
}
