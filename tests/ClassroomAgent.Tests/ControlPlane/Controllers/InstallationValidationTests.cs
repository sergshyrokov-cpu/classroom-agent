using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-003: invalid name, domain and client ID are rejected; boundaries are accepted (VR-001 … VR-004, OD-001, OD-002).</summary>
public sealed class InstallationValidationTests(PostgreSqlFixture database)
{
    private static readonly string Label63 = new('a', 63);

    public static TheoryData<string, string> InvalidNames => new()
    {
        { string.Empty, "Installation.Name.Required" },
        { new string('я', 201), "Installation.Name.Length" },
        { string.Concat(Enumerable.Repeat("𝔸", 201)), "Installation.Name.Length" },
        { " Ліцей", "Installation.Name.EdgeWhitespace" },
        { "Ліцей ", "Installation.Name.EdgeWhitespace" },
        { " Ліцей", "Installation.Name.EdgeWhitespace" },
        { "Ліцей\n№1", "Installation.Name.InvalidCharacters" },
        { "Ліцей\t№1", "Installation.Name.InvalidCharacters" },
        { "Ліцей​№1", "Installation.Name.InvalidCharacters" },
        { "Ліцей‏№1", "Installation.Name.InvalidCharacters" },
    };

    public static TheoryData<string> ValidNames => new()
    {
        "Я",
        new string('я', 200),
        string.Concat(Enumerable.Repeat("𝔸", 200)),
        "Ліцей «Надія» №1 — корпус Б",
    };

    public static TheoryData<string, string> InvalidDomains => new()
    {
        { string.Empty, "Installation.Domain.Required" },
        { "a.", "Installation.Domain.Length" },
        { string.Join('.', Label63, Label63, Label63, new string('b', 62)), "Installation.Domain.Length" },
        { "https://school.example.test", "Installation.Domain.Characters" },
        { "school.example.test/path", "Installation.Domain.Characters" },
        { "admin@school.example.test", "Installation.Domain.Characters" },
        { "ліцей.укр", "Installation.Domain.Characters" },
        { "school one.example.test", "Installation.Domain.Characters" },
        { "school_one.example.test", "Installation.Domain.Characters" },
        { "localhost", "Installation.Domain.NoDot" },
        { ".school.example", "Installation.Domain.Labels" },
        { "school.example.", "Installation.Domain.Labels" },
        { "school..example", "Installation.Domain.Labels" },
        { "-school.example", "Installation.Domain.Labels" },
        { "school-.example", "Installation.Domain.Labels" },
        { "school.-example", "Installation.Domain.Labels" },
        { new string('a', 64) + ".example", "Installation.Domain.Labels" },
        { "xn--80a.example", "Installation.Domain.Idn" },
        { "school.XN--p1ai", "Installation.Domain.Idn" },
    };

    public static TheoryData<string, string> ValidDomains => new()
    {
        { "a.b", "a.b" },
        { string.Join('.', Label63, Label63, Label63, new string('b', 61)), string.Join('.', Label63, Label63, Label63, new string('b', 61)) },
        { Label63 + ".example", Label63 + ".example" },
        { "my-school.edu.example", "my-school.edu.example" },
        { "1school.example", "1school.example" },
        { "DAC.Ukr.Education", "dac.ukr.education" },
    };

    public static TheoryData<string, string> InvalidClientIds => new()
    {
        { string.Empty, "Installation.ClientId.Required" },
        { "123456789", "Installation.ClientId.Format" },
        { new string('1', 33), "Installation.ClientId.Format" },
        { "12345 67890", "Installation.ClientId.Format" },
        { "1234567890a", "Installation.ClientId.Format" },
        { "+1234567890", "Installation.ClientId.Format" },
        { "١٢٣٤٥٦٧٨٩٠", "Installation.ClientId.Format" },
    };

    public static TheoryData<string> ValidClientIds => new()
    {
        "1234567890",
        new string('9', 32),
        "0123456789",
        "123456789012345678901",
    };

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public async Task Register_InvalidName_Returns400WithMessage_CreatesNothing(string name, string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var response = await owner.PostFromPageAsync(
            "/installations/new",
            "/installations",
            InstallationTestData.RegisterFields(name: name),
            ct);

        AssertRejected(host, response, key);
        Assert.Equal(name, Html.InputValue(response.Body, "name"));
        Assert.Equal(InstallationTestData.Domain, Html.InputValue(response.Body, "domain"));
        Assert.Equal(InstallationTestData.ClientId, Html.InputValue(response.Body, "clientId"));
        Assert.Empty(await host.InstallationsAsync(ct));
    }

    [Theory]
    [MemberData(nameof(ValidNames))]
    public async Task Register_NameAtBoundaryOrWithAnyScript_IsAccepted(string name)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var identifier = await host.RegisterInstallationAsync(owner, ct, name: name);

