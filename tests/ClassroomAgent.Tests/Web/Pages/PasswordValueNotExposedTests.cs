using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-069 AC-009, a guard that holds before and after the Story: a password a person typed into a refused form never
/// comes back in the page and is never written to a log (spec FR-006, SC-10).
/// </summary>
public sealed class PasswordValueNotExposedTests(PostgreSqlFixture database)
{
    private const string Marker = "marker-pass-value-7f3c-long-enough";

    private static async Task AssertMarkerAbsentAsync(InstallationTestHost host, PageResponse page, CancellationToken ct)
    {
        Assert.DoesNotContain(Marker, page.Body, StringComparison.Ordinal);
        var logs = await host.ReadLogFilesAsync(ct);
        Assert.NotEmpty(logs);
        Assert.DoesNotContain(Marker, string.Join(Environment.NewLine, logs), StringComparison.Ordinal);
    }

    /// <summary>AC-009: a wrong Dean sign-in does not echo the typed password.</summary>
    [Fact]
    public async Task AWrongDeanSignIn_DoesNotExposeThePassword()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.InsertDeanAsync(ct);
        using var client = host.CreateClient();

        var page = await client.SignInAsDeanAsync(DeanAccountTestData.DeanEmail, Marker, ct);

        // A refused Dean sign-in answers by a redirect back to the form, so the form is read as well.
        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(DeanAccountTestData.Paths.SignIn, page.LocationPath);
        var form = await client.GetAsync(DeanAccountTestData.Paths.SignIn, ct);
        Assert.DoesNotContain(Marker, form.Body, StringComparison.Ordinal);
        await AssertMarkerAbsentAsync(host, page, ct);
    }

    /// <summary>AC-009: a refused Dean creation does not echo the typed temporary password.</summary>
    [Fact]
    public async Task ARefusedDeanCreation_DoesNotExposeThePassword()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;

        var page = await client.CreateDeanAsync("not-an-email", Marker, ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        await AssertMarkerAbsentAsync(host, page, ct);
    }

    /// <summary>AC-009: a wrong current password on the own page does not echo either password.</summary>
    [Fact]
    public async Task AWrongCurrentPassword_DoesNotExposeThePassword()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        using var _client = client;
        await client.GetAsync(DeanAccountTestData.Paths.OwnPassword, ct);

        var page = await client.PostFormAsync(
            DeanAccountTestData.Paths.OwnPassword,
            [
                new(DeanAccountTestData.Fields.CurrentPassword, Marker),
                new(DeanAccountTestData.Fields.NewPassword, DeanAccountTestData.NewPassword),
            ],
            ct);

        await AssertMarkerAbsentAsync(host, page, ct);
    }
}
