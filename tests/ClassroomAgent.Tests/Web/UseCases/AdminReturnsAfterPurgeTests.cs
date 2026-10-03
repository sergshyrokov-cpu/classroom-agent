using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.RetentionPurgeTestData;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-037 AC-006, spec FR-007, OD-005: an Admin account deleted by the purge — the last Admin included — comes back as
/// a new account at the next Google sign-in while the Control Plane still allows the email; old audit rows keep the
/// old id.
/// </summary>
public sealed class AdminReturnsAfterPurgeTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task ADeletedAdmin_SignsInAgain_AndGetsANewAccount()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = ScriptedHttpHandler.AdminLoginCheckJson(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(true));
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(
            database,
            ct,
            controlPlaneHandler: channel);
        var oldId = await host.InsertAccountAsync(SignInTestData.AdminEmail, "admin", Old.AddYears(-1), Old, ct);
        var oldRow = await host.InsertAuditRowAsync(Recent, ct, actorId: oldId);

        await host.RunPurgeAsync(ct);
        Assert.Empty(await host.AppUsersAsync(ct));

        await host.SignInWithGoogleAsync(ct);

        var account = Assert.Single(await host.AppUsersAsync(ct));
        Assert.Equal(SignInTestData.AdminEmail, account.Email);
        Assert.Equal("admin", account.Role);
        Assert.NotEqual(oldId, account.Id);
        Assert.Equal(Now, account.LastSuccessfulSignInAt);
        Assert.Equal(oldId, Assert.Single(await host.AuditRowsAsync(ct), r => r.Id == oldRow).ActorId);
    }
}
