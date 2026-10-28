using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureHostExtensions;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureTestData;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-065 AC-011, SC-10, S-06: the entered email, valid or not, found loaded or stored, never reaches a log line, and
/// neither does the fingerprint of a stored exclusion.
/// </summary>
public sealed class ErasureExclusionEntryLoggingTests(PostgreSqlFixture database)
{
    /// <summary>SC-10, S-06: the entered email is never logged, whichever way the entry ends.</summary>
    [Theory]
    [InlineData("entered")]
    [InlineData("invalid")]
    [InlineData("loaded")]
    public async Task TheEnteredEmail_IsNeverLogged(string outcome)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        if (outcome == "loaded")
        {
            await host.SeedErasureWorldAsync(ct);
        }

        var page = outcome == "invalid"
            ? await client.PostEntryAsync("o.koval@@school-one.example.test", ct)
            : await client.PostEntryAsync(TargetEmail, ct);
        Assert.Equal(
            outcome switch
            {
                "entered" => HttpStatusCode.Redirect,
                "invalid" => HttpStatusCode.BadRequest,
                _ => HttpStatusCode.OK,
            },
            page.Status);
        var files = await host.ReadLogFilesWhileRunningAsync(ct);

        Assert.DoesNotContain("o.koval", string.Join("\n", files), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>SC-10, S-06: after an entry no log line carries the stored fingerprint.</summary>
    [Fact]
    public async Task AfterAnEntry_NoLogLine_CarriesTheFingerprint()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var entry = await client.PostEntryAsync(TargetEmail, ct);
        Assert.Equal(HttpStatusCode.Redirect, entry.Status);
        var row = Assert.Single(await host.ExclusionRowsAsync(ct));
        var files = await host.ReadLogFilesWhileRunningAsync(ct);

        Assert.DoesNotContain(
            Convert.ToHexString(row.EmailFingerprint!), string.Join("\n", files), StringComparison.OrdinalIgnoreCase);
    }
}
