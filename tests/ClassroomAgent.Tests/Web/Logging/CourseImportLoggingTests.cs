using ClassroomAgent.Application.Models;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-014 AC-008, spec FR-016, S-06: the lines an <b>import</b> writes carry counters, states and internal or
/// Google identifiers only — never a course name, a person's name or an address (SC-10, DC-10). Proved in a real
/// host, where those lines are actually written, over real PostgreSQL (TC-2); the Classroom port is substituted, so
/// nothing reaches Google (TC-4).
/// </summary>
/// <remarks>
/// This class closes the gap TEST_WRITING recorded rather than hid: SC-10 compliance for the new lines was
/// asserted only through the stored <c>SyncState</c> diagnosis, because the import step did not exist yet
/// (test-generation report §9). US-013's <c>SyncLoggingTests</c> is the precedent it extends.
/// </remarks>
public sealed class CourseImportLoggingTests(PostgreSqlFixture database)
{
    private const string SkippedEvent = "SyncCourseSkipped";

    private static RosterEntry Person(int ordinal) =>
        new(CourseTestData.UserId(ordinal), CourseTestData.Email($"person{ordinal}"), CourseTestData.Name(ordinal));

    /// <summary>
    /// AC-008, S-06: a run that imported a course and its roster wrote no course name, no person's name and no
    /// address into any line — not even the person's Google id, which SC-10 also keeps out.
    /// </summary>
    [Fact]
    public async Task TheLogOfAnImport_CarriesNoCourseNameAndNoPersonalData()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: reader => reader.WithCourse(
                CourseTestData.CourseId(1),
                CourseTestData.CourseName(1),
                teachers: [Person(1)],
                students: [Person(2)],
                description: "A description no log line may carry."));
        await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunCompleted, ct);

        var all = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));

        Assert.DoesNotContain(CourseTestData.CourseName(1), all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("A description no log line may carry.", all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(CourseTestData.Name(1), all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(CourseTestData.Name(2), all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(CourseTestData.Email("person1"), all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(CourseTestData.UserId(1), all, StringComparison.Ordinal);

        // The control: the run really did import that course, so the absences above are not the absence of a run.
        var imported = await host.ScalarAsync<long>("SELECT count(*) FROM course_membership", ct);
        Assert.Equal(2, imported);
    }

    /// <summary>
    /// AC-001, FR-013, FR-016: the finish line carries the counter of <b>courses</b> processed and the number of
    /// memberships this run marked off a roster — counters and states, nothing else.
    /// </summary>
    [Fact]
    public async Task TheFinishLine_CarriesTheCourseCounterAndTheOffRosterCount()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: reader => reader
                .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)])
                .WithCourse(CourseTestData.CourseId(2), CourseTestData.CourseName(2)));
        var events = await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunCompleted, ct);

        var line = Assert.Single(events, e => e.EventName == SyncTestData.LogEvents.RunCompleted);
        Assert.Equal("Information", line.Level);
        Assert.Equal("2", line.Property("ProcessedCount"));
        Assert.Equal("0", line.Property("MarkedOffRoster"));
    }

    /// <summary>
    /// AC-004, FR-010, FR-016: a person the roster no longer lists is marked off it — the membership survives with
    /// its last-seen date — and the finish line counts the departure without naming anyone.
    /// </summary>
    [Fact]
    public async Task ADepartureFromARoster_IsCountedAndTheMembershipSurvives()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            // The course is known and the person is on its roster before the run; Classroom then returns the course
            // with an empty roster, which is an answer and not a failure (I-7).
            seed: async (h, token) =>
            {
                var course = await CourseRows.InsertCourseAsync(h, token, googleId: CourseTestData.CourseId(1));
                var participant = await CourseRows.InsertParticipantAsync(
                    h,
                    token,
                    googleUserId: CourseTestData.UserId(1),
                    email: CourseTestData.Email("person1"),
                    fullName: CourseTestData.Name(1));
                await CourseRows.InsertMembershipAsync(h, course, participant, token);
            },
            classroom: reader => reader.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1)));
        var events = await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunCompleted, ct);

        var line = Assert.Single(events, e => e.EventName == SyncTestData.LogEvents.RunCompleted);
        Assert.Equal("1", line.Property("MarkedOffRoster"));

        var rows = await host.QueryAsync(
            "SELECT on_roster, first_seen_at = last_seen_at FROM course_membership",
            r => (OnRoster: r.GetBoolean(0), InstantsEqual: r.GetBoolean(1)),
            ct);
        var membership = Assert.Single(rows);
        Assert.False(membership.OnRoster);

        // Never deleted and its last sighting never moved: the leaver's own expiry counts from it (PC-11, BR-051).
        Assert.True(membership.InstantsEqual);
    }

    /// <summary>
    /// AC-001, OD-010: a course whose state Classroom reports outside the five §3 values is logged at
    /// <b>Warning</b> — not Information — with the state string and the course's Google id, and never its name.
    /// </summary>
    [Fact]
    public async Task ASkippedCourse_IsLoggedAtWarningWithTheStateAndTheGoogleId()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: reader => reader
                .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1))
                .WithCourse(CourseTestData.CourseId(2), CourseTestData.CourseName(2), CourseTestData.UnrecognisedState));
        await host.WaitForLogEventAsync(SkippedEvent, ct);
        var events = await HostLogs.ReadEventsAsync(host.LogDirectory, ct);

        var line = Assert.Single(events, e => e.EventName == SkippedEvent);
        Assert.Equal("Warning", line.Level);
        Assert.Equal(CourseTestData.CourseId(2), line.Property("CourseGoogleId"));
        Assert.Equal(CourseTestData.UnrecognisedState, line.Property("CourseState"));

        var all = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));
        Assert.DoesNotContain(CourseTestData.CourseName(2), all, StringComparison.OrdinalIgnoreCase);

        // The run completed with the other course: the skip cost the school one course, not the whole import.
        var stored = await host.QueryAsync("SELECT google_id FROM course", r => r.GetString(0), ct);
        Assert.Equal([CourseTestData.CourseId(1)], stored);
    }
}
