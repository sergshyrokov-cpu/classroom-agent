using ClassroomAgent.ControlPlane.Services;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.ControlPlane.Services;

/// <summary>AC-004: the setup service creates one account under true concurrency (FR-006, db-design §3.1).</summary>
public sealed class FirstRunSetupConcurrencyTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task TwoConcurrentSetups_CreateOneOwner_LoserGetsConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var firstScope = host.CreateScope();
        using var secondScope = host.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<FirstRunSetupService>();
        var second = secondScope.ServiceProvider.GetRequiredService<FirstRunSetupService>();

        var results = await Task.WhenAll(
            Task.Run(() => first.CreateOwnerAsync("owner.first", TestData.Password, host.CodeGenerator.Code, "request-1", ct), ct),
            Task.Run(() => second.CreateOwnerAsync("owner.second", TestData.Password, host.CodeGenerator.Code, "request-2", ct), ct));

        Assert.Equal(
            [FirstRunSetupOutcome.Created, FirstRunSetupOutcome.AlreadyExists],
            results.Select(r => r.Outcome).Order().ToArray());
        Assert.NotNull(results.Single(r => r.Outcome == FirstRunSetupOutcome.Created).Session);
        Assert.Null(results.Single(r => r.Outcome == FirstRunSetupOutcome.AlreadyExists).Session);
        Assert.Equal(1, await host.OwnerCountAsync(ct));
        var row = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal("owner_first_run_setup", row.Action);
        Assert.Equal("succeeded", row.Outcome);
        Assert.Equal((await host.OwnerAsync(ct))!.Id, row.ActorId);
    }
}
