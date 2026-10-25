using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.BackgroundServices;

/// <summary>
/// US-055 AC-001 … AC-004 (spec FR-001, FR-002, FR-004, S-04): a synchronization run through the real host, its
/// background service and PostgreSQL (TC-2) stores maximum points and grades of any size exactly as the substituted
/// Classroom port delivers them (TC-4), and goes on to the following courses and the Meet step. Every value is
/// synthetic (S-03).
/// </summary>
public sealed class LargeGradesSyncTests(PostgreSqlFixture database)
{
    private static RosterEntry Student(int ordinal) =>
        new(CourseTestData.UserId(ordinal), CourseTestData.Email($"student{ordinal}"), CourseTestData.Name(ordinal));

    /// <summary>One course, one student, one graded task and the student's submission with the given values.</summary>
    private static Action<FakeClassroomReader> OneGradedTask(decimal? maxPoints, decimal? assigned, decimal? draft) =>
        reader => reader
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Student(1)])
            .WithCourseWork(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseWorkResource.CourseWork,
                CourseWorkTestData.Title(1),
                InstallationTestHost.DefaultStart,
                maxPoints: maxPoints)
            .WithSubmission(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseTestData.UserId(1),
                CourseWorkTestData.SubmissionId(1),
                CourseWorkTestData.RawStates.TurnedIn,
                assignedGrade: assigned,
                draftGrade: draft);

    private static Task<decimal?> StoredMaxPointsAsync(InstallationTestHost host, string itemGoogleId, CancellationToken ct) =>
        host.ScalarAsync<decimal?>("SELECT max_points FROM course_work WHERE google_id = @id", ct, ("id", itemGoogleId));

    private static async Task<(decimal? Assigned, decimal? Draft)> StoredGradesAsync(InstallationTestHost host, CancellationToken ct)
    {
        var rows = await host.QueryAsync(
            "SELECT assigned_grade, draft_grade FROM submission WHERE google_id = @id",
            r => (r.IsDBNull(0) ? (decimal?)null : r.GetDecimal(0), r.IsDBNull(1) ? (decimal?)null : r.GetDecimal(1)),
            ct,
            ("id", CourseWorkTestData.SubmissionId(1)));
        return Assert.Single(rows);
    }

    /// <summary>The value as PostgreSQL prints it — "not rounded to four decimal places" is a statement about digits (VR-002).</summary>
    private static Task<string?> StoredTextAsync(InstallationTestHost host, string sql, CancellationToken ct) =>
        host.ScalarAsync<string>(sql, ct);

    /// <summary>AC-001, FR-001, FR-002, FR-004: maximum points of 12 345 678.5 — the run completes and stores exactly that.</summary>
    [Fact]
    public async Task LargeMaximumPoints_AreStoredAsReturned_AndTheRunCompletes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database, ct, classroom: OneGradedTask(12_345_678.5m, 3m, null));

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        Assert.Null(run.LastError);
        Assert.Equal(12_345_678.5m, await StoredMaxPointsAsync(host, CourseWorkTestData.ItemId(1), ct));
    }

    /// <summary>AC-002, FR-001, FR-002, FR-004: an assigned grade of 2 500 000 and a draft of 1 000 000.25, both stored exactly.</summary>
    [Fact]
    public async Task LargeGrades_AreStoredAsReturned_AndTheRunCompletes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database, ct, classroom: OneGradedTask(12_345_678.5m, 2_500_000m, 1_000_000.25m));

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        Assert.Equal((2_500_000m, 1_000_000.25m), await StoredGradesAsync(host, ct));
    }

    /// <summary>AC-003, FR-001, FR-002, VR-002: 10.123456 and 7.654321 are stored with all six decimal places.</summary>
    [Fact]
    public async Task SixDecimalPlaces_AreNotRounded()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database, ct, classroom: OneGradedTask(10.123456m, 7.654321m, 7.654321m));

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        Assert.Equal(10.123456m, await StoredMaxPointsAsync(host, CourseWorkTestData.ItemId(1), ct));
        Assert.Equal((7.654321m, 7.654321m), await StoredGradesAsync(host, ct));
        Assert.Equal("10.123456", await StoredTextAsync(host, "SELECT max_points::text FROM course_work", ct));
        Assert.Equal("7.654321", await StoredTextAsync(host, "SELECT assigned_grade::text FROM submission", ct));
    }

    /// <summary>
    /// AC-004, FR-004, spec I-6: three courses, the second with maximum points above 999 999.9999 — all three courses and
    /// their items are stored, the substituted Meet port is called and the run ends <c>completed</c>.
    /// </summary>
    [Fact]
    public async Task TheCoursesAfterTheUnusualOne_StillLoad_AndTheMeetStepRuns()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: reader =>
            {
                for (var i = 1; i <= 3; i++)
                {
                    reader
                        .WithCourse(CourseTestData.CourseId(i), CourseTestData.CourseName(i), students: [Student(i)])
                        .WithCourseWork(
                            CourseTestData.CourseId(i),
                            CourseWorkTestData.ItemId(i),
                            CourseWorkResource.CourseWork,
                            CourseWorkTestData.Title(i),
                            InstallationTestHost.DefaultStart,
                            maxPoints: i == 2 ? 1_000_000m : 10m);
                }
            });

        var run = await host.WaitForFinishedRunAsync(ct);

        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        Assert.Equal(3, run.ProcessedCount);
        for (var i = 1; i <= 3; i++)
        {
            Assert.Equal(1, await host.CountAsync("course", ct, "google_id = @id", ("id", CourseTestData.CourseId(i))));
            Assert.Equal(1, await host.CountAsync("course_work", ct, "google_id = @id", ("id", CourseWorkTestData.ItemId(i))));
        }

        Assert.Equal(1_000_000m, await StoredMaxPointsAsync(host, CourseWorkTestData.ItemId(2), ct));
        Assert.NotEmpty(host.Meet.Calls);
        Assert.NotNull(await host.ScalarAsync<object>("SELECT meet_loaded_up_to FROM sync_state", ct));
    }

    /// <summary>
    /// S-04, SC-10: the completed run's log carries none of the large values it stored. The completion assertion comes
    /// first — "no value in the log" is free when the run stopped before storing anything.
    /// </summary>
    [Fact]
    public async Task ACompletedRunWithLargeValues_LogsNoneOfThem()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database, ct, classroom: OneGradedTask(12_345_678.5m, 2_500_000m, 1_000_000.25m));

        var run = await host.WaitForFinishedRunAsync(ct);
        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunCompleted, ct);

        var all = string.Join('\n', await host.ReadLogFilesAsync(ct));
        foreach (var value in new[] { 12_345_678.5m, 2_500_000m, 1_000_000.25m })
        {
            Assert.DoesNotContain(value.ToString(CultureInfo.InvariantCulture), all, StringComparison.Ordinal);
            Assert.DoesNotContain(value.ToString(CultureInfo.GetCultureInfo("uk-UA")), all, StringComparison.Ordinal);
        }
    }
}
