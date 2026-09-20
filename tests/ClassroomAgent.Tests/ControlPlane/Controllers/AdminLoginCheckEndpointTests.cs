using System.Net;
using System.Text;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>
/// US-008 AC-005, AC-006: the Control Plane answers whether an email is an approved Admin of <em>that</em>
/// installation and nothing else, refuses an unknown or malformed call without revealing anything, and
/// writes nothing at all (spec FR-009, VR-006, S-16, S-17; api-design 3).
/// </summary>
public sealed class AdminLoginCheckEndpointTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task ApprovedEmail_Returns200_AllowedTrue_AndNothingElse()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        await host.InsertAllowedAdminAsync(installation, SignInTestData.AdminEmail, ct);

        var response = await host.PostAdminLoginCheckAsync(installation, SignInTestData.AdminEmail, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.True(AdminLoginCheckHostExtensions.AllowedOf(response));
    }

    [Fact]
    public async Task EmailNotApproved_Returns200_AllowedFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        await host.InsertAllowedAdminAsync(installation, SignInTestData.AdminEmail, ct);

        var response = await host.PostAdminLoginCheckAsync(installation, SignInTestData.UnapprovedEmail, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.False(AdminLoginCheckHostExtensions.AllowedOf(response));
    }

    /// <summary>AC-005: an entry of another <c>Installation</c> never matches.</summary>
    [Fact]
    public async Task AnotherInstallationsEntry_NeverMatches()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var school = await host.InsertInstallationAsync(ct);
        var otherSchool = await host.InsertInstallationAsync(
            ct,
            name: InstallationTestData.OtherName,
            domain: InstallationTestData.OtherDomain,
            clientId: InstallationTestData.OtherClientId);
        await host.InsertAllowedAdminAsync(otherSchool, AllowedAdminTestData.OtherSchoolEmail, ct);

        var asksForItsOwn = await host.PostAdminLoginCheckAsync(otherSchool, AllowedAdminTestData.OtherSchoolEmail, ct);
        var asksForAnothers = await host.PostAdminLoginCheckAsync(school, AllowedAdminTestData.OtherSchoolEmail, ct);

        Assert.True(AdminLoginCheckHostExtensions.AllowedOf(asksForItsOwn));
        Assert.False(AdminLoginCheckHostExtensions.AllowedOf(asksForAnothers));
    }

    /// <summary>AC-005: the comparison is on the stored lower-cased email, exact match.</summary>
    [Theory]
    [InlineData(SignInTestData.AdminEmail)]
    [InlineData(SignInTestData.AdminEmailMixedCase)]
    [InlineData("  " + SignInTestData.AdminEmail + "  ")]
    public async Task EmailIsComparedLowerCasedAndTrimmed(string email)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        await host.InsertAllowedAdminAsync(installation, SignInTestData.AdminEmail, ct);

        var response = await host.PostAdminLoginCheckAsync(installation, email, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.True(AdminLoginCheckHostExtensions.AllowedOf(response), $"'{email}' should match the stored entry.");
    }

    /// <summary>AC-005: status is the legitimacy check's business, not this endpoint's.</summary>
    [Theory]
    [InlineData("active")]
    [InlineData("suspended")]
    public async Task InstallationStatus_DoesNotChangeTheAnswer(string status)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct, status: status);
        await host.InsertAllowedAdminAsync(installation, SignInTestData.AdminEmail, ct);

        var response = await host.PostAdminLoginCheckAsync(installation, SignInTestData.AdminEmail, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.True(AdminLoginCheckHostExtensions.AllowedOf(response));
    }

    /// <summary>AC-005, S-16: no entry id, no list, no other school's data — one boolean property.</summary>
    [Fact]
    public async Task Answer_CarriesNoIdentifierNoListAndNoOtherSchoolData()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var entry = await host.InsertAllowedAdminAsync(installation, SignInTestData.AdminEmail, ct);
        await host.InsertInstallationAsync(
            ct,
            name: InstallationTestData.OtherName,
            domain: InstallationTestData.OtherDomain,
            clientId: InstallationTestData.OtherClientId);

        var response = await host.PostAdminLoginCheckAsync(installation, SignInTestData.AdminEmail, ct);

        Assert.Equal(new[] { "allowed" }, LegitimacyCheckHostExtensions.JsonProperties(response).Keys);
        Assert.DoesNotContain(entry.ToString("D"), response.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SignInTestData.AdminEmail, response.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(InstallationTestData.OtherDomain, response.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(InstallationTestData.Domain, response.Body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>AC-006: an unknown installation id is refused with the outcome alone.</summary>
    [Fact]
    public async Task UnknownInstallation_Returns404_UnknownInstallationOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        await host.InsertAllowedAdminAsync(installation, SignInTestData.AdminEmail, ct);

        var response = await host.PostAdminLoginCheckAsync(
            Guid.Parse(InstallationTestData.UnknownIdentifier),
            SignInTestData.AdminEmail,
            ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.Equal(
            new Dictionary<string, string> { ["outcome"] = "unknown_installation" },
            LegitimacyCheckHostExtensions.JsonProperties(response));
    }

    /// <summary>AC-006, S-14: the refusal never hints whether the email exists anywhere.</summary>
    [Fact]
    public async Task UnknownInstallation_AnswerIsIdenticalForAnApprovedAndAnUnknownEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        await host.InsertAllowedAdminAsync(installation, SignInTestData.AdminEmail, ct);
        var unknown = Guid.Parse(InstallationTestData.UnknownIdentifier);

        var approved = await host.PostAdminLoginCheckAsync(unknown, SignInTestData.AdminEmail, ct);
        var stranger = await host.PostAdminLoginCheckAsync(unknown, SignInTestData.UnapprovedEmail, ct);

        Assert.Equal(approved.Status, stranger.Status);
        Assert.Equal(approved.Body, stranger.Body);
    }

    public static TheoryData<string, string?, string> InvalidRequests => new()
    {
        { "missing body", null, "application/json" },
        { "empty body", string.Empty, "application/json" },
        { "not JSON content type", """{"installationId":"{id}","email":"a@school-one.example.test"}""", "text/plain" },
        { "form content type", "installationId={id}&email=a@school-one.example.test", "application/x-www-form-urlencoded" },
        { "malformed JSON", """{"installationId":"{id}","email":"a@school-one.example.test" """, "application/json" },
        { "JSON array", """[{"installationId":"{id}","email":"a@school-one.example.test"}]""", "application/json" },
        { "missing installationId", """{"email":"a@school-one.example.test"}""", "application/json" },
        { "installationId not a UUID", """{"installationId":"abc","email":"a@school-one.example.test"}""", "application/json" },
        { "installationId a number", """{"installationId":12345,"email":"a@school-one.example.test"}""", "application/json" },
        { "missing email", """{"installationId":"{id}"}""", "application/json" },
        { "email empty", """{"installationId":"{id}","email":""}""", "application/json" },
        { "email blank", """{"installationId":"{id}","email":"   "}""", "application/json" },
        { "email without at sign", """{"installationId":"{id}","email":"school-one.example.test"}""", "application/json" },
        { "email without domain", """{"installationId":"{id}","email":"admin@"}""", "application/json" },
        { "email with a space", """{"installationId":"{id}","email":"ad min@school-one.example.test"}""", "application/json" },
        { "email a number", """{"installationId":"{id}","email":42}""", "application/json" },
        { "email null", """{"installationId":"{id}","email":null}""", "application/json" },
    };

    /// <summary>AC-006, VR-006: every malformed shape gives the same <c>400</c>, and nothing is recorded.</summary>
    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidRequest_Returns400_InvalidRequestOnly_RecordsNothing(string scenario, string? template, string contentType)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        await host.InsertAllowedAdminAsync(installation, SignInTestData.AdminEmail, ct);
        var auditBefore = await host.AuditRowsAsync(ct);

        var response = await host.PostAdminLoginCheckAsync(
            template?.Replace("{id}", installation.ToString("D"), StringComparison.Ordinal),
            ct,
            contentType);

        Assert.True(response.Status == HttpStatusCode.BadRequest, $"{scenario}: expected 400, got {(int)response.Status}.");
        Assert.Equal(
            new Dictionary<string, string> { ["outcome"] = "invalid_request" },
            LegitimacyCheckHostExtensions.JsonProperties(response));
        Assert.Equal(auditBefore, await host.AuditRowsAsync(ct));
        Assert.Empty(await host.InstanceLicenseChecksAsync(ct));
    }

    /// <summary>VR-006: 254 characters is accepted, 255 is not.</summary>
    [Fact]
    public async Task EmailLength_254IsAccepted_255IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);

        var atLimit = await host.PostAdminLoginCheckAsync(installation, AdminLoginCheckTestData.EmailOfLength(254), ct);
        var pastLimit = await host.PostAdminLoginCheckAsync(installation, AdminLoginCheckTestData.EmailOfLength(255), ct);

        Assert.Equal(HttpStatusCode.OK, atLimit.Status);
        Assert.False(AdminLoginCheckHostExtensions.AllowedOf(atLimit));
        Assert.Equal(HttpStatusCode.BadRequest, pastLimit.Status);
    }

    /// <summary>DC-12: an unknown property is ignored, so a newer installation can still be answered.</summary>
    [Fact]
    public async Task UnknownExtraProperties_AreIgnored()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        await host.InsertAllowedAdminAsync(installation, SignInTestData.AdminEmail, ct);
        var body = $"{{\"installationId\":\"{installation:D}\",\"email\":\"{SignInTestData.AdminEmail}\",\"futureField\":{{\"x\":1}}}}";

        var response = await host.PostAdminLoginCheckAsync(body, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.True(AdminLoginCheckHostExtensions.AllowedOf(response));
    }

    /// <summary>AC-006, SC-4: POST only; nothing else is handled and nothing is recorded.</summary>
    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task OtherMethods_AreNotHandled(string method)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        await host.InsertAllowedAdminAsync(installation, SignInTestData.AdminEmail, ct);
        using var anonymous = host.CreateClient();

        var response = await anonymous.SendAsync(
            new HttpMethod(method),
            AdminLoginCheckTestData.Path,
            method == "GET"
                ? null
                : new StringContent(
                    AdminLoginCheckTestData.RequestJson(installation, SignInTestData.AdminEmail),
                    Encoding.UTF8,
                    "application/json"),
            ct);

        Assert.True(
            response.Status is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"{method}: expected 404 or 405, got {(int)response.Status}.");
        Assert.DoesNotContain("allowed", response.Body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>AC-006, SC-4: no session and no antiforgery token are needed — the service-channel exemption.</summary>
    [Fact]
    public async Task WithoutSessionAndWithoutAntiforgeryToken_IsProcessed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        await host.InsertAllowedAdminAsync(installation, SignInTestData.AdminEmail, ct);
        using var anonymous = host.CreateClient();

        var response = await anonymous.SendAsync(
            HttpMethod.Post,
            AdminLoginCheckTestData.Path,
            new StringContent(
                AdminLoginCheckTestData.RequestJson(installation, SignInTestData.AdminEmail),
                Encoding.UTF8,
                "application/json"),
            ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Empty(response.SetCookies);
        Assert.Null(response.Location);
    }

    /// <summary>US-001 setup gate: before the Owner exists the channel redirects, and the installation reads that as an error answer.</summary>
    [Fact]
    public async Task BeforeOwnerSetup_RedirectsToSetup_RecordsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        var installation = await host.InsertInstallationAsync(ct);

        var response = await host.PostAdminLoginCheckAsync(installation, SignInTestData.AdminEmail, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/setup", response.LocationPath);
        Assert.Empty(await host.AuditRowsAsync(ct));
    }

    /// <summary>AC-005, S-17: a login check writes nothing at all — no audit row, no licence check, no stamp.</summary>
    [Fact]
    public async Task Checks_WriteNothing_NoAuditNoLicenceCheckNoInstallationChange()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        await host.InsertAllowedAdminAsync(installation, SignInTestData.AdminEmail, ct);
        var auditBefore = await host.AuditRowsAsync(ct);
        var rowBefore = await host.InstallationAsync(installation, ct);
        var adminsBefore = await host.AllowedAdminsAsync(installation, ct);
        host.Time.Advance(TimeSpan.FromMinutes(7));

        await host.PostAdminLoginCheckAsync(installation, SignInTestData.AdminEmail, ct);
        await host.PostAdminLoginCheckAsync(installation, SignInTestData.UnapprovedEmail, ct);
        await host.PostAdminLoginCheckAsync(Guid.Parse(InstallationTestData.UnknownIdentifier), SignInTestData.AdminEmail, ct);
        await host.PostAdminLoginCheckAsync("{", ct);

        Assert.Equal(auditBefore, await host.AuditRowsAsync(ct));
        Assert.Empty(await host.InstanceLicenseChecksAsync(ct));
        Assert.Equal(rowBefore, await host.InstallationAsync(installation, ct));
        Assert.Equal(adminsBefore, await host.AllowedAdminsAsync(installation, ct));
    }

    /// <summary>AC-005, AC-018, SC-10: the log carries the installation id and the outcome, never the email.</summary>
    [Fact]
    public async Task Log_CarriesTheInstallationIdAndOutcome_NeverTheEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        await host.InsertAllowedAdminAsync(installation, SignInTestData.AdminEmail, ct);

        await host.PostAdminLoginCheckAsync(installation, SignInTestData.AdminEmail, ct);
        var files = await host.ReadLogFilesAsync(ct);

        var log = string.Join("\n", files);
        Assert.DoesNotContain(SignInTestData.AdminEmail, log, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ivan.petrenko", log, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(installation.ToString("D"), log, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>AC-006, SC-10: a rejected body is never written to the log.</summary>
    [Fact]
    public async Task RejectedBody_IsNeverLogged_AndTheUnknownIdIsLoggedAsWarning()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await host.InsertInstallationAsync(ct);
        const string marker = "canary-value-not-a-real-address";

        await host.PostAdminLoginCheckAsync($$"""{"installationId":"nonsense","email":"{{marker}}"}""", ct);
        await host.PostAdminLoginCheckAsync(Guid.Parse(InstallationTestData.UnknownIdentifier), SignInTestData.AdminEmail, ct);
        var events = await host.ReadLogEventsAsync(ct);

        var log = string.Join("\n", events.Select(e => e.Line));
        Assert.DoesNotContain(marker, log, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SignInTestData.AdminEmail, log, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            events,
            e => e.Level == "Warning" && e.Line.Contains(InstallationTestData.UnknownIdentifier, StringComparison.OrdinalIgnoreCase));
    }
}
