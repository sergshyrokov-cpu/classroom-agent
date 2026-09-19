using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>
/// US-006 AC-003: setting, changing and clearing the push address each write one
/// <c>installation_push_address_changed</c> row with the Owner as actor and no address in it; a refused or
/// unchanged submission writes none (spec FR-003; db-design §3.3; SC-11).
/// </summary>
public sealed class InstallationPushAddressAuditTests(PostgreSqlFixture database)
{
    private const string Action = "installation_push_address_changed";

    [Fact]
    public async Task SettingTheAddress_WritesOneRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct, pushAddress: null);
        var ownerId = (await host.OwnerAsync(ct))!.Id;
        var installationId = (await host.InstallationAsync(installation, ct))!.Id;

        await owner.ChangePushAddressAsync(installation, PushTestData.Address, ct);

        var row = Assert.Single(await PushAddressRowsAsync(host, ct));
        Assert.Equal("owner", row.ActorType);
        Assert.Equal(ownerId, row.ActorId);
        Assert.Equal("installation", row.TargetType);
        Assert.Equal(installationId, row.TargetId);
        Assert.Equal("succeeded", row.Outcome);
        Assert.Null(row.RefusalCategory);
        Assert.False(string.IsNullOrEmpty(row.RequestId));
        Assert.Equal(host.Time.GetUtcNow(), row.OccurredAt);
    }

    [Fact]
    public async Task ChangingAndClearing_EachWriteOneRow_WithTheSameAction()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        await owner.ChangePushAddressAsync(installation, PushTestData.OtherAddress, ct);
        await owner.ChangePushAddressAsync(installation, string.Empty, ct);

        Assert.Equal(2, (await PushAddressRowsAsync(host, ct)).Count);
    }

    [Fact]
    public async Task RowsCarryNoAddress()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct, pushAddress: null);
        await owner.ChangePushAddressAsync(installation, PushTestData.Address, ct);

        var rows = await host.AuditRowsAsJsonAsync(ct);

        Assert.DoesNotContain(rows, row => row.Contains("10.0.0.5", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RegistrationWithAnAddress_WritesOnlyTheRegistrationRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        var rows = await host.AuditRowsAsync(ct);
        Assert.Contains(rows, row => row.Action == "installation_created");
        Assert.DoesNotContain(rows, row => row.Action == Action);
    }

    [Fact]
    public async Task UnchangedSubmission_WritesNoRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        await owner.ChangePushAddressAsync(installation, PushTestData.Address, ct);

        Assert.Empty(await PushAddressRowsAsync(host, ct));
    }

    [Fact]
    public async Task RefusedSubmission_WritesNoRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct, pushAddress: null);

        await owner.ChangePushAddressAsync(installation, "https://10.0.0.5:8081", ct);

        Assert.Empty(await PushAddressRowsAsync(host, ct));
    }

    [Fact]
    public async Task ChangeAndItsRow_AreWrittenTogether()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct, pushAddress: null);

        await owner.ChangePushAddressAsync(installation, PushTestData.Address, ct);

        var stored = await host.PushAddressAsync(installation, ct);
        var rows = await PushAddressRowsAsync(host, ct);
        Assert.Equal(stored is null, rows.Count == 0);
    }

    [Fact]
    public async Task AuditRow_IsNotUpdatable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct, pushAddress: null);
        await owner.ChangePushAddressAsync(installation, PushTestData.Address, ct);

        await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
            host.ExecuteAsync("UPDATE audit_event SET outcome = 'refused' WHERE action = @action", ct, ("action", Action)));
    }

    private static async Task<IReadOnlyList<AuditRow>> PushAddressRowsAsync(
        ControlPlaneTestHost host,
        CancellationToken cancellationToken) =>
        (await host.AuditRowsAsync(cancellationToken)).Where(row => row.Action == Action).ToList();
}
