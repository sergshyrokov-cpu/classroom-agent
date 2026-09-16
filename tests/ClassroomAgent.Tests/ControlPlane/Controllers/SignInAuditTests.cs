using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-008: every Owner sign-in outcome writes its audit row, with no personal data (FR-012, db-design §4.1).</summary>
public sealed class SignInAuditTests(PostgreSqlFixture database)
{
    private const string WrongPassword = "this is not the password";

    [Fact]
    public async Task SuccessfulSignIn_WritesSucceededRowWithOwnerActor()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var setup = await host.CreateOwnerAsync(ct);
        var owner = await host.OwnerAsync(ct);

        var (client, _) = await host.SignInAsync(TestData.Login, TestData.Password, ct);
        using var __ = client;

        var row = Assert.Single(await SignInRowsAsync(host, ct));
        AssertOwnerRow(host, row, owner!.Id, "succeeded", refusalCategory: null);
    }

    [Fact]
    public async Task WrongPassword_WritesRefusedWrongPassword()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var setup = await host.CreateOwnerAsync(ct);
        var owner = await host.OwnerAsync(ct);

        var (client, _) = await host.SignInAsync(TestData.Login, WrongPassword, ct);
        using var __ = client;

        var row = Assert.Single(await SignInRowsAsync(host, ct));
        AssertOwnerRow(host, row, owner!.Id, "refused", "wrong_password");
    }

    [Fact]
    public async Task LockedOut_WritesRefusedLockedOut()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var setup = await host.CreateOwnerAsync(ct);
        var owner = await host.OwnerAsync(ct);
        using var client = host.CreateClient();
        await client.GetAsync("/sign-in", ct);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await client.PostFormAsync("/sign-in", TestData.SignInFields(password: WrongPassword), ct);
        }

        await client.PostFormAsync("/sign-in", TestData.SignInFields(), ct);

        var rows = await SignInRowsAsync(host, ct);
        Assert.Equal(6, rows.Count);
        Assert.All(rows.Take(5), r => AssertOwnerRow(host, r, owner!.Id, "refused", "wrong_password"));
        AssertOwnerRow(host, rows[5], owner!.Id, "refused", "locked_out");
    }

    [Fact]
    public async Task UnknownLogin_WritesRefusedAnonymousWithoutTarget()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var setup = await host.CreateOwnerAsync(ct);

        var (client, _) = await host.SignInAsync("nobody.here", TestData.Password, ct);
        using var __ = client;

        var row = Assert.Single(await SignInRowsAsync(host, ct));
        Assert.Equal("anonymous", row.ActorType);
        Assert.Null(row.ActorId);
        Assert.Null(row.TargetType);
        Assert.Null(row.TargetId);
        Assert.Equal("refused", row.Outcome);
        Assert.Equal("unknown_login", row.RefusalCategory);
        Assert.False(string.IsNullOrWhiteSpace(row.RequestId));
        Assert.Equal(host.Time.GetUtcNow(), row.OccurredAt);
        Assert.Equal(row.CreatedAt, row.UpdatedAt);
    }

    [Fact]
    public async Task AuditRows_ContainNoTypedLoginOrPassword()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var setup = await host.CreateOwnerAsync(ct);
        const string unknownLogin = "someone.else";
        using var client = host.CreateClient();
        await client.GetAsync("/sign-in", ct);

        await client.PostFormAsync("/sign-in", TestData.SignInFields(unknownLogin, WrongPassword), ct);
        await client.PostFormAsync("/sign-in", TestData.SignInFields(password: WrongPassword), ct);
        await client.PostFormAsync("/sign-in", TestData.SignInFields(), ct);

        var rows = await host.AuditRowsAsJsonAsync(ct);
        Assert.True(rows.Count >= 4, $"Expected the setup row and three sign-in rows, found {rows.Count}.");
        foreach (var typed in new[] { TestData.Login, unknownLogin, TestData.Password, WrongPassword })
        {
            Assert.All(rows, row => Assert.DoesNotContain(typed, row, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static async Task<List<AuditRow>> SignInRowsAsync(ControlPlaneTestHost host, CancellationToken cancellationToken) =>
        (await host.AuditRowsAsync(cancellationToken)).Where(r => r.Action == "owner_sign_in").ToList();

    private static void AssertOwnerRow(ControlPlaneTestHost host, AuditRow row, long ownerId, string outcome, string? refusalCategory)
    {
        Assert.Equal("owner", row.ActorType);
        Assert.Equal(ownerId, row.ActorId);
        Assert.Equal("owner_sign_in", row.Action);
        Assert.Equal("owner", row.TargetType);
        Assert.Equal(ownerId, row.TargetId);
        Assert.Equal(outcome, row.Outcome);
        Assert.Equal(refusalCategory, row.RefusalCategory);
        Assert.False(string.IsNullOrWhiteSpace(row.RequestId));
        Assert.Equal(host.Time.GetUtcNow(), row.OccurredAt);
        Assert.Equal(row.CreatedAt, row.UpdatedAt);
    }
}
