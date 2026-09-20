using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-008 AC-010: every sign-in attempt writes exactly one audit row — success or refusal, never neither —
/// with the time in UTC, the actor, the action, the outcome and the request id, and with no personal datum
/// anywhere (spec FR-012; S-13; SC-11; db-design 4.4).
/// </summary>
public sealed class AdminSignInAuditTests(PostgreSqlFixture database)
{
    private static ScriptedHttpHandler Allowed() =>
        ScriptedHttpHandler.Json(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(true));

    [Fact]
    public async Task ASuccessfulSignIn_WritesOneSucceededRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        await host.SignInWithGoogleAsync(ct);

        var user = Assert.Single(await host.AppUsersAsync(ct));
        var audit = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal(InstallationTestHost.DefaultStart, audit.OccurredAt);
        Assert.Equal("app_user", audit.ActorType);
        Assert.Equal(user.Id, audit.ActorId);
        Assert.Equal("admin", audit.ActorRole);
        Assert.Equal("admin_sign_in", audit.Action);
        Assert.Equal("succeeded", audit.Outcome);
        Assert.Null(audit.RefusalCategory);
        Assert.Null(audit.TargetType);
        Assert.Null(audit.TargetId);
        Assert.False(string.IsNullOrWhiteSpace(audit.RequestId), "The row carries no request id (SC-11).");
    }

    /// <summary>AC-010: one row per attempt, and the account is created together with its row.</summary>
    [Fact]
    public async Task EveryAttempt_WritesExactlyOneRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var answers = new Queue<bool>([true, false, true]);
        var channel = new ScriptedHttpHandler((_, _) => Task.FromResult(
            ScriptedHttpHandler.JsonResponse(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(answers.Dequeue()))));
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        await host.SignInWithGoogleAsync(ct);
        await host.SignInWithGoogleAsync(ct);
        await host.SignInWithGoogleAsync(ct);

        var rows = await host.AuditRowsAsync(ct);
        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { "succeeded", "refused", "succeeded" }, rows.Select(r => r.Outcome));
    }

    /// <summary>AC-010, SC-11: the row and its log line are tied together by the request id.</summary>
    [Fact]
    public async Task TheRequestId_TiesTheRowToItsLogLine()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        await host.SignInWithGoogleAsync(ct);
        var audit = Assert.Single(await host.AuditRowsAsync(ct));

        var files = await host.ReadLogFilesAsync(ct);

        Assert.Contains(audit.RequestId!, string.Join("\n", files), StringComparison.Ordinal);
    }

    /// <summary>AC-010, S-13: no row carries a name, an email or a Google subject identifier.</summary>
    [Fact]
    public async Task NoRow_CarriesAPersonalDatum()
    {
        var ct = TestContext.Current.CancellationToken;
        var answers = new Queue<bool>([true, false]);
        var channel = new ScriptedHttpHandler((_, _) => Task.FromResult(
            ScriptedHttpHandler.JsonResponse(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(answers.Dequeue()))));
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        await host.SignInWithGoogleAsync(ct);
        await host.SignInWithGoogleAsync(ct);

        var rows = await host.AuditRowsAsJsonAsync(ct);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.DoesNotContain(SignInTestData.AdminEmail, row, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ivan", row, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("petrenko", row, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(host.Google.Subject, row, StringComparison.Ordinal);
            Assert.DoesNotContain(host.Google.DisplayName, row, StringComparison.Ordinal);
            Assert.DoesNotContain("school-one", row, StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>AC-010, S-13, PC-9: a row is never updated — the constraint rejects an update through EF Core.</summary>
    [Fact]
    public async Task ARow_CannotBeUpdatedThroughEfCore_ThoughRawSqlIsNotDefendedAgainst()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        await host.SignInWithGoogleAsync(ct);
        var before = Assert.Single(await host.AuditRowsAsync(ct));

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => host.ExecuteAsync(
            "UPDATE audit_event SET updated_at = updated_at + interval '1 second' WHERE id = @id",
            ct,
            ("id", before.Id)));

        Assert.Contains("ck_audit_event_immutable", Text(failure), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, Assert.Single(await host.AuditRowsAsync(ct)));
    }

    /// <summary>AC-010, PC-6: because a row is never updated, its timestamps are equal.</summary>
    [Fact]
    public async Task ARowsTimestamps_AreEqual()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        await host.SignInWithGoogleAsync(ct);

        var audit = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal(audit.CreatedAt, audit.UpdatedAt);
    }

    /// <summary>AC-010: the successful sign-in and its account land together — no account without its row.</summary>
    [Fact]
    public async Task TheAccountAndItsRow_AreWrittenTogether()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        await host.SignInWithGoogleAsync(ct);

        var users = await host.AppUsersAsync(ct);
        var rows = await host.AuditRowsAsync(ct);
        Assert.Single(users);
        Assert.Single(rows);
        Assert.Equal(users[0].Id, rows[0].ActorId);
    }

    /// <summary>AC-010, FR-012: only the one action this Story performs is ever written.</summary>
    [Fact]
    public async Task OnlyTheAdminSignInAction_IsWritten()
    {
        var ct = TestContext.Current.CancellationToken;
        var answers = new Queue<bool>([true, false]);
        var channel = new ScriptedHttpHandler((_, _) => Task.FromResult(
            ScriptedHttpHandler.JsonResponse(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(answers.Dequeue()))));
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        var (client, _) = await host.SignInWithGoogleAsync(ct);
        await client.GetAsync(SignInTestData.LandingPath, ct);
        await host.SignInWithGoogleAsync(ct);

        var rows = await host.AuditRowsAsync(ct);

        Assert.All(rows, r => Assert.Equal("admin_sign_in", r.Action));
        Assert.All(rows, r => Assert.Contains(r.ActorType, new[] { "app_user", "anonymous" }));
    }

    /// <summary>AC-014, I-13: sign-out writes no audit row — the section 5 list is closed.</summary>
    [Fact]
    public async Task SignOut_WritesNoRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        var (client, _) = await host.SignInWithGoogleAsync(ct);
        var before = await host.AuditRowsAsync(ct);

        await client.GetAsync(SignInTestData.LandingPath, ct);
        await client.PostFormAsync(SignInTestData.SignOutPath, [], ct);

        Assert.Equal(before, await host.AuditRowsAsync(ct));
    }

    private static string Text(Exception exception)
    {
        var text = new System.Text.StringBuilder();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            text.AppendLine(current.Message);
        }

        return text.ToString();
    }
}
