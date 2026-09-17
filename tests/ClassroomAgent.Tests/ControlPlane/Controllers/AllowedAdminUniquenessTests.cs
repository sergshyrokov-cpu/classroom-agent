using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-004: an email is listed once per installation, also under concurrency (FR-003 step 6, db-design §5).</summary>
public sealed class AllowedAdminUniquenessTests(PostgreSqlFixture database)
{
    [Theory]
    [InlineData(AllowedAdminTestData.Email)]
    [InlineData("IVAN.Petrenko@School-One.EXAMPLE.test")]
    public async Task Add_EmailAlreadyAnEntry_AnyCase_Returns409Taken_KeepsValue_CreatesNothing(string email)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        await host.AddAllowedAdminAsync(owner, installation, AllowedAdminTestData.Email, ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

        var response = await owner.PostFromPageAsync(
            AllowedAdminTestData.AddFormPath(installation),
            AllowedAdminTestData.AddPath(installation),
            AllowedAdminTestData.AddFields(email),
            ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Null(response.Location);
        Assert.Contains(host.Text("AllowedAdmin.Email.Taken", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Equal(email, Html.InputValue(response.Body, "email"));
        Assert.Single(await host.AllowedAdminsAsync(ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Fact]
    public async Task ConcurrentAdditions_SameEmailDifferentCase_OneCreated_Other409()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var first = await host.CreateOwnerAsync(ct);
        var (second, _) = await host.SignInAsync(TestData.Login, TestData.Password, ct);
        using (second)
        {
            var installation = await host.InsertInstallationAsync(ct);
            await first.GetAsync(AllowedAdminTestData.AddFormPath(installation), ct);
            await second.GetAsync(AllowedAdminTestData.AddFormPath(installation), ct);
            var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

            var responses = await Task.WhenAll(
                first.PostFormAsync(AllowedAdminTestData.AddPath(installation), AllowedAdminTestData.AddFields(AllowedAdminTestData.Email), ct),
                second.PostFormAsync(AllowedAdminTestData.AddPath(installation), AllowedAdminTestData.AddFields(AllowedAdminTestData.Email.ToUpperInvariant()), ct));

            Assert.Equal(
                [HttpStatusCode.Redirect, HttpStatusCode.Conflict],
                responses.Select(r => r.Status).Order().ToArray());
            Assert.Contains(
                host.Text("AllowedAdmin.Email.Taken", "uk"),
                responses.Single(r => r.Status == HttpStatusCode.Conflict).Text,
                StringComparison.Ordinal);
            Assert.Single(await host.AllowedAdminsAsync(ct));
            var row = Assert.Single((await host.AuditRowsAsync(ct)).Skip(auditRowsBefore));
            Assert.Equal("allowed_admin_added", row.Action);
        }
    }

    [Fact]
    public async Task SameNamePart_InTwoInstallations_IsTwoIndependentEntries()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var first = await host.InsertInstallationAsync(ct);
        var second = await host.InsertInstallationAsync(
            ct,
            name: InstallationTestData.OtherName,
            domain: InstallationTestData.OtherDomain,
            clientId: InstallationTestData.OtherClientId);

        await host.AddAllowedAdminAsync(owner, first, "admin@" + InstallationTestData.Domain, ct);
        await host.AddAllowedAdminAsync(owner, second, "admin@" + InstallationTestData.OtherDomain, ct);

        Assert.Single(await host.AllowedAdminsAsync(first, ct));
        Assert.Single(await host.AllowedAdminsAsync(second, ct));
    }
}
