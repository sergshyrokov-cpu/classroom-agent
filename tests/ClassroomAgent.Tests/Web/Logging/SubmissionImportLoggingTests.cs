using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Logging;

/// <summary>
/// US-015 AC-008, spec FR-017, S-06: once a run imports coursework and submissions, its lines still carry
/// counters, states and internal or Google identifiers only — never a grade, a student's name, email or Google
/// id, a course name or a work title (SC-10, DC-10). Proved in a real host, where those lines are actually
/// written, over real PostgreSQL (TC-2); the Classroom port is substituted, so nothing reaches Google (TC-4).
/// Extends US-014's <see cref="CourseImportLoggingTests"/>.
/// </summary>
public sealed class SubmissionImportLoggingTests(PostgreSqlFixture database)
{
    /// <summary>US-015 OD-005: the one Warning line the state vocabulary's marker adds, named as US-014's own course-skip line was.</summary>
    private const string UnrecognisedStateEvent = "SyncSubmissionStateUnrecognised";

    private static RosterEntry Student(int ordinal) =>
        new(CourseTestData.UserId(ordinal), CourseTestData.Email($"student{ordinal}"), CourseTestData.Name(ordinal));

    private static FakeClassroomReader World(FakeClassroomReader reader, string rawState, decimal? grade) =>
        reader
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Student(1)])
            .WithCourseWork(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseWorkResource.CourseWork,
                CourseWorkTestData.Title(1),
                itemDate: InstallationTestHost.DefaultStart,
                maxPoints: 100m)
            .WithSubmission(
                CourseTestData.CourseId(1),
                CourseWorkTestData.ItemId(1),
                CourseTestData.UserId(1),
                CourseWorkTestData.SubmissionId(1),
                rawState,
                assignedGrade: grade,
                draftGrade: grade);

    /// <summary>AC-008, FR-017: no line carries the grade Google reported.</summary>
    [Fact]
    public async Task ARunThatImportedSubmissions_WritesNoGrade()
    {
        var ct = TestContext.Current.CancellationToken;
        const decimal grade = 91.25m;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: reader => World(reader, CourseWorkTestData.RawStates.TurnedIn, grade));
        await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunCompleted, ct);

        var all = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));

        await AssertTheImportHappenedAsync(host, ct);
        Assert.DoesNotContain(grade.ToString(System.Globalization.CultureInfo.InvariantCulture), all, StringComparison.Ordinal);
    }

    /// <summary>
    /// The control every absence assertion in this class depends on. "No grade appears in a log line" is equally
    /// true of a run that imported graded submissions <b>and</b> of a run that imported nothing at all, so each
    /// test asserts the rows really arrived before asserting what the log does not contain (US-014's
    /// <c>CourseImportLoggingTests</c> pattern; the red-phase rule).
    /// </summary>
    private static async Task AssertTheImportHappenedAsync(InstallationTestHost host, CancellationToken ct)
    {
        Assert.Equal(1, await host.ScalarAsync<long>("SELECT count(*) FROM course_work", ct));
        Assert.Equal(1, await host.ScalarAsync<long>("SELECT count(*) FROM submission", ct));
    }

    /// <summary>AC-008, SC-10: no line carries the student's name, email address or Google user id.</summary>
    [Fact]
    public async Task NoStudentNameEmailOrGoogleIdIsLogged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: reader => World(reader, CourseWorkTestData.RawStates.TurnedIn, 80m));
        await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunCompleted, ct);

        var all = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));

        await AssertTheImportHappenedAsync(host, ct);
        Assert.DoesNotContain(CourseTestData.Name(1), all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(CourseTestData.Email("student1"), all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(CourseTestData.UserId(1), all, StringComparison.Ordinal);
    }

    /// <summary>AC-008, SC-10: no line carries the course name or the coursework title.</summary>
    [Fact]
    public async Task NoCourseNameOrWorkTitleIsLogged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: reader => World(reader, CourseWorkTestData.RawStates.TurnedIn, 80m));
        await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunCompleted, ct);

        var all = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));

        await AssertTheImportHappenedAsync(host, ct);
        Assert.DoesNotContain(CourseTestData.CourseName(1), all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(CourseWorkTestData.Title(1), all, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The mandatory control (test-strategy §9.1): the run really did store the coursework and the submission, so
    /// the three absences above are not simply the absence of an import.
    /// </summary>
    [Fact]
    public async Task TheImportReallyHappened()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: reader => World(reader, CourseWorkTestData.RawStates.TurnedIn, 80m));
        await host.WaitForLogEventAsync(SyncTestData.LogEvents.RunCompleted, ct);

        var courseWork = await host.ScalarAsync<long>("SELECT count(*) FROM course_work", ct);
        var submissions = await host.ScalarAsync<long>("SELECT count(*) FROM submission", ct);

        Assert.Equal(1, courseWork);
        Assert.Equal(1, submissions);
    }

    /// <summary>OD-005: the one Warning line carries the raw state string and the submission id, and nothing else identifying.</summary>
    [Fact]
    public async Task UnrecognisedStateWarning_CarriesOnlyTheStateAndTheSubmissionId()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: reader => World(reader, CourseWorkTestData.UnrecognisedState, null));
        await host.WaitForLogEventAsync(UnrecognisedStateEvent, ct);
        var events = await HostLogs.ReadEventsAsync(host.LogDirectory, ct);

        var line = Assert.Single(events, e => e.EventName == UnrecognisedStateEvent);
        Assert.Equal("Warning", line.Level);
        Assert.Equal(CourseWorkTestData.UnrecognisedState, line.Property("RawState"));
        Assert.Equal(CourseWorkTestData.SubmissionId(1), line.Property("SubmissionGoogleId"));

        var all = string.Join('\n', await HostLogs.ReadFilesAsync(host.LogDirectory, ct));
        Assert.DoesNotContain(CourseTestData.Name(1), all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(CourseTestData.CourseName(1), all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(CourseWorkTestData.Title(1), all, StringComparison.OrdinalIgnoreCase);
    }
}
