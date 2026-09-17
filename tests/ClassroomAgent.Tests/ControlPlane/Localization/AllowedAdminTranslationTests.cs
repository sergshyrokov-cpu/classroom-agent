using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Localization;

/// <summary>AC-012, TC-8: AllowedAdmin pages are translated, Ukrainian by default; emails are never translated.</summary>
public sealed class AllowedAdminTranslationTests(PostgreSqlFixture database)
{
    /// <summary>The keys fixed by the US-003 contract (openapi ValidationMessageKeys, PageTextKeys).</summary>
    private static readonly string[] Keys =
    [
        "AllowedAdmin.Email.Required",
        "AllowedAdmin.Email.Length",
        "AllowedAdmin.Email.Format",
        "AllowedAdmin.Email.NameLength",
        "AllowedAdmin.Email.NameCharacters",
        "AllowedAdmin.Email.NameDots",
        "AllowedAdmin.Email.WrongDomain",
        "AllowedAdmin.Email.Taken",
        "AllowedAdmins.Title",
        "AllowedAdmins.Empty",
        "AllowedAdmins.FewerThanTwoWarning",
        "AllowedAdmins.Add",
        "AllowedAdmin.Revoke",
        "AllowedAdmin.Email.DomainHint",
        "AllowedAdmin.Revoke.Explanation",
        "AllowedAdmin.Revoke.FewerThanTwoNote",
        "AllowedAdmin.Revoke.Confirm",
        "AllowedAdmin.Revoke.Cancel",
    ];

    public static TheoryData<string> ContractKeys => new(Keys);

    [Theory]
    [MemberData(nameof(ContractKeys))]
    public async Task ContractKey_ExistsInUkrainianAndEnglish_AndDiffers(string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var ukrainian = host.Text(key, "uk");
        var english = host.Text(key, "en");

        Assert.NotEqual(ukrainian, english);
    }

    [Theory]
    [InlineData("AllowedAdmin.Email.WrongDomain")]
    [InlineData("AllowedAdmin.Email.DomainHint")]
    public async Task DomainArgumentKeys_TakeTheDomainInBothLanguages(string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        Assert.Contains("{0}", host.Text(key, "uk"), StringComparison.Ordinal);
        Assert.Contains("{0}", host.Text(key, "en"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AllowedAdminPages_AreUkrainianByDefault_EmailsUntranslated()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var english = new Dictionary<string, string> { ["Accept-Language"] = "en" };
        var installation = await host.InsertInstallationAsync(ct);

        var emptyDetail = await owner.GetAsync(AllowedAdminTestData.DetailPath(installation), ct, english);
        var form = await owner.GetAsync(AllowedAdminTestData.AddFormPath(installation), ct, english);
        var admin = await host.InsertAllowedAdminAsync(installation, AllowedAdminTestData.Email, ct);
        var confirmation = await owner.GetAsync(AllowedAdminTestData.RevocationPath(installation, admin), ct, english);
        var detail = await owner.GetAsync(AllowedAdminTestData.DetailPath(installation), ct);

        Assert.All(new[] { emptyDetail, form, confirmation, detail }, page =>
        {
            Assert.Equal(HttpStatusCode.OK, page.Status);
            Assert.Contains("<html lang=\"uk\"", page.Body, StringComparison.Ordinal);
        });
        Assert.Contains(host.Text("AllowedAdmins.Empty", "uk"), emptyDetail.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text("AllowedAdmins.Empty", "en"), emptyDetail.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text("AllowedAdmin.Revoke.Explanation", "uk"), confirmation.Text, StringComparison.Ordinal);
        Assert.Contains(AllowedAdminTestData.Email, detail.Text, StringComparison.Ordinal);
        Assert.Contains(AllowedAdminTestData.Email, confirmation.Text, StringComparison.Ordinal);
    }
}
