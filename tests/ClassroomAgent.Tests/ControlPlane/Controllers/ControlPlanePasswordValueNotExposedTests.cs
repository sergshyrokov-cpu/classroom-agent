using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>
/// US-069 AC-009 on the Control Plane, a guard that holds before and after the Story: a password typed into a refused
/// sign-in or setup form never comes back in the page and is never written to a log (spec FR-006, SC-10).
/// </summary>
public sealed class ControlPlanePasswordValueNotExposedTests(PostgreSqlFixture database)
{
    private const string Marker = "marker-pass-value-7f3c-long-enough";

    private static async Task AssertMarkerAbsentAsync(ControlPlaneTestHost host, PageResponse page, CancellationToken ct)
    {
        Assert.DoesNotContain(Marker, page.Body, StringComparison.Ordinal);
        var logs = await host.ReadLogFilesAsync(ct);
        Assert.NotEmpty(logs);
        Assert.DoesNotContain(Marker, string.Join(Environment.NewLine, logs), StringComparison.Ordinal);
    }

    /// <summary>AC-009: a wrong Owner sign-in does not echo the typed password.</summary>
    [Fact]
    public async Task AWrongOwnerSignIn_DoesNotExposeThePassword()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        (await host.CreateOwnerAsync(ct)).Dispose();

        var (client, page) = await host.SignInAsync(TestData.Login, Marker, ct);
        using var _client = client;

        await AssertMarkerAbsentAsync(host, page, ct);
    }

    /// <summary>AC-009: a refused setup does not echo the typed password.</summary>
    [Fact]
    public async Task ARefusedSetup_DoesNotExposeThePassword()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();
        await client.GetAsync("/setup", ct);

        var page = await client.PostFormAsync("/setup", TestData.SetupFields(TestData.OtherSetupCode, password: Marker), ct);

        await AssertMarkerAbsentAsync(host, page, ct);
    }
}
