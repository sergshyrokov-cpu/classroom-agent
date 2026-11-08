using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.BackgroundServices;

/// <summary>
/// US-077 AC-001, AC-006, spec NFR, S-01: a whole synchronization through the real host, background service and
/// PostgreSQL (TC-2) with the <b>real</b> Classroom adapter over a scripted transport (TC-4). Many per-course reads
/// cost one token per scope set, and the token reaches neither the log nor the database.
/// </summary>
public sealed class TokenReuseSyncTests(PostgreSqlFixture database)
{
    private const string Token = "ya29.synthetic-access-token";
    private const string TokenHost = "oauth2.googleapis.com";

    private static GoogleClassroomScript ThreeCourses()
    {
        var script = new GoogleClassroomScript();
        foreach (var n in new[] { 1, 2, 3 })
        {
            var course = CourseTestData.CourseId(n);
            script.Course(course, CourseTestData.CourseName(n))
                .Student(course, CourseTestData.UserId(n), CourseTestData.Name(n), CourseTestData.Email($"person{n}"))
                .Item(course, CourseWorkTestData.ItemId(n), CourseWorkTestData.Title(n), 10)
                .Submission(course, CourseWorkTestData.ItemId(n), CourseWorkTestData.SubmissionId(n), CourseTestData.UserId(n), 7);
        }

        return script;
    }

    private static Task<InstallationTestHost> StartAsync(PostgreSqlFixture database, GoogleClassroomScript script, CancellationToken ct) =>
        SyncHostExtensions.StartAsync(database, ct, seed: (host, _) =>
        {
            script.InstallIn(host);
            return Task.CompletedTask;
        });

    /// <summary>AC-001, NFR: a run over three courses asks Google for one token per scope set, however many reads it makes.</summary>
    [Fact]
    public async Task AWholeRun_RequestsOneTokenPerScopeSet()
    {
        var ct = TestContext.Current.CancellationToken;
        var script = ThreeCourses();
        await using var host = await StartAsync(database, script, ct);

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        Assert.Equal(2, script.Transport.Requests.Count(r => r.Uri?.Host == TokenHost));
        Assert.True(script.Transport.Requests.Count(r => r.Uri?.Host != TokenHost) > 10, "The run made too few reads to prove reuse.");
    }

    /// <summary>
    /// AC-006, S-01: after a run the token is in no log line and in no text column of any table. A guard rather than a red
    /// test: the token passes through this run today too, and must keep staying out.
    /// </summary>
    [Fact]
    public async Task AfterARun_TheTokenIsInNoLogLine_AndInNoStoredValue()
    {
        var ct = TestContext.Current.CancellationToken;
        var script = ThreeCourses();
        await using var host = await StartAsync(database, script, ct);
        var run = await host.WaitForFinishedRunAsync(ct);
        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunCompleted, ct);

        var log = string.Join('\n', await host.ReadLogFilesAsync(ct));
        Assert.DoesNotContain(Token, log, StringComparison.Ordinal);

        var tables = await host.QueryAsync(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'",
            r => r.GetString(0),
            ct);
        Assert.NotEmpty(tables);
        foreach (var table in tables)
        {
            Assert.Equal(0, await host.ScalarAsync<long>(
                $"SELECT count(*) FROM \"{table}\" t WHERE t::text LIKE @token",
                ct,
                ("token", "%" + Token + "%")));
        }
    }
}
