using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.RetentionPurgeTestData;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-037 spec FR-012, AC-010, SC-10: the purge logs its start and its completion with the counts, one <c>Error</c>
/// per failed unit naming the course by internal id and the exception by type, and no personal data or Google id.
/// </summary>
public sealed class RetentionPurgeLoggingTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task ARun_LogsItsStartAndItsCompletionWithTheCounts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithPurgeServiceAsync(
            database,
            ct,
            async (h, token) =>
            {
                await CourseRows.InsertCourseAsync(h, token, updateTime: Old);
                await h.InsertAuditRowAsync(Old, token);
            });

        var events = await host.WaitForLogEventAsync(Events.Completed, ct);

        var started = Assert.Single(events, e => e.EventName == Events.Started);
        Assert.Equal("Information", started.Level);
        var completed = Assert.Single(events, e => e.EventName == Events.Completed);
        Assert.Equal("Information", completed.Level);
        Assert.Contains("1", completed.Line, StringComparison.Ordinal);
    }

    /// <summary>AC-010: one <c>Error</c> naming the course by internal id; no Google id, name or exception message.</summary>
    [Fact]
    public async Task AFailedCourse_IsLoggedOnceAsAnErrorWithItsInternalId()
    {
        var ct = TestContext.Current.CancellationToken;
        long courseId = 0;
        await using var host = await RetentionPurgeHost.StartWithPurgeServiceAsync(
            database,
            ct,
            async (h, token) =>
            {
                courseId = await CourseRows.InsertCourseAsync(
                    h,
                    token,
                    googleId: CourseTestData.CourseId(1),
                    name: CourseTestData.CourseName(1),
                    updateTime: Old);
                await h.FailDeletesAsync("course", token, courseId);
            });

        var events = await host.WaitForLogEventAsync(Events.Completed, ct);

        var failure = Assert.Single(events, e => e.EventName == Events.UnitFailed);
        Assert.Equal("Error", failure.Level);
        Assert.Contains(courseId.ToString(System.Globalization.CultureInfo.InvariantCulture), failure.Line, StringComparison.Ordinal);
        Assert.DoesNotContain(CourseTestData.CourseId(1), failure.Line, StringComparison.Ordinal);
        Assert.DoesNotContain(CourseTestData.CourseName(1), failure.Line, StringComparison.Ordinal);
        Assert.DoesNotContain("injected purge test failure", failure.Line, StringComparison.Ordinal);
    }

    /// <summary>SC-10: nothing the purge removed — names, emails, Google ids — appears in any log line.</summary>
    [Fact]
    public async Task NoLogLine_CarriesWhatWasPurged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithPurgeServiceAsync(
            database,
            ct,
            async (h, token) =>
            {
                var course = await CourseRows.InsertCourseAsync(
                    h,
                    token,
                    googleId: CourseTestData.CourseId(1),
                    name: CourseTestData.CourseName(1),
                    updateTime: Old);
                var person = await CourseRows.InsertParticipantAsync(
                    h,
                    token,
                    googleUserId: CourseTestData.UserId(1),
                    email: CourseTestData.Email("purged.student"),
                    fullName: CourseTestData.Name(1));
                await CourseRows.InsertMembershipAsync(h, course, person, token, firstSeenAt: Old, lastSeenAt: Old);
                await h.InsertAccountAsync("purged.dean@school.test", "dean", Old, Old, token);
            });

        await host.WaitForLogEventAsync(Events.Completed, ct);
        var text = string.Join('\n', await host.ReadLogFilesWhileRunningAsync(ct));

        Assert.DoesNotContain(CourseTestData.CourseId(1), text, StringComparison.Ordinal);
        Assert.DoesNotContain(CourseTestData.CourseName(1), text, StringComparison.Ordinal);
        Assert.DoesNotContain(CourseTestData.UserId(1), text, StringComparison.Ordinal);
        Assert.DoesNotContain("purged.student", text, StringComparison.Ordinal);
        Assert.DoesNotContain(CourseTestData.Name(1), text, StringComparison.Ordinal);
        Assert.DoesNotContain("purged.dean", text, StringComparison.Ordinal);
    }
}
