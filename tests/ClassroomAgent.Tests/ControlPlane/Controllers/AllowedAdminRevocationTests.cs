using System.Net;
using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-005, AC-006, AC-008: revoking after confirmation; unknown targets answer 404 (FR-004, FR-005, FR-008).</summary>
public sealed class AllowedAdminRevocationTests(PostgreSqlFixture database)
{
    private const string NoteElement = "id=\"revoke-fewer-than-two-note\"";

    [Fact]
    public async Task Confirmation_ShowsEmailInstallationExplanationFormAndCancel_DeletesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var admin = await host.InsertAllowedAdminAsync(installation, AllowedAdminTestData.Email, ct);
        var path = AllowedAdminTestData.RevocationPath(installation, admin);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

        var response = await owner.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Contains(AllowedAdminTestData.Email, response.Text, StringComparison.Ordinal);
        Assert.Contains(InstallationTestData.Name, response.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text("AllowedAdmin.Revoke.Explanation", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text("AllowedAdmin.Revoke.Confirm", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text("AllowedAdmin.Revoke.Cancel", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Matches($"<form[^>]*method=\"post\"[^>]*action=\"(https://localhost)?{Regex.Escape(path)}\"|<form[^>]*action=\"(https://localhost)?{Regex.Escape(path)}\"[^>]*method=\"post\"", response.Body);
        Assert.True(Html.HasInput(response.Body, Html.AntiforgeryFieldName));
        Assert.Matches($"<a[^>]*href=\"(https://localhost)?/installations/{installation:D}\"", response.Body);
        Assert.DoesNotMatch("<script(?![^>]*\\bsrc=)[^>]*>", response.Body);
        Assert.DoesNotMatch("\\son[a-z]+\\s*=", response.Body);
        Assert.Single(await host.AllowedAdminsAsync(ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public async Task Confirmation_NotesWhenRevokingLeavesFewerThanTwo(int entries, bool noted)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var emails = new[] { AllowedAdminTestData.Email, AllowedAdminTestData.OtherEmail, AllowedAdminTestData.ThirdEmail };
        var identifiers = new List<Guid>();
        foreach (var email in emails.Take(entries))
        {
            identifiers.Add(await host.InsertAllowedAdminAsync(installation, email, ct));
        }

        var response = await owner.GetAsync(AllowedAdminTestData.RevocationPath(installation, identifiers[0]), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(noted, response.Body.Contains(NoteElement, StringComparison.Ordinal));
        Assert.Equal(noted, response.Text.Contains(host.Text("AllowedAdmin.Revoke.FewerThanTwoNote", "uk"), StringComparison.Ordinal));
        Assert.Contains(host.Text("AllowedAdmin.Revoke.Confirm", "uk"), response.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Revoke_DeletesOnlyThatEntry_RedirectsToDetail_WhichNoLongerListsIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var revoked = await host.AddAllowedAdminAsync(owner, installation, AllowedAdminTestData.Email, ct);
        var kept = await host.AddAllowedAdminAsync(owner, installation, AllowedAdminTestData.OtherEmail, ct);

        var response = await owner.RevokeAllowedAdminAsync(installation, revoked.Identifier, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(AllowedAdminTestData.DetailPath(installation), response.LocationPath);
        Assert.Equal(new[] { kept }, await host.AllowedAdminsAsync(ct));
        var detail = await owner.GetAsync(AllowedAdminTestData.DetailPath(installation), ct);
        Assert.DoesNotContain(AllowedAdminTestData.Email, detail.Text, StringComparison.Ordinal);
        Assert.Contains(AllowedAdminTestData.OtherEmail, detail.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Revoke_LastEntry_IsAllowed_AndWarningAppears()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var only = await host.AddAllowedAdminAsync(owner, installation, AllowedAdminTestData.Email, ct);

        var response = await owner.RevokeAllowedAdminAsync(installation, only.Identifier, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Empty(await host.AllowedAdminsAsync(ct));
        var detail = await owner.GetAsync(AllowedAdminTestData.DetailPath(installation), ct);
        Assert.Contains(host.Text("AllowedAdmins.Empty", "uk"), detail.Text, StringComparison.Ordinal);
        Assert.Contains("id=\"allowed-admins-warning\"", detail.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Revoke_OnSuspendedInstallation_Succeeds_InstallationUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var admin = await host.AddAllowedAdminAsync(owner, installation, AllowedAdminTestData.Email, ct);
        await host.SetInstallationStatusAsync(installation, "suspended", ct);
        var before = await host.InstallationAsync(installation, ct);

        var response = await owner.RevokeAllowedAdminAsync(installation, admin.Identifier, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Empty(await host.AllowedAdminsAsync(ct));
        Assert.Equal(before, await host.InstallationAsync(installation, ct));
    }

    [Fact]
    public async Task Revoke_ThenAddSameEmail_CreatesNewEntryWithNewIdentifierAndTime()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var original = await host.AddAllowedAdminAsync(owner, installation, AllowedAdminTestData.Email, ct);
        await owner.RevokeAllowedAdminAsync(installation, original.Identifier, ct);
        // Less than the Owner's 30-minute idle timeout (trebovaniya.md §8, v68), so the session stays valid.
        host.Time.Advance(TimeSpan.FromMinutes(10));

        var readded = await host.AddAllowedAdminAsync(owner, installation, AllowedAdminTestData.Email, ct);

        Assert.NotEqual(original.Id, readded.Id);
        Assert.NotEqual(original.Identifier, readded.Identifier);
        Assert.Equal(host.Time.GetUtcNow(), readded.CreatedAt);
        Assert.NotEqual(original.CreatedAt, readded.CreatedAt);
    }

    [Fact]
    public async Task Revoke_SubmittedTwice_SecondReturns404_OneAuditRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var admin = await host.AddAllowedAdminAsync(owner, installation, AllowedAdminTestData.Email, ct);
        var path = AllowedAdminTestData.RevocationPath(installation, admin.Identifier);
        await owner.GetAsync(path, ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

        var first = await owner.PostFormAsync(path, [], ct);
        var second = await owner.PostFormAsync(path, [], ct);

        Assert.Equal(HttpStatusCode.Redirect, first.Status);
        Assert.Equal(HttpStatusCode.NotFound, second.Status);
        Assert.Contains(host.Text("Error.NotFound", "uk"), second.Text, StringComparison.Ordinal);
        Assert.Single((await host.AuditRowsAsync(ct)).Skip(auditRowsBefore));
    }

    [Fact]
    public async Task ConcurrentRevocations_OneRevoked_Other404_OneAuditRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var first = await host.CreateOwnerAsync(ct);
        var (second, _) = await host.SignInAsync(TestData.Login, TestData.Password, ct);
        using (second)
        {
            var installation = await host.InsertInstallationAsync(ct);
            var admin = await host.AddAllowedAdminAsync(first, installation, AllowedAdminTestData.Email, ct);
            var path = AllowedAdminTestData.RevocationPath(installation, admin.Identifier);
            await first.GetAsync(path, ct);
            await second.GetAsync(path, ct);
            var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

            var responses = await Task.WhenAll(first.PostFormAsync(path, [], ct), second.PostFormAsync(path, [], ct));

            Assert.Equal(
                [HttpStatusCode.Redirect, HttpStatusCode.NotFound],
                responses.Select(r => r.Status).Order().ToArray());
            Assert.Empty(await host.AllowedAdminsAsync(ct));
            var row = Assert.Single((await host.AuditRowsAsync(ct)).Skip(auditRowsBefore));
            Assert.Equal("allowed_admin_revoked", row.Action);
        }
    }

    [Fact]
    public async Task UnknownInstallationOrEntry_OrEntryOfAnotherInstallation_Returns404_ChangesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var other = await host.InsertInstallationAsync(
            ct,
            name: InstallationTestData.OtherName,
            domain: InstallationTestData.OtherDomain,
            clientId: InstallationTestData.OtherClientId);
        var admin = await host.InsertAllowedAdminAsync(installation, AllowedAdminTestData.Email, ct);
        var unknown = Guid.Parse(InstallationTestData.UnknownIdentifier);
        var validPage = AllowedAdminTestData.RevocationPath(installation, admin);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

        var paths = new[]
        {
            AllowedAdminTestData.RevocationPath(unknown, admin),
            AllowedAdminTestData.RevocationPath(installation, unknown),
            AllowedAdminTestData.RevocationPath(other, admin),
        };
        var failures = new List<string>();
        foreach (var path in paths)
        {
            var get = await owner.GetAsync(path, ct);
            await owner.GetAsync(validPage, ct);
            var post = await owner.PostFormAsync(path, [], ct);
            foreach (var response in new[] { get, post })
            {
                if (response.Status != HttpStatusCode.NotFound
                    || !response.Text.Contains(host.Text("Error.NotFound", "uk"), StringComparison.Ordinal)
                    || response.Text.Contains(AllowedAdminTestData.Email, StringComparison.Ordinal))
                {
                    failures.Add($"{path} → {(int)response.Status}");
                }
            }
        }

        Assert.Empty(failures);
        Assert.Single(await host.AllowedAdminsAsync(ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Fact]
    public async Task AddForm_UnknownInstallation_GetAndPostReturn404()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var unknown = Guid.Parse(InstallationTestData.UnknownIdentifier);

        var get = await owner.GetAsync(AllowedAdminTestData.AddFormPath(unknown), ct);
        await owner.GetAsync(AllowedAdminTestData.AddFormPath(installation), ct);
        var post = await owner.PostFormAsync(AllowedAdminTestData.AddPath(unknown), AllowedAdminTestData.AddFields("ivan@" + InstallationTestData.Domain), ct);

        Assert.Equal(HttpStatusCode.NotFound, get.Status);
        Assert.Equal(HttpStatusCode.NotFound, post.Status);
        Assert.Contains(host.Text("Error.NotFound", "uk"), post.Text, StringComparison.Ordinal);
        Assert.Empty(await host.AllowedAdminsAsync(ct));
    }

    [Theory]
    [InlineData("/installations/not-a-uuid/admins/new")]
    [InlineData("/installations/not-a-uuid/admins/0f8fad5b-d9cb-469f-a165-70867728950e/revocation")]
    [InlineData("/installations/0f8fad5b-d9cb-469f-a165-70867728950e/admins/not-a-uuid/revocation")]
    [InlineData("/installations/0f8fad5b-d9cb-469f-a165-70867728950e/admins/12345/revocation")]
    public async Task NonUuidRouteValues_Return404ErrorPage(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var response = await owner.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.Contains(host.Text("Error.NotFound", "uk"), response.Text, StringComparison.Ordinal);
    }
}
