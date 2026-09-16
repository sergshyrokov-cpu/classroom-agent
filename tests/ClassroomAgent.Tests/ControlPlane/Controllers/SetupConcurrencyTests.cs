using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-004: two setup submissions at once create one account; the loser gets 409, never 500 (FR-006).</summary>
public sealed class SetupConcurrencyTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task TwoConcurrentSetupPosts_OneRedirects_OtherReturns409()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var first = host.CreateClient();
        using var second = host.CreateClient();
        await first.GetAsync("/setup", ct);
        await second.GetAsync("/setup", ct);

        var responses = await Task.WhenAll(
            first.PostFormAsync("/setup", TestData.SetupFields(login: "owner.first"), ct),
            second.PostFormAsync("/setup", TestData.SetupFields(login: "owner.second"), ct));

        Assert.Equal(
            [HttpStatusCode.Redirect, HttpStatusCode.Conflict],
            responses.Select(r => r.Status).Order().ToArray());
        Assert.Equal(1, await host.OwnerCountAsync(ct));
        var rows = await host.AuditRowsAsync(ct);
        var row = Assert.Single(rows);
        Assert.Equal("owner_first_run_setup", row.Action);
        Assert.Equal("succeeded", row.Outcome);
    }
}
