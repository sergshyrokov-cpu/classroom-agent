using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-006: an action that would not change the status writes nothing and shows the current status (FR-002, FR-004, FR-005).</summary>
public sealed class InstallationStatusUnchangedTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task Suspend_AlreadySuspended_RedirectsWithNotice_WritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct, status: "suspended");
        var before = await host.InstallationAsync(installation, ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;
        host.Time.Advance(TimeSpan.FromMinutes(1));

        var response = await owner.SuspendInstallationAsync(installation, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(InstallationStatusTestData.NoticePath(installation, InstallationStatusTestData.AlreadySuspended), response.LocationPath);
        Assert.Equal(before, await host.InstallationAsync(installation, ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Fact]
    public async Task Resume_AlreadyActive_RedirectsWithNotice_WritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var before = await host.InstallationAsync(installation, ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;
        host.Time.Advance(TimeSpan.FromMinutes(1));

        var response = await owner.ResumeInstallationAsync(installation, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(InstallationStatusTestData.NoticePath(installation, InstallationStatusTestData.AlreadyActive), response.LocationPath);
        Assert.Equal(before, await host.InstallationAsync(installation, ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Fact]
    public async Task SameConfirmationSubmittedTwice_SecondIsUnchanged_OneAuditRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var path = InstallationStatusTestData.SuspensionPath(installation);
        await owner.GetAsync(path, ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

        var first = await owner.PostFormAsync(path, [], ct);
        var second = await owner.PostFormAsync(path, [], ct);

        Assert.Equal(HttpStatusCode.Redirect, first.Status);
        Assert.Equal(InstallationStatusTestData.DetailPath(installation), first.LocationPath);
        Assert.Equal(HttpStatusCode.Redirect, second.Status);
        Assert.Equal(InstallationStatusTestData.NoticePath(installation, InstallationStatusTestData.AlreadySuspended), second.LocationPath);
        var row = Assert.Single((await host.AuditRowsAsync(ct)).Skip(auditRowsBefore));
        Assert.Equal("installation_suspended", row.Action);
    }

    [Fact]
    public async Task StaleTab_SuspendedElsewhere_ConfirmingIsUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var first = await host.CreateOwnerAsync(ct);
        var (second, _) = await host.SignInAsync(TestData.Login, TestData.Password, ct);
        using (second)
        {
            var installation = await host.InsertInstallationAsync(ct);
            var path = InstallationStatusTestData.SuspensionPath(installation);
            Assert.Equal(HttpStatusCode.OK, (await first.GetAsync(path, ct)).Status);
            Assert.Equal(HttpStatusCode.OK, (await second.GetAsync(path, ct)).Status);
            await first.PostFormAsync(path, [], ct);
            var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

            var stale = await second.PostFormAsync(path, [], ct);

            Assert.Equal(HttpStatusCode.Redirect, stale.Status);
            Assert.Equal(InstallationStatusTestData.NoticePath(installation, InstallationStatusTestData.AlreadySuspended), stale.LocationPath);
            Assert.Equal("suspended", (await host.InstallationAsync(installation, ct))!.Status);
            Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
        }
    }

    [Fact]
    public async Task StaleResumeAfterResumeElsewhere_DoesNotSuspend()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct, status: "suspended");
        var path = InstallationStatusTestData.ResumptionPath(installation);
        await owner.GetAsync(path, ct);
        await owner.PostFormAsync(path, [], ct);

        var stale = await owner.PostFormAsync(path, [], ct);

        Assert.Equal(HttpStatusCode.Redirect, stale.Status);
        Assert.Equal(InstallationStatusTestData.NoticePath(installation, InstallationStatusTestData.AlreadyActive), stale.LocationPath);
        Assert.Equal("active", (await host.InstallationAsync(installation, ct))!.Status);
    }

    [Theory]
    [InlineData("active", "resumption", InstallationStatusTestData.AlreadyActive)]
    [InlineData("suspended", "suspension", InstallationStatusTestData.AlreadySuspended)]
    public async Task ConfirmationPage_ForTargetStatus_RedirectsWithNotice(string status, string page, string notice)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct, status: status);
        var before = await host.InstallationAsync(installation, ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

        var response = await owner.GetAsync($"/installations/{installation:D}/{page}", ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(InstallationStatusTestData.NoticePath(installation, notice), response.LocationPath);
        Assert.Equal(before, await host.InstallationAsync(installation, ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Theory]
    [InlineData("suspended", InstallationStatusTestData.AlreadySuspended, "Installation.Status.AlreadySuspended")]
    [InlineData("active", InstallationStatusTestData.AlreadyActive, "Installation.Status.AlreadyActive")]
    public async Task Detail_NoticeMatchingCurrentStatus_IsShown(string status, string notice, string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct, status: status);

        var detail = await owner.GetAsync(InstallationStatusTestData.NoticePath(installation, notice), ct);

        Assert.Equal(HttpStatusCode.OK, detail.Status);
        Assert.Contains(InstallationStatusTestData.NoticeElement, detail.Body, StringComparison.Ordinal);
        Assert.Contains(host.Text(key, "uk"), detail.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("active", InstallationStatusTestData.AlreadySuspended)]
    [InlineData("suspended", InstallationStatusTestData.AlreadyActive)]
    [InlineData("active", "something-else")]
    [InlineData("active", "<script>alert(1)</script>")]
    [InlineData("suspended", "ALREADY-SUSPENDED")]
    public async Task Detail_NoticeNotMatchingOrUnknown_IsNotShown_NotAnError(string status, string notice)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct, status: status);

        var detail = await owner.GetAsync(
            $"{InstallationStatusTestData.DetailPath(installation)}?notice={Uri.EscapeDataString(notice)}",
            ct);

        Assert.Equal(HttpStatusCode.OK, detail.Status);
        Assert.DoesNotContain(InstallationStatusTestData.NoticeElement, detail.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text("Installation.Status.AlreadySuspended", "uk"), detail.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text("Installation.Status.AlreadyActive", "uk"), detail.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(notice, detail.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Detail_RepeatedNoticeParameter_IsNotShown()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct, status: "suspended");

        var detail = await owner.GetAsync(
            $"{InstallationStatusTestData.DetailPath(installation)}?notice=already-suspended&notice=already-suspended",
            ct);

        Assert.Equal(HttpStatusCode.OK, detail.Status);
        Assert.DoesNotContain(InstallationStatusTestData.NoticeElement, detail.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConcurrentSuspends_ExactlyOneChangesAndIsAudited_OthersUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        var clients = new List<FormClient> { await host.CreateOwnerAsync(ct) };
        for (var i = 0; i < 4; i++)
        {
            clients.Add((await host.SignInAsync(TestData.Login, TestData.Password, ct)).Client);
        }

        try
        {
            var installation = await host.InsertInstallationAsync(ct);
            var path = InstallationStatusTestData.SuspensionPath(installation);
            foreach (var client in clients)
            {
                await client.GetAsync(path, ct);
            }

            var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

            var responses = await Task.WhenAll(clients.Select(c => c.PostFormAsync(path, [], ct)));

            Assert.All(responses, r => Assert.Equal(HttpStatusCode.Redirect, r.Status));
            Assert.Single(responses, r => r.LocationPath == InstallationStatusTestData.DetailPath(installation));
            Assert.Equal(
                clients.Count - 1,
                responses.Count(r => r.LocationPath == InstallationStatusTestData.NoticePath(installation, InstallationStatusTestData.AlreadySuspended)));
            Assert.Equal("suspended", (await host.InstallationAsync(installation, ct))!.Status);
            var row = Assert.Single((await host.AuditRowsAsync(ct)).Skip(auditRowsBefore));
            Assert.Equal("installation_suspended", row.Action);
        }
        finally
        {
            clients.ForEach(c => c.Dispose());
        }
    }

    [Fact]
    public async Task ConcurrentSuspendAndResume_AuditRowsMatchTheChangesMade()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var suspender = await host.CreateOwnerAsync(ct);
        var (resumer, _) = await host.SignInAsync(TestData.Login, TestData.Password, ct);
        using (resumer)
        {
            for (var round = 0; round < 5; round++)
            {
                var installation = await host.InsertInstallationAsync(
                    ct,
                    name: $"School {round}",
                    domain: $"school-{round}.race.example.test",
                    clientId: $"30000000000{round}");
                await suspender.LoadTokenAsync(ct);
                await resumer.LoadTokenAsync(ct);
                var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

                var responses = await Task.WhenAll(
                    suspender.PostFormAsync(InstallationStatusTestData.SuspensionPath(installation), [], ct),
                    resumer.PostFormAsync(InstallationStatusTestData.ResumptionPath(installation), [], ct));

                Assert.All(responses, r => Assert.Equal(HttpStatusCode.Redirect, r.Status));
                var status = (await host.InstallationAsync(installation, ct))!.Status;
                var actions = (await host.AuditRowsAsync(ct)).Skip(auditRowsBefore).Select(r => r.Action).ToArray();
                if (actions.Length == 2)
                {
                    Assert.Equal(new[] { "installation_suspended", "installation_resumed" }, actions);
                    Assert.Equal("active", status);
                }
                else
                {
                    Assert.Equal(new[] { "installation_suspended" }, actions);
                    Assert.Equal("suspended", status);
                }
            }
        }
    }
}
