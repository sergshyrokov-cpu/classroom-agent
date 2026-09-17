using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-003: invalid emails are rejected with the first failing rule; nothing is created or logged (VR-001 … VR-003).</summary>
public sealed class AllowedAdminValidationTests(PostgreSqlFixture database)
{
    private const string Domain = InstallationTestData.Domain;

    public static TheoryData<string, string> InvalidFormats => new()
    {
        { string.Empty, "AllowedAdmin.Email.Required" },
        // 231 + 1 + 23 = 255 characters.
        { new string('a', 231) + "@" + Domain, "AllowedAdmin.Email.Length" },
        { Domain, "AllowedAdmin.Email.Format" },
        { "a@b@" + Domain, "AllowedAdmin.Email.Format" },
        { "@" + Domain, "AllowedAdmin.Email.Format" },
        { "ivan@", "AllowedAdmin.Email.Format" },
        { new string('a', 65) + "@" + Domain, "AllowedAdmin.Email.NameLength" },
        { "ivan+x@" + Domain, "AllowedAdmin.Email.NameCharacters" },
        { "іван@" + Domain, "AllowedAdmin.Email.NameCharacters" },
        { "iv an@" + Domain, "AllowedAdmin.Email.NameCharacters" },
        { " ivan@" + Domain, "AllowedAdmin.Email.NameCharacters" },
        { "ivań@" + Domain, "AllowedAdmin.Email.NameCharacters" },
        { "iv..an+@" + Domain, "AllowedAdmin.Email.NameCharacters" },
        { ".ivan@" + Domain, "AllowedAdmin.Email.NameDots" },
        { "ivan.@" + Domain, "AllowedAdmin.Email.NameDots" },
        { "iv..an@" + Domain, "AllowedAdmin.Email.NameDots" },
    };

    public static TheoryData<string> ForeignDomains => new()
    {
        "ivan@gmail.com",
        "ivan@school-two.example.test",
        "ivan@mail.school-one.example.test",
        "ivan@example.test",
        "ivan@school-one.example.test.",
        "ivan@school-one.example.test ",
    };

    [Theory]
    [MemberData(nameof(InvalidFormats))]
    public async Task Add_InvalidFormat_Returns400WithRuleKey_KeepsValue_CreatesNothing(string email, string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

        var response = await owner.PostFromPageAsync(
            AllowedAdminTestData.AddFormPath(installation),
            AllowedAdminTestData.AddPath(installation),
            AllowedAdminTestData.AddFields(email),
            ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Null(response.Location);
        Assert.Contains(host.Text(key, "uk"), response.Text, StringComparison.Ordinal);
        Assert.Equal(email, Html.InputValue(response.Body, "email"));
        Assert.Empty(await host.AllowedAdminsAsync(ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Theory]
    [MemberData(nameof(ForeignDomains))]
    public async Task Add_EmailOutsideInstallationDomain_Returns400WrongDomain_NamingExpectedDomain(string email)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        await host.InsertInstallationAsync(
            ct,
            name: InstallationTestData.OtherName,
            domain: InstallationTestData.OtherDomain,
            clientId: InstallationTestData.OtherClientId);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

        var response = await owner.PostFromPageAsync(
            AllowedAdminTestData.AddFormPath(installation),
            AllowedAdminTestData.AddPath(installation),
            AllowedAdminTestData.AddFields(email),
            ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        var errorText = WrongDomainTemplatePrefix(host);
        Assert.Contains(errorText, response.Text, StringComparison.Ordinal);
        Assert.Contains(Domain, response.Text, StringComparison.Ordinal);
        Assert.Equal(email, Html.InputValue(response.Body, "email"));
        Assert.Empty(await host.AllowedAdminsAsync(ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Fact]
    public async Task Add_UnknownInstallation_WithInvalidEmail_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        await owner.GetAsync(AllowedAdminTestData.AddFormPath(installation), ct);

        var response = await owner.PostFormAsync(
            $"/installations/{InstallationTestData.UnknownIdentifier}/admins",
            AllowedAdminTestData.AddFields(string.Empty),
            ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.Contains(host.Text("Error.NotFound", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Empty(await host.AllowedAdminsAsync(ct));
    }

    [Fact]
    public async Task RejectedValues_AreHtmlEncodedWhenRefilled()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        const string markup = "\"><script>alert(1)</script>@" + Domain;

        var response = await owner.PostFromPageAsync(
            AllowedAdminTestData.AddFormPath(installation),
            AllowedAdminTestData.AddPath(installation),
            AllowedAdminTestData.AddFields(markup),
            ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.DoesNotContain("<script>alert(1)</script>", response.Body, StringComparison.Ordinal);
        Assert.Equal(markup, Html.InputValue(response.Body, "email"));
    }

    [Fact]
    public async Task RejectedAddedAndRevokedEmails_NeverReachTheLogFile()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        const string invalid = "rejected+name@school-one.example.test";
        const string foreign = "foreign.person@gmail.com";
        using (var owner = await host.CreateOwnerAsync(ct))
        {
            var installation = await host.InsertInstallationAsync(ct);
            foreach (var email in new[] { invalid, foreign })
            {
                await owner.PostFromPageAsync(
                    AllowedAdminTestData.AddFormPath(installation),
                    AllowedAdminTestData.AddPath(installation),
                    AllowedAdminTestData.AddFields(email),
                    ct);
            }

            var added = await host.AddAllowedAdminAsync(owner, installation, AllowedAdminTestData.Email, ct);
            await owner.PostFromPageAsync(
                AllowedAdminTestData.AddFormPath(installation),
                AllowedAdminTestData.AddPath(installation),
                AllowedAdminTestData.AddFields(AllowedAdminTestData.Email.ToUpperInvariant()),
                ct);
            await owner.RevokeAllowedAdminAsync(installation, added.Identifier, ct);
        }

        var logs = await host.ReadLogFilesAsync(ct);

        Assert.Contains(logs, content => content.Length > 0);
        foreach (var value in new[] { invalid, foreign, AllowedAdminTestData.Email, "ivan.petrenko", "rejected+name", "foreign.person" })
        {
            Assert.All(logs, content => Assert.DoesNotContain(value, content, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>The fixed part of the WrongDomain message before its domain argument.</summary>
    private static string WrongDomainTemplatePrefix(ControlPlaneTestHost host)
    {
        var template = host.Text("AllowedAdmin.Email.WrongDomain", "uk");
        var argument = template.IndexOf("{0}", StringComparison.Ordinal);
        Assert.True(argument >= 0, "AllowedAdmin.Email.WrongDomain takes the installation's domain as {0}.");
        return template[..argument].TrimEnd();
    }
}
