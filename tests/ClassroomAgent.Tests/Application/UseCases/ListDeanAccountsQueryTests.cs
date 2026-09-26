using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-012 AC-001: the Admin's list — every Dean, active and disabled alike, with the four columns OD-003 fixed
/// (spec FR-011). It lists no Admin account, and it is never blocked in read-only mode (BR-026).
/// </summary>
public sealed class ListDeanAccountsQueryTests
{
    /// <summary>AC-001: both an active and a disabled Dean appear.</summary>
    [Fact]
    public async Task ItLists_ActiveAndDisabledDeansAlike()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        var active = world.SeedDean();
        var disabled = world.SeedDean(DeanAccountTestData.SecondDeanEmail);
        await world.SetState.ExecuteAsync(admin.Id, disabled.Id, disable: true, "r-1", ct);

        var rows = await world.List.ExecuteAsync(ct);

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Id == active.Id && !r.IsDisabled);
        Assert.Contains(rows, r => r.Id == disabled.Id && r.IsDisabled);
    }

    /// <summary>AC-001, I-8: an Admin account never appears on the Dean screen.</summary>
    [Fact]
    public async Task ItLists_NoAdminAccount()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var admin = world.SeedAdmin();
        world.SeedDean();

        var rows = await world.List.ExecuteAsync(ct);

        Assert.DoesNotContain(rows, r => r.Id == admin.Id);
        Assert.DoesNotContain(rows, r => r.Email == admin.Email);
    }

    /// <summary>AC-001: the four columns of OD-003, including the temporary-password state.</summary>
    [Fact]
    public async Task EachRow_CarriesTheFourColumnsOd003Fixed()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();

        var row = Assert.Single(await world.List.ExecuteAsync(ct));

        Assert.Equal(dean.Id, row.Id);
        Assert.Equal(DeanAccountTestData.DeanEmail, row.Email);
        Assert.False(row.IsDisabled);
        Assert.True(row.PasswordIsTemporary);
        Assert.Null(row.LastSuccessfulSignInAt);
    }

    /// <summary>AC-001: a Dean who has signed in carries the date the retention clock counts from (PC-11).</summary>
    [Fact]
    public async Task ASignedInDean_CarriesTheLastSignInDate()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();
        await world.ForcedChange.ExecuteAsync(dean.Id, DeanAccountTestData.NewPassword, "r-1", ct);
        await world.SignIn.ExecuteAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.NewPassword, "r-2", ct);

        var row = Assert.Single(await world.List.ExecuteAsync(ct));

        Assert.Equal(world.Time.GetUtcNow(), row.LastSuccessfulSignInAt);
        Assert.False(row.PasswordIsTemporary);
    }

    /// <summary>AC-009: viewing keeps working in read-only mode — reading is never blocked (BR-026).</summary>
    [Fact]
    public async Task ItWorks_InReadOnlyMode()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld(readOnly: true);
        world.SeedDean();

        var rows = await world.List.ExecuteAsync(ct);

        Assert.Single(rows);
        Assert.Empty(world.ReadOnly.Operations);
    }

    /// <summary>S-10: no row carries a password hash, a security stamp or the lockout state.</summary>
    [Fact]
    public async Task NoRow_CarriesASecret()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new DeanAccountWorld();
        var dean = world.SeedDean();

        var row = Assert.Single(await world.List.ExecuteAsync(ct));

        var rendered = row.ToString() ?? string.Empty;
        Assert.DoesNotContain(DeanAccountWorld.Hashes.Marker, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(dean.SecurityStamp, rendered, StringComparison.Ordinal);
    }
}
