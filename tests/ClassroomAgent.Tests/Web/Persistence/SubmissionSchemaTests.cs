using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;

namespace ClassroomAgent.Tests.Web.Persistence;

/// <summary>
/// US-015 db-design §4: the migration creates <c>submission</c> with the course-work-scoped upsert key
/// Specification v2 fixes, the deliberately non-unique cell lookup of §4.3, and the state/raw-state biconditional
/// OD-005 requires. Against real PostgreSQL via Testcontainers; the InMemory provider enforces none of this (TC-2).
/// </summary>
/// <remarks>
/// US-015 OD-012: until <c>AddCourseWorkAndSubmissions</c> exists, every test here is expected to fail because the
/// table itself is absent, not because a constraint is wrong (test-strategy §9). <see cref="TheTable_Exists"/> is
/// asserted first for exactly that reason.
/// </remarks>
public sealed class SubmissionSchemaTests(PostgreSqlFixture database)
{
    /// <summary>db-design §10 "table presence": asserted first, so the absence test below is not vacuous.</summary>
    [Fact]
    public async Task TheTable_Exists()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var tables = await host.TableNamesAsync(ct);

        Assert.Contains(tables, t => t == CourseWorkTestData.SubmissionTable);
    }

    /// <summary>db-design §4.3, spec FR-006: the upsert key is scoped by the owning course work.</summary>
    [Fact]
    public async Task DuplicateGoogleIdUnderTheSameCourseWork_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var courseWork = await CourseWorkRows.InsertCourseWorkAsync(host, course, ct);
        var participant = await CourseRows.InsertParticipantAsync(host, ct);
        await CourseWorkRows.InsertSubmissionAsync(
            host, courseWork, participant, ct, googleId: CourseWorkTestData.SubmissionId(1));

        var second = async () => await CourseWorkRows.InsertSubmissionAsync(
            host, courseWork, participant, ct, googleId: CourseWorkTestData.SubmissionId(1));

        var error = await Assert.ThrowsAsync<PostgresException>(second);
        Assert.Equal("23505", error.SqlState);
        Assert.Equal(CourseWorkTestData.Constraints.SubmissionCourseWorkGoogleId, error.ConstraintName);
    }

    /// <summary>
    /// db-design §4.3, Specification v2's correction: the same submission id under a <b>different</b> piece of
    /// work must succeed — Classroom documents a submission id as unique only among its own course work's.
    /// </summary>
    [Fact]
    public async Task TheSameGoogleIdUnderAnotherCourseWork_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var first = await CourseWorkRows.InsertCourseWorkAsync(host, course, ct, googleId: CourseWorkTestData.ItemId(1));
        var second = await CourseWorkRows.InsertCourseWorkAsync(host, course, ct, googleId: CourseWorkTestData.ItemId(2));
        var participant = await CourseRows.InsertParticipantAsync(host, ct);
        await CourseWorkRows.InsertSubmissionAsync(
            host, first, participant, ct, googleId: CourseWorkTestData.SubmissionId(1));

        var id = await CourseWorkRows.InsertSubmissionAsync(
            host, second, participant, ct, googleId: CourseWorkTestData.SubmissionId(1));

        Assert.True(id > 0);
    }

    /// <summary>
    /// db-design §4.3: two submissions of one item by one student must succeed — the journal's cell lookup is
    /// deliberately not a unique index, so a Google-side repetition shows up as two cells, not a failed import.
    /// </summary>
    [Fact]
    public async Task TwoSubmissionsOfOneItemByOneStudent_AreAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var courseWork = await CourseWorkRows.InsertCourseWorkAsync(host, course, ct);
        var participant = await CourseRows.InsertParticipantAsync(host, ct);
        await CourseWorkRows.InsertSubmissionAsync(
            host, courseWork, participant, ct, googleId: CourseWorkTestData.SubmissionId(1));

        var id = await CourseWorkRows.InsertSubmissionAsync(
            host, courseWork, participant, ct, googleId: CourseWorkTestData.SubmissionId(2));

        Assert.True(id > 0);
    }

    /// <summary>
    /// db-design §4.1, BR-059: nothing beyond the design's stored list — no file, no answer, no attachment, no
    /// grade-change history and no rubric grade. Presence is asserted first so this absence is not vacuous.
    /// </summary>
    [Fact]
    public async Task TheTable_HasNoContentOrHistoryColumn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var tables = await host.TableNamesAsync(ct);
        Assert.Contains(tables, t => t == CourseWorkTestData.SubmissionTable);

        var columns = await host.QueryAsync(
            "SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @table",
            r => r.GetString(0),
            ct,
            ("table", CourseWorkTestData.SubmissionTable));

        Assert.DoesNotContain(columns, c => c is "content" or "answer" or "attachment" or "history" or "rubric_grade");
    }

    /// <summary>db-design §4.2: a state outside the seven-code vocabulary is rejected.</summary>
    [Fact]
    public async Task StateOutsideTheVocabulary_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var courseWork = await CourseWorkRows.InsertCourseWorkAsync(host, course, ct);
        var participant = await CourseRows.InsertParticipantAsync(host, ct);

        var insert = async () => await CourseWorkRows.InsertSubmissionAsync(
            host, courseWork, participant, ct, state: "graded");

        var error = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal("23514", error.SqlState);
        Assert.Equal(CourseWorkTestData.Constraints.SubmissionState, error.ConstraintName);
    }

    /// <summary>db-design §4.2, OD-005: the marker without the raw string is rejected — biconditional half one.</summary>
    [Fact]
    public async Task UnrecognisedWithoutRawState_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var courseWork = await CourseWorkRows.InsertCourseWorkAsync(host, course, ct);
        var participant = await CourseRows.InsertParticipantAsync(host, ct);

        var insert = async () => await CourseWorkRows.InsertSubmissionAsync(
            host, courseWork, participant, ct, state: CourseWorkTestData.StateCodes.Unrecognised, rawState: null);

        var error = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal("23514", error.SqlState);
        Assert.Equal(CourseWorkTestData.Constraints.SubmissionRawState, error.ConstraintName);
    }

    /// <summary>db-design §4.2, OD-005: a recognised state carrying a leftover raw string is rejected — biconditional half two.</summary>
    [Fact]
    public async Task RecognisedWithRawState_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var courseWork = await CourseWorkRows.InsertCourseWorkAsync(host, course, ct);
        var participant = await CourseRows.InsertParticipantAsync(host, ct);

        var insert = async () => await CourseWorkRows.InsertSubmissionAsync(
            host,
            courseWork,
            participant,
            ct,
            state: CourseWorkTestData.StateCodes.TurnedIn,
            rawState: CourseWorkTestData.UnrecognisedState);

        var error = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal("23514", error.SqlState);
        Assert.Equal(CourseWorkTestData.Constraints.SubmissionRawState, error.ConstraintName);
    }

    /// <summary>db-design §4.4, PC-8: deleting a course work that has submissions is refused, so the purge must delete explicitly.</summary>
    [Fact]
    public async Task DeletingCourseWorkThatHasSubmissions_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var courseWork = await CourseWorkRows.InsertCourseWorkAsync(host, course, ct);
        var participant = await CourseRows.InsertParticipantAsync(host, ct);
        await CourseWorkRows.InsertSubmissionAsync(host, courseWork, participant, ct);

        var delete = async () => await host.ExecuteAsync("DELETE FROM course_work WHERE id = @id", ct, ("id", courseWork));

        var error = await Assert.ThrowsAsync<PostgresException>(delete);
        Assert.Equal("23503", error.SqlState);
        Assert.Equal(CourseWorkTestData.Constraints.SubmissionCourseWorkForeignKey, error.ConstraintName);
    }

    /// <summary>db-design §4.2, VR-001: data sanity on external input — a negative assigned or draft grade is rejected.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task NegativeGrades_AreRejected(bool negativeAssigned, bool negativeDraft)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var courseWork = await CourseWorkRows.InsertCourseWorkAsync(host, course, ct);
        var participant = await CourseRows.InsertParticipantAsync(host, ct);

        var insert = async () => await CourseWorkRows.InsertSubmissionAsync(
            host,
            courseWork,
            participant,
            ct,
            assignedGrade: negativeAssigned ? -1m : (decimal?)null,
            draftGrade: negativeDraft ? -1m : (decimal?)null);

        var error = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal("23514", error.SqlState);
        Assert.Equal(CourseWorkTestData.Constraints.SubmissionGradesNonNegative, error.ConstraintName);
    }

    /// <summary>db-design §10 item 16, spec FR-020: a scheduled run that imports coursework and submissions still writes no audit row.</summary>
    [Fact]
    public async Task AScheduledRun_WritesNoAuditRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SyncHostExtensions.StartAsync(
            database,
            ct,
            classroom: reader => reader
                .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)])
                .WithCourseWork(
                    CourseTestData.CourseId(1),
                    CourseWorkTestData.ItemId(1),
                    CourseWorkResource.CourseWork,
                    CourseWorkTestData.Title(1),
                    itemDate: InstallationTestHost.DefaultStart)
                .WithSubmission(
                    CourseTestData.CourseId(1),
                    CourseWorkTestData.ItemId(1),
                    CourseTestData.UserId(1),
                    CourseWorkTestData.SubmissionId(1),
                    CourseWorkTestData.RawStates.TurnedIn));
        await host.WaitForFinishedRunAsync(ct);

        Assert.Empty(await host.AuditRowsAsync(ct));
    }

    private static RosterEntry Person(int ordinal) =>
        new(CourseTestData.UserId(ordinal), CourseTestData.Email($"person{ordinal}"), CourseTestData.Name(ordinal));
}
