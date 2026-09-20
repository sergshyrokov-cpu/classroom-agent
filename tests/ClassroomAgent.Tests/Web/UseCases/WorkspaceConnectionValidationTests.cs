using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-009 AC-006: every external input is validated before it reaches business logic, with the limits
/// stated explicitly and the rejected value never written to a log (spec VR-001…VR-003; SC-10;
/// <c>trebovaniya.md</c> §8). A malformed request is `400`; a well-formed one the state refuses is `409`
/// (api-design §2.4).
/// </summary>
public sealed class WorkspaceConnectionValidationTests(PostgreSqlFixture database)
{
    /// <summary>AC-006: the address must be an address, and nothing else gets through.</summary>
    [Theory]
    [MemberData(nameof(WorkspaceConnectionTestData.MalformedAddresses), MemberType = typeof(WorkspaceConnectionTestData))]
    public async Task AMalformedAddress_IsRejectedWithoutWriting(string address)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var response = await client.SaveConnectionAsync(address, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Empty(await host.WorkspaceConnectionsAsync(ct));
    }

    /// <summary>AC-006: the rejection is per field and translated.</summary>
    [Fact]
    public async Task AMissingAddress_IsRejectedWithAMessageOnTheField()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var response = await client.SaveConnectionAsync(string.Empty, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(
            host.Text(WorkspaceConnectionTestData.TextKeys.ValidationRequired, "uk"),
            response.Text,
            StringComparison.Ordinal);
    }

    /// <summary>AC-006: an address longer than VR-001 allows is rejected at the boundary.</summary>
    [Fact]
    public async Task AnAddressLongerThanTheLimit_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var response = await client.SaveConnectionAsync(WorkspaceConnectionTestData.TooLongAccount, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Empty(await host.WorkspaceConnectionsAsync(ct));
    }

    /// <summary>AC-006: an address of exactly the limit is accepted — the boundary is inclusive (VR-001).</summary>
    [Fact]
    public async Task AnAddressOfExactlyTheLimit_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        var name = new string('a', 254 - 1 - WorkspaceConnectionTestData.AllowedDomain.Length);
        var address = name + "@" + WorkspaceConnectionTestData.AllowedDomain;
        Assert.Equal(254, address.Length);

        var response = await client.SaveConnectionAsync(address, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        var row = Assert.Single(await host.WorkspaceConnectionsAsync(ct));
        Assert.Equal(address, row.ImpersonationUserEmail);
    }

    /// <summary>AC-006: a request without the field at all is rejected, not treated as an empty address.</summary>
    [Fact]
    public async Task ARequestWithoutTheField_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        await client.OpenSettingsAsync(ct);
        await client.GetAsync(SignInTestData.LandingPath, ct);

        var response = await client.PostFormAsync(WorkspaceConnectionTestData.Path, [], ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Empty(await host.WorkspaceConnectionsAsync(ct));
    }

    /// <summary>AC-006, SC-10: the rejected value never reaches the log.</summary>
    [Fact]
    public async Task TheRejectedValue_IsNotLogged()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        const string address = "not-an-address-but-memorable";

        await client.SaveConnectionAsync(address, ct);
        var events = await host.ReadLogEventsAsync(ct);

        Assert.DoesNotContain(events, e => e.Line.Contains(address, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>AC-006, SC-10: a refused save logs its category, never the address the Admin typed.</summary>
    [Fact]
    public async Task ARefusedSave_LogsNoAddress()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        await client.SaveConnectionAsync(WorkspaceConnectionTestData.NeighbouringSchoolAccount, ct);
        var events = await host.ReadLogEventsAsync(ct);

        Assert.DoesNotContain(
            events,
            e => e.Line.Contains(WorkspaceConnectionTestData.NeighbouringSchoolAccount, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>AC-003, SC-10: a successful save logs the actor and the request, never the address or the domain.</summary>
    [Fact]
    public async Task ASuccessfulSave_LogsNoAddress()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);
        var events = await host.ReadLogEventsAsync(ct);

        Assert.DoesNotContain(
            events,
            e => e.Line.Contains(WorkspaceConnectionTestData.TechnicalAccount, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>AC-006: validation runs before the business rules — a malformed foreign address is `400`, not `409`.</summary>
    [Fact]
    public async Task AMalformedForeignAddress_IsRejectedAsMalformed()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var response = await client.SaveConnectionAsync("two@at@school-two.example.test", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
    }
}
