using System.Net;
using System.Text.RegularExpressions;
using ClassroomAgent.Application.Ports;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Drives US-011 over HTTP: an installation host whose Google port is the <see cref="FakeGoogleAccessProbe"/>, a
/// signed-in Admin, the page and the run of US-011 openapi. Nothing reaches Google (TC-4).
/// </summary>
public static partial class AccessCheckHostExtensions
{
    /// <summary>
    /// A started installation in the given legitimacy state with an Admin signed in and the fake port registered.
    /// The connection is seeded <b>after</b> the start, so the startup self-check found none; the calls it might
    /// still make are told apart by <see cref="FakeGoogleAccessProbe.RequestCalls"/>.
    /// </summary>
    public static async Task<(InstallationTestHost Host, FormClient Client, FakeGoogleAccessProbe Probe)> StartSignedInAsync(
        PostgreSqlFixture database,
        CancellationToken cancellationToken,
        ReadOnlyModeHost.Cause cause = ReadOnlyModeHost.Cause.NotReadOnly,
        SeededConnection connection = SeededConnection.Usable,
        string? keyReference = null)
    {
        var host = await InstallationTestHost.CreateAsync(database, cancellationToken);
        await ReadOnlyModeHost.SeedAsync(host, cause, cancellationToken);
        if (keyReference is not null)
        {
            host.Settings[AccessCheckTestData.KeyReferenceSetting] = keyReference;
        }

        host.ControlPlaneHandler = WorkspaceConnectionHostExtensions.ApprovingChannel();
        host.ConfigureServices = RegisterFakeProbe;
        host.Start();
        var (client, callback) = await host.SignInWithGoogleAsync(cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, callback.Status);
        await SeedConnectionAsync(host, connection, cancellationToken);
        return (host, client, ProbeOf(host));
    }

    /// <summary>
    /// A host that is created, seeded and only then started — for the startup self-check, which reads what is in the
    /// database at start. No one signs in.
    /// </summary>
    public static async Task<(InstallationTestHost Host, FakeGoogleAccessProbe Probe)> StartWithSelfCheckAsync(
        PostgreSqlFixture database,
        CancellationToken cancellationToken,
        ReadOnlyModeHost.Cause cause = ReadOnlyModeHost.Cause.NotReadOnly,
        SeededConnection connection = SeededConnection.Usable,
        Action<FakeGoogleAccessProbe>? script = null)
    {
        var host = await InstallationTestHost.CreateAsync(database, cancellationToken);
        await ReadOnlyModeHost.SeedAsync(host, cause, cancellationToken);
        await SeedConnectionAsync(host, connection, cancellationToken);

        // Scripted before the host starts: the self-check may call the port as soon as it has started.
        var probe = new FakeGoogleAccessProbe(new HttpContextAccessor());
        script?.Invoke(probe);
        host.ConfigureServices = services => RegisterFakeProbe(services, probe);
        host.Start();
        return (host, probe);
    }

    /// <summary>Registers the fake as the one Google port, replacing whatever the host registered.</summary>
    public static void RegisterFakeProbe(IServiceCollection services) => RegisterFakeProbe(services, null);

    /// <summary>Registers that fake instance, or a new one, as the one Google port.</summary>
    public static void RegisterFakeProbe(IServiceCollection services, FakeGoogleAccessProbe? instance)
    {
        services.AddHttpContextAccessor();
        services.RemoveAll<IGoogleAccessProbe>();
        services.RemoveAll<FakeGoogleAccessProbe>();
        if (instance is null)
        {
            services.AddSingleton<FakeGoogleAccessProbe>();
        }
        else
        {
            services.AddSingleton(instance);
        }

        services.AddSingleton<IGoogleAccessProbe>(p => p.GetRequiredService<FakeGoogleAccessProbe>());
    }

    public static FakeGoogleAccessProbe ProbeOf(InstallationTestHost host) =>
        host.Services.GetRequiredService<FakeGoogleAccessProbe>();

