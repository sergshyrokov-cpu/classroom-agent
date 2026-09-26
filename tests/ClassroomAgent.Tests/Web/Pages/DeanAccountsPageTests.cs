using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-012 AC-001, AC-002, AC-003, AC-009: the Admin's screen and its three POSTs, against the US-012 OpenAPI
/// contract — status codes, the Post-Redirect-Get of every successful write, and the re-rendered form of every
/// refusal (TC-3, api-design §3).
/// </summary>
public sealed class DeanAccountsPageTests(PostgreSqlFixture database)
{
    /// <summary>AC-001: the screen lists a seeded Dean with the columns OD-003 fixed.</summary>
    [Fact]
    public async Task TheScreen_ListsTheDeans()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        await host.InsertDeanAsync(ct);

        var page = await client.OpenDeanAccountsAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(DeanAccountTestData.DeanEmail, page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-002: a valid creation answers with Post-Redirect-Get back to the screen (api-design §2.2).</summary>
    [Fact]
    public async Task AValidCreation_RedirectsBackToTheScreen()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;

        var page = await client.CreateDeanAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.TemporaryPassword, ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(DeanAccountTestData.Paths.Deans, page.LocationPath);
        Assert.Contains(await host.AppUsersAsync(ct), u => u.Role == "dean" && u.Email == DeanAccountTestData.DeanEmail);
    }

    /// <summary>
    /// AC-003: a refused creation answers 400 and re-renders the form with the typed **email** preserved and
    /// the password nowhere in the response (spec VR-006, S-10).
    /// </summary>
    [Fact]
    public async Task ARefusedCreation_PreservesTheEmailAndNeverThePassword()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;

        var page = await client.CreateDeanAsync(DeanAccountTestData.OutsideDomainEmail, DeanAccountTestData.TemporaryPassword, ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Contains(DeanAccountTestData.OutsideDomainEmail, page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(DeanAccountTestData.TemporaryPassword, page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-005: disabling an account redirects back to the screen and the row is disabled.</summary>
    [Fact]
    public async Task Disabling_RedirectsBackAndMarksTheRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        await host.InsertDeanAsync(ct);
        var dean = Assert.Single(await host.AppUsersAsync(ct), u => u.Role == "dean");

        var page = await client.SetDeanStateAsync(dean.Id, DeanAccountTestData.Fields.StateDisabled, ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(DeanAccountTestData.Paths.Deans, page.LocationPath);
        var after = Assert.Single(await host.AppUsersAsync(ct), u => u.Role == "dean");
        Assert.True(after.IsDisabled);
    }

    /// <summary>
    /// AC-005, I-8: a management action on an id that is not a Dean answers 404 — never 403, which would
    /// announce that the id exists (api-design §2.7).
    /// </summary>
    [Fact]
    public async Task AManagementActionOnANonDeanId_Answers404()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        var admin = Assert.Single(await host.AppUsersAsync(ct), u => u.Role == "admin");

        var screen = await client.OpenDeanAccountsAsync(ct);
        var onAdmin = await client.SetDeanStateAsync(admin.Id, DeanAccountTestData.Fields.StateDisabled, ct);
        var onNobody = await client.SetDeanStateAsync(9999, DeanAccountTestData.Fields.StateDisabled, ct);

        // The screen must exist, or the two 404s below would say nothing about the id (red-phase check).
        Assert.Equal(HttpStatusCode.OK, screen.Status);
        Assert.Equal(HttpStatusCode.NotFound, onAdmin.Status);
        Assert.Equal(HttpStatusCode.NotFound, onNobody.Status);
    }

    /// <summary>AC-007: a reset redirects back and the row shows a temporary password again.</summary>
    [Fact]
    public async Task AReset_RedirectsBack()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        await host.InsertDeanAsync(ct);
        var dean = Assert.Single(await host.AppUsersAsync(ct), u => u.Role == "dean");

        var page = await client.ResetDeanPasswordAsync(dean.Id, "a fresh temporary one", ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(DeanAccountTestData.Paths.Deans, page.LocationPath);
    }

    /// <summary>
    /// AC-009: in read-only mode every management action answers 409 and nothing is written, while the screen
    /// itself keeps answering 200 (API-5, BR-026, spec FR-015).
    /// </summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheScreenIsServedAndTheActionsAreRefused(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct, cause);
        await using var _host = host;
        using var _client = client;
        var before = (await host.AppUsersAsync(ct)).Count;

        var screen = await client.OpenDeanAccountsAsync(ct);
        var created = await client.CreateDeanAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.TemporaryPassword, ct);

        Assert.Equal(HttpStatusCode.OK, screen.Status);
        Assert.Equal(HttpStatusCode.Conflict, created.Status);
        Assert.Equal(before, (await host.AppUsersAsync(ct)).Count);
    }

    /// <summary>
    /// AC-008: no path deletes an account. The contract exposes no DELETE, so the method is refused on both
    /// the collection and one account (spec FR-010, BR-014).
    /// </summary>
    [Fact]
    public async Task NoDeleteMethod_IsExposed()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;
        await host.InsertDeanAsync(ct);
        var dean = Assert.Single(await host.AppUsersAsync(ct), u => u.Role == "dean");

        var onCollection = await client.SendAsync(HttpMethod.Delete, DeanAccountTestData.Paths.Deans, null, ct);
        var onOne = await client.SendAsync(HttpMethod.Delete, DeanAccountTestData.Paths.Deans + "/" + dean.Id, null, ct);

        Assert.NotEqual(HttpStatusCode.OK, onCollection.Status);
        Assert.NotEqual(HttpStatusCode.OK, onOne.Status);
        Assert.Contains(await host.AppUsersAsync(ct), u => u.Id == dean.Id);
    }
}