        Assert.Equal(name, (await host.InstallationAsync(identifier, ct))?.Name);
    }

    [Theory]
    [MemberData(nameof(InvalidDomains))]
    public async Task Register_InvalidDomain_Returns400WithMessage_CreatesNothing(string domain, string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var response = await owner.PostFromPageAsync(
            "/installations/new",
            "/installations",
            InstallationTestData.RegisterFields(domain: domain),
            ct);

        AssertRejected(host, response, key);
        Assert.Equal(domain, Html.InputValue(response.Body, "domain"));
        Assert.Empty(await host.InstallationsAsync(ct));
    }

    [Theory]
    [MemberData(nameof(ValidDomains))]
    public async Task Register_DomainAtBoundary_IsAcceptedAndStoredLowerCase(string domain, string stored)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var identifier = await host.RegisterInstallationAsync(owner, ct, domain: domain);

        Assert.Equal(stored, (await host.InstallationAsync(identifier, ct))?.Domain);
    }

    [Theory]
    [MemberData(nameof(InvalidClientIds))]
    public async Task Register_InvalidClientId_Returns400WithMessage_CreatesNothing(string clientId, string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var response = await owner.PostFromPageAsync(
            "/installations/new",
            "/installations",
            InstallationTestData.RegisterFields(clientId: clientId),
            ct);

        AssertRejected(host, response, key);
        Assert.Equal(clientId, Html.InputValue(response.Body, "clientId"));
        Assert.Empty(await host.InstallationsAsync(ct));
    }

    [Theory]
    [MemberData(nameof(ValidClientIds))]
    public async Task Register_ClientIdAtBoundary_IsAcceptedAsDigitString(string clientId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var identifier = await host.RegisterInstallationAsync(owner, ct, clientId: clientId);

        Assert.Equal(clientId, (await host.InstallationAsync(identifier, ct))?.ClientId);
    }

    [Fact]
    public async Task Register_AllFieldsInvalid_ReportsEveryField()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var response = await owner.PostFromPageAsync(
            "/installations/new",
            "/installations",
            InstallationTestData.RegisterFields(name: string.Empty, domain: "localhost", clientId: "12"),
            ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(host.Text("Installation.Name.Required", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text("Installation.Domain.NoDot", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text("Installation.ClientId.Format", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Empty(await host.InstallationsAsync(ct));
    }

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public async Task Rename_InvalidName_Returns400WithMessage_NameUnchanged(string name, string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.RegisterInstallationAsync(owner, ct);

        var response = await owner.PostFromPageAsync(
            $"/installations/{identifier:D}/name",
            $"/installations/{identifier:D}/name",
            InstallationTestData.NameFields(name),
            ct);

        AssertRejected(host, response, key);
        Assert.Equal(name, Html.InputValue(response.Body, "name"));
        Assert.Equal(InstallationTestData.Name, (await host.InstallationAsync(identifier, ct))?.Name);
    }

    [Theory]
    [MemberData(nameof(InvalidClientIds))]
    public async Task ChangeClientId_InvalidClientId_Returns400WithMessage_ClientIdUnchanged(string clientId, string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.RegisterInstallationAsync(owner, ct);

        var response = await owner.PostFromPageAsync(
            $"/installations/{identifier:D}/client-id",
            $"/installations/{identifier:D}/client-id",
            InstallationTestData.ClientIdFields(clientId),
            ct);

        AssertRejected(host, response, key);
        Assert.Equal(clientId, Html.InputValue(response.Body, "clientId"));
        Assert.Equal(InstallationTestData.ClientId, (await host.InstallationAsync(identifier, ct))?.ClientId);
    }

    [Fact]
    public async Task RejectedAndStoredValues_NeverReachTheLogFile()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        const string rejectedName = "Відхилена назва​";
        const string rejectedDomain = "rejected-domain.example.";
        const string rejectedClientId = "98765x43210";
        using (var owner = await host.CreateOwnerAsync(ct))
        {
            await owner.PostFromPageAsync(
                "/installations/new",
                "/installations",
                InstallationTestData.RegisterFields(rejectedName, rejectedDomain, rejectedClientId),
                ct);
            var identifier = await host.RegisterInstallationAsync(owner, ct);
            await owner.PostFromPageAsync(
                "/installations/new",
                "/installations",
                InstallationTestData.RegisterFields(domain: InstallationTestData.Domain.ToUpperInvariant()),
                ct);
            await owner.PostFromPageAsync(
                $"/installations/{identifier:D}/name",
                $"/installations/{identifier:D}/name",
                InstallationTestData.NameFields(InstallationTestData.OtherName),
                ct);
            await owner.PostFromPageAsync(
                $"/installations/{identifier:D}/client-id",
                $"/installations/{identifier:D}/client-id",
                InstallationTestData.ClientIdFields(InstallationTestData.OtherClientId),
                ct);
        }

        var logs = await host.ReadLogFilesAsync(ct);

        Assert.Contains(logs, content => content.Length > 0);
        var values = new[]
        {
            "Відхилена назва",
            rejectedDomain,
            rejectedClientId,
            InstallationTestData.Name,
            InstallationTestData.OtherName,
            InstallationTestData.Domain,
            InstallationTestData.ClientId,
            InstallationTestData.OtherClientId,
        };
        foreach (var value in values)
        {
            Assert.All(logs, content => Assert.DoesNotContain(value, content, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static void AssertRejected(ControlPlaneTestHost host, PageResponse response, string key)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Null(response.Location);
        Assert.Contains(host.Text(key, "uk"), response.Text, StringComparison.Ordinal);
    }
}