    public static async Task SeedConnectionAsync(InstallationTestHost host, SeededConnection connection, CancellationToken cancellationToken)
    {
        switch (connection)
        {
            case SeededConnection.None:
                return;
            case SeededConnection.Usable:
                await host.InsertWorkspaceConnectionAsync(cancellationToken, impersonationUserEmail: AccessCheckTestData.TechnicalAccount);
                return;
            case SeededConnection.ForAnotherDomain:
                await host.InsertWorkspaceConnectionAsync(
                    cancellationToken,
                    domain: InstallationTestData.OtherDomain,
                    impersonationUserEmail: "classroom-agent@" + InstallationTestData.OtherDomain);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(connection), connection, null);
        }
    }

    /// <summary>Opens the check-access page.</summary>
    public static Task<PageResponse> OpenAccessCheckAsync(this FormClient client, CancellationToken cancellationToken) =>
        client.GetAsync(AccessCheckTestData.Path, cancellationToken);

    /// <summary>
    /// Opens the page and posts the run with its antiforgery token. While the page does not exist yet it carries no
    /// token, so the token of the landing page is used — the <c>POST</c> then fails on the production behaviour under
    /// test, not on the fixture (the US-009 pattern).
    /// </summary>
    public static async Task<PageResponse> RunAccessCheckAsync(
        this FormClient client,
        CancellationToken cancellationToken,
        bool withToken = true,
        IEnumerable<KeyValuePair<string, string>>? extraFields = null)
    {
        var page = await client.OpenAccessCheckAsync(cancellationToken);
        if (withToken && !Html.HasInput(page.Body, Html.AntiforgeryFieldName))
        {
            await client.GetAsync(SignInTestData.LandingPath, cancellationToken);
        }

        return await client.PostFormAsync(AccessCheckTestData.Path, extraFields ?? [], cancellationToken, withToken);
    }

    /// <summary>The audit rows of this Story, in order.</summary>
    public static async Task<IReadOnlyList<InstallationAuditRow>> AccessCheckAuditRowsAsync(
        this InstallationTestHost host,
        CancellationToken cancellationToken)
    {
        var rows = await host.AuditRowsAsync(cancellationToken);
        return rows.Where(r => r.Action == AccessCheckTestData.Audit.Action).ToList();
    }

    /// <summary>The id of the one stored connection.</summary>
    public static async Task<long> ConnectionIdAsync(this InstallationTestHost host, CancellationToken cancellationToken) =>
        Assert.Single(await host.WorkspaceConnectionsAsync(cancellationToken)).Id;

    /// <summary>The verdict the page renders, or null when it renders none.</summary>
    public static string? VerdictOf(PageResponse page)
    {
        var match = VerdictElement().Match(page.Body);
        return match.Success ? match.Groups["verdict"].Value : null;
    }

    /// <summary>The steps the page renders, in document order.</summary>
    public static IReadOnlyList<RenderedStep> StepsOf(PageResponse page) =>
        StepElement().Matches(page.Body)
            .Select(m => new RenderedStep(
                Attribute(m.Value, AccessCheckTestData.Markup.KindAttribute),
                Attribute(m.Value, AccessCheckTestData.Markup.ScopeAttribute),
                Attribute(m.Value, AccessCheckTestData.Markup.OutcomeAttribute),
                System.Net.WebUtility.HtmlDecode(Tags().Replace(m.Groups["content"].Value, " "))))
            .ToList();

    private static string? Attribute(string element, string name)
    {
        var match = Regex.Match(element, name + "=\"(?<value>[^\"]*)\"", RegexOptions.CultureInvariant);
        return match.Success ? System.Net.WebUtility.HtmlDecode(match.Groups["value"].Value) : null;
    }

    [GeneratedRegex("id=\"access-check-verdict\"[^>]*data-verdict=\"(?<verdict>[A-Za-z]+)\"|data-verdict=\"(?<verdict>[A-Za-z]+)\"[^>]*id=\"access-check-verdict\"", RegexOptions.CultureInvariant)]
    private static partial Regex VerdictElement();

    [GeneratedRegex("<(?<tag>li|div|tr)\\b[^>]*class=\"[^\"]*\\baccess-check-step\\b[^\"]*\"[^>]*>(?<content>.*?)</\\k<tag>>", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex StepElement();

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex Tags();
}
