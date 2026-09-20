using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-009 AC-004 and AC-005: BR-020 — the second of the Owner's three points of control. An installation
/// may work only with the domain the Owner recorded, so a connection whose domain, or whose impersonation
/// user's email domain, differs from the <c>Installation</c> domain is refused (spec FR-006, FR-007; S-02;
/// SC-9). The refusal is a state conflict, `409` (api-design §2.4), and it writes nothing.
/// </summary>
public sealed class WorkspaceConnectionInvariantTests(PostgreSqlFixture database)
{
    /// <summary>AC-005: an address outside the installation's domain is refused, whatever its shape.</summary>
    [Theory]
    [MemberData(nameof(WorkspaceConnectionTestData.ForeignDomainAddresses), MemberType = typeof(WorkspaceConnectionTestData))]
    public async Task AnAddressOutsideTheDomain_IsRefused(string address)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var response = await client.SaveConnectionAsync(address, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Empty(await host.WorkspaceConnectionsAsync(ct));
    }

    /// <summary>AC-005: the refusal names the domain the installation may work with, in the user's language.</summary>
    [Fact]
    public async Task TheRefusal_NamesTheAllowedDomain()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var response = await client.SaveConnectionAsync(WorkspaceConnectionTestData.NeighbouringSchoolAccount, ct);

        Assert.Contains(
            host.Text(WorkspaceConnectionTestData.TextKeys.RefusedImpersonationDomainMismatch, "uk"),
            response.Text,
            StringComparison.Ordinal);
        Assert.Contains(WorkspaceConnectionTestData.AllowedDomain, response.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-005: the typed address survives the refusal so it can be corrected (spec FR-005).</summary>
    [Fact]
    public async Task AfterARefusal_TheTypedAddressIsStillInTheForm()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var response = await client.SaveConnectionAsync(WorkspaceConnectionTestData.NeighbouringSchoolAccount, ct);

        Assert.Equal(
            WorkspaceConnectionTestData.NeighbouringSchoolAccount,
            Html.InputValue(response.Body, WorkspaceConnectionTestData.EmailField));
    }

    /// <summary>AC-005, spec FR-007: a subdomain of the school's domain is not the school's domain.</summary>
    [Fact]
    public async Task ASubdomainOfTheSchoolDomain_IsNotTheDomain()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var response = await client.SaveConnectionAsync(WorkspaceConnectionTestData.SubdomainAccount, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Empty(await host.WorkspaceConnectionsAsync(ct));
    }

    /// <summary>
    /// AC-005: the Admin's own account is accepted only because it is in the domain — the program never asks
    /// whether a person stands behind it. BR-015 is explained on the page, not enforced against Google here.
    /// </summary>
    [Fact]
    public async Task TheAdminsOwnAddress_IsNotRefusedByTheDomainRule()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var response = await client.SaveConnectionAsync(WorkspaceConnectionTestData.AdminOwnAccount, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
    }

    /// <summary>AC-004: a stored connection bound to another domain is refused a change, not silently rebound.</summary>
    [Fact]
    public async Task AMismatchedStoredConnection_IsNotUsedToAcceptAForeignAddress()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        await host.InsertWorkspaceConnectionAsync(
            ct,
            domain: "school-two.example.test",
            impersonationUserEmail: "classroom-agent@school-two.example.test");

        var response = await client.SaveConnectionAsync("someone-else@school-two.example.test", ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        var row = Assert.Single(await host.WorkspaceConnectionsAsync(ct));
        Assert.Equal("classroom-agent@school-two.example.test", row.ImpersonationUserEmail);
    }

    /// <summary>
    /// AC-004, S-02: the refusal is the control, so it holds for a request the form cannot produce. A domain
    /// sent under any field name changes nothing: the domain written comes from <c>legitimacy_state</c>
    /// (OD-001), so the row is still bound to the installation's own domain.
    /// </summary>
    [Fact]
    public async Task ADomainSentInTheRequest_IsIgnored()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        await client.OpenSettingsAsync(ct);
        await client.GetAsync(SignInTestData.LandingPath, ct);

        var response = await client.PostFormAsync(
            WorkspaceConnectionTestData.Path,
            [
                new(WorkspaceConnectionTestData.EmailField, WorkspaceConnectionTestData.TechnicalAccount),
                new("domain", "school-two.example.test"),
                new("Domain", "school-two.example.test"),
            ],
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        var row = Assert.Single(await host.WorkspaceConnectionsAsync(ct));
        Assert.Equal(WorkspaceConnectionTestData.AllowedDomain, row.Domain);
    }

    /// <summary>AC-004: a refused save leaves the table exactly as it was.</summary>
    [Fact]
    public async Task ARefusedSave_LeavesAStoredConnectionUntouched()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);
        var before = await host.WorkspaceConnectionsAsync(ct);

        await client.SaveConnectionAsync(WorkspaceConnectionTestData.PersonalAccount, ct);

        Assert.Equal(before, await host.WorkspaceConnectionsAsync(ct));
    }

    /// <summary>AC-010: with no successful legitimacy check the save is refused and nothing is written.</summary>
    [Fact]
    public async Task WithNoKnownDomain_TheSaveIsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(
            database,
            ct,
            ReadOnlyModeHost.Cause.NeverConfirmed);
        await using var _host = host;

        var response = await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Empty(await host.WorkspaceConnectionsAsync(ct));
    }

    /// <summary>
    /// AC-010: a stale domain is never substituted for a missing one. An <c>upgrade_required</c> answer
    /// records a domain without moving the last successful check, and that domain must not license a save.
    /// </summary>
    [Fact]
    public async Task WithADomainButNoSuccessfulCheck_TheSaveIsStillRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(
            database,
            ct,
            ReadOnlyModeHost.Cause.NeverConfirmed);
        await using var _host = host;
        await host.InsertLegitimacyStateAsync(ct, lastSuccessfulCheckAt: null, compatibility: "upgrade_required");

        var response = await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Empty(await host.WorkspaceConnectionsAsync(ct));
    }
}
