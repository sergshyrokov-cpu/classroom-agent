using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Drives US-056 through real synchronization runs (TC-2, Google ports substituted — TC-4): the installation's first run
/// on <see cref="FirstRun"/>, and later runs on later days over the same database. A later run is a restarted host whose
/// clock starts on its day, because the start of a membership is compared by calendar date and a scheduled run minutes
/// later would land on the first run's day (spec §5 preamble: every run happens at a later instant).
/// </summary>
public static class MembershipStartHost
{
    /// <summary>Google <c>creationTime</c> of the course of AC-001 … AC-007: 2025-09-01, 09:00 in Kyiv.</summary>
    public static readonly DateTimeOffset CourseCreated = new(2025, 9, 1, 6, 0, 0, TimeSpan.Zero);

    /// <summary>The installation's first synchronization: 2026-10-22, 10:00 in Kyiv.</summary>
    public static readonly DateTimeOffset FirstRun = new(2026, 10, 22, 7, 0, 0, TimeSpan.Zero);

    /// <summary>The later run of AC-007, AC-008: 2026-11-05, 10:00 in Kyiv.</summary>
    public static readonly DateTimeOffset LaterRun = new(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);

    /// <summary>A course item in October 2025, so the journal of 01.09–31.12.2025 has a column.</summary>
    public static readonly DateTimeOffset October2025Item = new(2025, 10, 10, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A course item in November 2025 (AC-005).</summary>
    public static readonly DateTimeOffset November2025Item = new(2025, 11, 12, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A course item in October 2026, before the first run.</summary>
    public static readonly DateTimeOffset October2026Item = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A roster entry with an invented name, address and id (TC-4).</summary>
    public static RosterEntry Person(int ordinal) =>
        new(CourseTestData.UserId(ordinal), CourseTestData.Email($"person{ordinal}"), CourseTestData.Name(ordinal));

    /// <summary>The installation's first run on <see cref="FirstRun"/>; returns once it has completed.</summary>
    public static async Task<InstallationTestHost> StartFirstRunAsync(
        PostgreSqlFixture database,
        Action<FakeClassroomReader> classroom,
        CancellationToken cancellationToken,
        Func<InstallationTestHost, CancellationToken, Task>? seed = null)
    {
        var host = await SyncHostExtensions.StartAsync(database, cancellationToken, classroom: classroom, seed: seed, now: FirstRun);
        var run = await host.WaitForFinishedRunAsync(cancellationToken);
        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        return host;
    }

    /// <summary>
    /// Stops <paramref name="previous"/> and starts the installation again over the same database with its clock on
    /// <paramref name="now"/> and Google as <paramref name="classroom"/> seeds it; returns once that start's run has
    /// completed. The caller disposes both hosts; only the first one owns the database.
    /// </summary>
    public static async Task<InstallationTestHost> RunAgainAtAsync(
        InstallationTestHost previous,
        DateTimeOffset now,
        Action<FakeClassroomReader> classroom,
        CancellationToken cancellationToken)
    {
        var before = Assert.Single(await previous.SyncStatesAsync(cancellationToken));
        await previous.StopAsync();
        var next = InstallationTestHost.Restart(previous, now);

        // The last successful check is older than the grace period on a later day; the start-up check succeeds, which
        // is also what the run waits for (US-013 FR-011).
        next.ControlPlane.ReplySuccess().ReplySuccess().ReplySuccess();
        classroom(next.Classroom);
        next.Start();
        await next.Time.WaitUntilAsync(
            () => next.SyncStatesAsync(cancellationToken).GetAwaiter().GetResult() is [{ } row]
                  && row.RunId != before.RunId
                  && row.Status != SyncTestData.Status.Running,
            "a finished synchronization run of the restarted host",
            cancellationToken);
        var run = Assert.Single(await next.SyncStatesAsync(cancellationToken));
        Assert.Equal(SyncTestData.Status.Completed, run.Status);
        return next;
    }

    /// <summary>The <c>first_seen_at</c>, <c>last_seen_at</c> and <c>on_roster</c> of one person's membership in one course.</summary>
    public static async Task<(DateTimeOffset FirstSeenAt, DateTimeOffset LastSeenAt, bool OnRoster)> MembershipAsync(
        this InstallationTestHost host,
        string courseGoogleId,
        string googleUserId,
        CancellationToken cancellationToken)
    {
        var rows = await host.QueryAsync(
            """
            SELECT m.first_seen_at, m.last_seen_at, m.on_roster
            FROM course_membership m
            JOIN course c ON c.id = m.course_id
            JOIN classroom_participant p ON p.id = m.participant_id
            WHERE c.google_id = @course AND p.google_user_id = @user
            """,
            r => (r.GetFieldValue<DateTimeOffset>(0), r.GetFieldValue<DateTimeOffset>(1), r.GetBoolean(2)),
            cancellationToken,
            ("course", courseGoogleId),
            ("user", googleUserId));
        return Assert.Single(rows);
    }

    /// <summary>The installation id of a course by its Google id.</summary>
    public static Task<long> CourseIdAsync(this InstallationTestHost host, string courseGoogleId, CancellationToken cancellationToken) =>
        host.ScalarAsync<long>("SELECT id FROM course WHERE google_id = @id", cancellationToken, ("id", courseGoogleId));
}
