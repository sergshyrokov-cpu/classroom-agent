using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-009 AC-003 and AC-007: a valid save stores the domain and the technical account and nothing else, the
/// values are normalised, the single record is updated rather than duplicated, and the save reaches no
/// Google API (spec FR-001, FR-005, FR-006; S-04, S-10).
/// </summary>
public sealed class SaveWorkspaceConnectionTests(PostgreSqlFixture database)
{
    /// <summary>AC-003: the save succeeds and redirects back to the page (api-design §2.5).</summary>
    [Fact]
    public async Task AValidSave_RedirectsBackToThePage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var response = await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(WorkspaceConnectionTestData.Path, response.LocationPath);
    }

    /// <summary>AC-003: exactly the two values are stored, bound to the installation's own domain.</summary>
    [Fact]
    public async Task AValidSave_StoresTheDomainAndTheTechnicalAccount()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);

        var row = Assert.Single(await host.WorkspaceConnectionsAsync(ct));
        Assert.Equal(WorkspaceConnectionTestData.AllowedDomain, row.Domain);
        Assert.Equal(WorkspaceConnectionTestData.TechnicalAccount, row.ImpersonationUserEmail);
    }

    /// <summary>AC-003: the saved values are what the page shows afterwards, with the confirmation.</summary>
    [Fact]
    public async Task AfterASave_ThePageShowsTheStoredValuesAndAConfirmation()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);
        var page = await client.OpenSettingsAsync(ct);

        Assert.Contains(WorkspaceConnectionTestData.TechnicalAccount, page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(WorkspaceConnectionTestData.TextKeys.Saved, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-003: case and surrounding spaces do not make a different account (BR-079's precedent).</summary>
    [Fact]
    public async Task TheAddress_IsTrimmedAndLowerCased()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccountMixedCase, ct);

        var row = Assert.Single(await host.WorkspaceConnectionsAsync(ct));
        Assert.Equal(WorkspaceConnectionTestData.TechnicalAccount, row.ImpersonationUserEmail);
        Assert.Equal(row.ImpersonationUserEmail.ToLowerInvariant(), row.ImpersonationUserEmail);
        Assert.Equal(row.Domain.ToLowerInvariant(), row.Domain);
    }

    /// <summary>AC-004, spec FR-007: every spelling of the allowed domain is accepted as that domain.</summary>
    [Theory]
    [MemberData(nameof(WorkspaceConnectionTestData.EquivalentSpellings), MemberType = typeof(WorkspaceConnectionTestData))]
    public async Task AnEquivalentSpellingOfTheDomain_IsAccepted(string address)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var response = await client.SaveConnectionAsync(address, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        var row = Assert.Single(await host.WorkspaceConnectionsAsync(ct));
        Assert.Equal(WorkspaceConnectionTestData.AllowedDomain, row.Domain);
    }

    /// <summary>AC-007: a second save changes the one record; a second row is impossible.</summary>
    [Fact]
    public async Task ChangingTheAccount_UpdatesTheOneRecord()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);
        var first = Assert.Single(await host.WorkspaceConnectionsAsync(ct));

        await client.SaveConnectionAsync(WorkspaceConnectionTestData.OtherTechnicalAccount, ct);

        var row = Assert.Single(await host.WorkspaceConnectionsAsync(ct));
        Assert.Equal(first.Id, row.Id);
        Assert.Equal(WorkspaceConnectionTestData.OtherTechnicalAccount, row.ImpersonationUserEmail);
    }

    /// <summary>AC-007: the previous value is not kept in the table — its history is the audit trail.</summary>
    [Fact]
    public async Task AfterAChange_ThePreviousAddressIsNotInTheTable()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);

        await client.SaveConnectionAsync(WorkspaceConnectionTestData.OtherTechnicalAccount, ct);

        var rows = await host.WorkspaceConnectionsAsync(ct);
        Assert.DoesNotContain(rows, r => r.ImpersonationUserEmail == WorkspaceConnectionTestData.TechnicalAccount);
    }

    /// <summary>AC-007, spec I-7: saving the identical values is accepted and changes nothing but the stamp.</summary>
    [Fact]
    public async Task SavingTheSameValuesAgain_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);
        var first = Assert.Single(await host.WorkspaceConnectionsAsync(ct));

        var response = await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        var row = Assert.Single(await host.WorkspaceConnectionsAsync(ct));
        Assert.Equal(first.Id, row.Id);
        Assert.Equal(first.ImpersonationUserEmail, row.ImpersonationUserEmail);
        Assert.Equal(first.CreatedAt, row.CreatedAt);
    }

    /// <summary>AC-007: a connection seeded before the Story's first save is updated, never duplicated.</summary>
    [Fact]
    public async Task WithAConnectionAlreadyStored_TheSaveUpdatesIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        await host.InsertWorkspaceConnectionAsync(ct, impersonationUserEmail: WorkspaceConnectionTestData.TechnicalAccount);

        await client.SaveConnectionAsync(WorkspaceConnectionTestData.OtherTechnicalAccount, ct);

        var row = Assert.Single(await host.WorkspaceConnectionsAsync(ct));
        Assert.Equal(WorkspaceConnectionTestData.OtherTechnicalAccount, row.ImpersonationUserEmail);
    }

    /// <summary>
    /// OD-002: saving again writes the domain the installation knows now, which is how a mismatch is cleared.
    /// </summary>
    [Fact]
    public async Task SavingAgain_ClearsADomainMismatch()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        await host.InsertWorkspaceConnectionAsync(
            ct,
            domain: "school-two.example.test",
            impersonationUserEmail: "classroom-agent@school-two.example.test");

        await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);

        var row = Assert.Single(await host.WorkspaceConnectionsAsync(ct));
        Assert.Equal(WorkspaceConnectionTestData.AllowedDomain, row.Domain);
        Assert.Equal(WorkspaceConnectionTestData.TechnicalAccount, row.ImpersonationUserEmail);
    }

    /// <summary>
    /// AC-003, S-10: saving records the account and verifies nothing. The one transport the installation has
    /// carries no request beyond the Admin login check of the sign-in itself — no Google call, none at all.
    /// </summary>
    [Fact]
    public async Task TheSave_CallsNothingOutsideTheInstallation()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, channel) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        var beforeSave = channel.Requests.Count;

        await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);

        Assert.Equal(beforeSave, channel.Requests.Count);
    }

    /// <summary>AC-003, S-04: no column of the stored row can carry a key, a secret or a reference to one.</summary>
    [Fact]
    public async Task TheTable_HoldsNoCredentialColumn()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var columns = await host.QueryAsync(
            """
            SELECT column_name FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'workspace_connection'
            ORDER BY column_name
            """,
            r => r.GetString(0),
            ct);

        Assert.NotEmpty(columns);
        Assert.All(
            columns,
            c => Assert.DoesNotContain(
                new[] { "key", "secret", "credential", "password", "token", "client_id" },
                forbidden => c.Contains(forbidden, StringComparison.Ordinal)));
    }
}
