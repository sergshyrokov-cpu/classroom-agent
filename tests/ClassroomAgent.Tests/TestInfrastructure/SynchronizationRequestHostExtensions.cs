using System.Net;
using ClassroomAgent.Application.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Drives US-019 over HTTP: an installation host whose synchronization-request port is the
/// <see cref="FakeSynchronizationRequests"/>, an Admin or a Dean signed in, and the press of openapi
/// <c>POST /synchronization/requests</c>. Nothing reaches Google (TC-4).
/// </summary>
public static class SynchronizationRequestHostExtensions
{
    public enum Actor
    {
        Admin,
        Dean,

        /// <summary>A Dean whose password is temporary: the restricted session of US-012 api-design §2.6.</summary>
        DeanWithTemporaryPassword,

        /// <summary>No session at all.</summary>
        Anonymous,
    }

    /// <summary>
    /// A started installation in the given legitimacy state, with the connection seeded, the fake port registered
    /// and the given actor signed in.
    /// </summary>
    public static async Task<(InstallationTestHost Host, FormClient Client, FakeSynchronizationRequests Requests)> StartAsync(
        PostgreSqlFixture database,
        Actor actor,
        CancellationToken cancellationToken,
        ReadOnlyModeHost.Cause cause = ReadOnlyModeHost.Cause.NotReadOnly,
        SeededConnection connection = SeededConnection.Usable)
    {
        var host = await InstallationTestHost.CreateAsync(database, cancellationToken);
        await ReadOnlyModeHost.SeedAsync(host, cause, cancellationToken);
        host.ControlPlaneHandler = WorkspaceConnectionHostExtensions.ApprovingChannel();
        host.ConfigureServices = RegisterFakeRequests;
        host.Start();
        await AccessCheckHostExtensions.SeedConnectionAsync(host, connection, cancellationToken);

        FormClient client;
        switch (actor)
        {
            case Actor.Admin:
                {
                    var (admin, callback) = await host.SignInWithGoogleAsync(cancellationToken);
                    Assert.Equal(HttpStatusCode.Redirect, callback.Status);
                    client = admin;
                    break;
                }

            case Actor.Dean:
            case Actor.DeanWithTemporaryPassword:
                {
                    var temporary = actor == Actor.DeanWithTemporaryPassword;
                    var password = temporary ? DeanAccountTestData.TemporaryPassword : SynchronizationRequestTestData.DeanPassword;
                    await host.InsertDeanAsync(cancellationToken, password: password, passwordIsTemporary: temporary);
                    client = host.CreateClient();
                    var signIn = await client.SignInAsDeanAsync(DeanAccountTestData.DeanEmail, password, cancellationToken);
                    Assert.Equal(HttpStatusCode.Redirect, signIn.Status);
                    break;
                }

            case Actor.Anonymous:
                client = host.CreateClient();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(actor), actor, null);
        }

        return (host, client, RequestsOf(host));
    }

    /// <summary>Registers one fake instance as the synchronization-request port, replacing the host's.</summary>
    public static void RegisterFakeRequests(IServiceCollection services)
    {
        services.RemoveAll<ISynchronizationRequests>();
        services.RemoveAll<FakeSynchronizationRequests>();
        services.AddSingleton<FakeSynchronizationRequests>();
        services.AddSingleton<ISynchronizationRequests>(p => p.GetRequiredService<FakeSynchronizationRequests>());
    }

    public static FakeSynchronizationRequests RequestsOf(InstallationTestHost host) =>
        host.Services.GetRequiredService<FakeSynchronizationRequests>();

    /// <summary>
    /// Opens <paramref name="fromPage"/> (the page carrying the button) and posts the press with its antiforgery
    /// token. While the button does not exist yet the page may carry no token, so the token of the landing page is
    /// used — the <c>POST</c> then fails on the production behaviour under test, not on the fixture (the US-009
    /// pattern).
    /// </summary>
    public static async Task<PageResponse> PressSynchronizeAsync(
        this FormClient client,
        string fromPage,
        CancellationToken cancellationToken,
        bool withToken = true,
        IEnumerable<KeyValuePair<string, string>>? extraFields = null)
    {
        var page = await client.GetAsync(fromPage, cancellationToken);
        if (withToken && !Html.HasInput(page.Body, Html.AntiforgeryFieldName))
        {
            await client.GetAsync(SignInTestData.LandingPath, cancellationToken);
        }

        return await client.PostFormAsync(SynchronizationRequestTestData.Path, extraFields ?? [], cancellationToken, withToken);
    }

    /// <summary>The audit rows of this Story, in order.</summary>
    public static async Task<IReadOnlyList<InstallationAuditRow>> SynchronizationRequestAuditRowsAsync(
        this InstallationTestHost host,
        CancellationToken cancellationToken)
    {
        var rows = await host.AuditRowsAsync(cancellationToken);
        return rows.Where(r => r.Action == SynchronizationRequestTestData.Audit.Action).ToList();
    }
}
