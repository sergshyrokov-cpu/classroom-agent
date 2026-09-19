using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>
/// US-006 AC-002: every push address rule of VR-001, with the message key of the first failing rule;
/// a refused submission stores nothing and never repeats the value outside the refilled input
/// (api-design §7; SC-10).
/// </summary>
public sealed class InstallationPushAddressValidationTests(PostgreSqlFixture database)
{
    public static TheoryData<string, string> InvalidAddresses => new()
    {
        { new string('a', 250) + ".test:8081", "Installation.PushAddress.Length" },
        { "not-an-address", "Installation.PushAddress.Format" },
        { "10.0.0.5:8081", "Installation.PushAddress.Format" },
        { "/service/v1/status-pushes", "Installation.PushAddress.Format" },
        { "https://10.0.0.5:8081", "Installation.PushAddress.Scheme" },
        { "ftp://10.0.0.5:8081", "Installation.PushAddress.Scheme" },
        { "http://user:secret@10.0.0.5:8081", "Installation.PushAddress.Extra" },
        { "http://10.0.0.5:8081/push", "Installation.PushAddress.Extra" },
        { "http://10.0.0.5:8081/?x=1", "Installation.PushAddress.Extra" },
        { "http://10.0.0.5:8081/#part", "Installation.PushAddress.Extra" },
        { "http://school_a.private:8081", "Installation.PushAddress.Host" },
        { "http://-school.private:8081", "Installation.PushAddress.Host" },
        { "http://fd00::5:8081", "Installation.PushAddress.Host" },
        { "http://10.0.0.5", "Installation.PushAddress.Port" },
        { "http://10.0.0.5:0", "Installation.PushAddress.Port" },
        { "http://10.0.0.5:70000", "Installation.PushAddress.Port" },
    };

    public static TheoryData<string> ValidAddresses =>
    [
        "http://10.0.0.5:8081",
        "http://10.0.0.5:1",
        "http://10.0.0.5:65535",
        "http://school-a.private:8081",
        "http://school-a.private.example.test:443",
        "http://[fd00::5]:8081",
        "http://127.0.0.1:8081",
    ];

    [Theory]
    [MemberData(nameof(InvalidAddresses))]
    public async Task InvalidAddress_OnThePushAddressPage_IsRefusedWithItsMessage(string address, string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct, pushAddress: null);

        var response = await owner.ChangePushAddressAsync(installation, address, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(host.Text(key, "uk"), response.Body, StringComparison.Ordinal);
        Assert.Null(await host.PushAddressAsync(installation, ct));
    }

    [Theory]
    [MemberData(nameof(InvalidAddresses))]
    public async Task InvalidAddress_AtRegistration_IsRefusedAndRegistersNothing(string address, string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await owner.GetAsync("/installations/new", ct);
        var fields = InstallationTestData.RegisterFields().ToList();
        fields.Add(new(PushTestData.FieldName, address));

        var response = await owner.PostFormAsync("/installations", fields, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(host.Text(key, "uk"), response.Body, StringComparison.Ordinal);
        Assert.Empty(await host.InstallationsAsync(ct));
    }

    [Theory]
    [MemberData(nameof(ValidAddresses))]
    public async Task ValidAddress_IsAccepted(string address)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct, pushAddress: null);

        var response = await owner.ChangePushAddressAsync(installation, address, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.NotNull(await host.PushAddressAsync(installation, ct));
    }

    [Fact]
    public async Task RefusedForm_RefillsTheTypedValue()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct, pushAddress: null);

        var response = await owner.ChangePushAddressAsync(installation, "https://10.0.0.5:8081", ct);

        Assert.Equal("https://10.0.0.5:8081", Html.InputValue(response.Body, PushTestData.FieldName));
    }

    [Fact]
    public async Task InvalidAddressAtRegistration_IsReportedTogetherWithOtherFieldErrors()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await owner.GetAsync("/installations/new", ct);
        var fields = InstallationTestData.RegisterFields(name: string.Empty).ToList();
        fields.Add(new(PushTestData.FieldName, "https://10.0.0.5:8081"));

        var response = await owner.PostFormAsync("/installations", fields, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(host.Text("Installation.Name.Required", "uk"), response.Body, StringComparison.Ordinal);
        Assert.Contains(host.Text("Installation.PushAddress.Scheme", "uk"), response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefusedForm_EncodesTheTypedValue()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct, pushAddress: null);

        var response = await owner.ChangePushAddressAsync(installation, "\"><script>alert(1)</script>", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.DoesNotContain("<script>", response.Body, StringComparison.Ordinal);
        Assert.Null(await host.PushAddressAsync(installation, ct));
    }

    [Fact]
    public async Task RefusedSubmission_IsNotWrittenToTheLog()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct, pushAddress: null);
        await owner.ChangePushAddressAsync(installation, "https://secret-host.example.test:8081", ct);

        var logs = await host.ReadLogFilesAsync(ct);

        Assert.DoesNotContain(logs, log => log.Contains("secret-host.example.test", StringComparison.Ordinal));
    }
}
