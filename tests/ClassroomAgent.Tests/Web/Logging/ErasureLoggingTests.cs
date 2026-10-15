using System.Globalization;
using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureHostExtensions;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureTestData;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-038 AC-009, spec FR-013, SC-10, S-06: an erasure writes one Information line with the internal id and the counts; no
/// log line carries the person's data, the search term or the email to lift; a refused erasure writes no erasure line.
/// </summary>
public sealed class ErasureLoggingTests(PostgreSqlFixture database)
{
    private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>AC-009, FR-013: one Information line after the commit, with the internal id and the five counts.</summary>
    [Fact]
    public async Task AnErasure_WritesOneInformationLine_WithTheInternalIdAndTheCounts()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedErasureWorldAsync(ct);

        var erasure = await client.PostErasureAsync(world.Target, "true", ct);
        Assert.Equal(HttpStatusCode.Redirect, erasure.Status);
        var events = await host.WaitForLogEventAsync(Log.Erased, ct);

        var line = Assert.Single(events, e => e.EventName == Log.Erased);
        Assert.Equal("Information", line.Level);
        Assert.Equal(world.Target.ToString(CultureInfo.InvariantCulture), line.Property(Log.ParticipantId));
        Assert.Equal(Count(Expected.ErasedMemberships), line.Property(Log.Memberships));
        Assert.Equal(Count(Expected.ErasedSubmissions), line.Property(Log.Submissions));
        Assert.Equal(Count(Expected.ErasedMeetParticipations), line.Property(Log.MeetParticipations));
        Assert.Equal(Count(Expected.ClearedMeetOrganizers), line.Property(Log.ClearedMeetOrganizers));
        Assert.Equal(Count(Expected.ClearedCourseOwners), line.Property(Log.ClearedCourseOwners));
    }

    /// <summary>AC-009, SC-10, S-06: after an erasure no log line carries the person's name, email, Google id or fingerprint.</summary>
    [Fact]
    public async Task NoLogLine_CarriesThePersonsData_AfterAnErasure()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedErasureWorldAsync(ct);

        var erasure = await client.PostErasureAsync(world.Target, "true", ct);
        Assert.Equal(HttpStatusCode.Redirect, erasure.Status);
        await host.WaitForLogEventAsync(Log.Erased, ct);
        var files = await host.ReadLogFilesWhileRunningAsync(ct);

        var text = string.Join("\n", files);
        foreach (var value in TargetPersonalValues)
        {
            Assert.DoesNotContain(value, text, StringComparison.OrdinalIgnoreCase);
        }

        var fingerprint = Convert.ToHexString(Assert.Single(await host.ExclusionRowsAsync(ct)).EmailFingerprint!);
        Assert.DoesNotContain(fingerprint, text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>SC-10, S-06: the search term is never logged.</summary>
    [Fact]
    public async Task TheSearchTerm_IsNeverLogged()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedErasureWorldAsync(ct);

        var search = await client.PostSearchAsync("Коваль", ct);
        Assert.Equal(HttpStatusCode.OK, search.Status);
        var files = await host.ReadLogFilesWhileRunningAsync(ct);

        Assert.DoesNotContain("Коваль", string.Join("\n", files), StringComparison.Ordinal);
    }

    /// <summary>SC-10, S-06: the email to lift is never logged.</summary>
    [Fact]
    public async Task TheEmailToLift_IsNeverLogged()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedExclusionAsync(TargetGoogleUserId, TargetEmail, ct);

        var lifting = await client.PostLiftingAsync(TargetEmail.ToUpperInvariant(), ct);
        Assert.Equal(HttpStatusCode.Redirect, lifting.Status);
        var files = await host.ReadLogFilesWhileRunningAsync(ct);

        Assert.DoesNotContain("o.koval", string.Join("\n", files), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>FR-013: a refused erasure writes no erasure line.</summary>
    [Fact]
    public async Task ARefusedErasure_WritesNoErasureLine()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedErasureWorldAsync(ct);

        var erasure = await client.PostErasureAsync(world.Target, null, ct);
        Assert.Equal(HttpStatusCode.BadRequest, erasure.Status);
        var files = await host.ReadLogFilesWhileRunningAsync(ct);

        Assert.DoesNotContain(LogEvent.Parse(files), e => e.EventName == Log.Erased);
    }
}
