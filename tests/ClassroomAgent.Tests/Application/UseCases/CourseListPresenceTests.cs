using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-059 spec FR-001 … FR-006, AC-001 … AC-008, AC-012 … AC-018, db-design §6 (R1, R2): once Google's course list has
/// been read to its end, a stored course the list no longer yields is kept and marked — "not in Classroom", or "not
/// visible to the technical account" when it was last seen before the account changed — a listed course records the
/// run's instant as its last sighting and loses its mark, and an empty list while courses are stored marks nothing and
/// sets the <c>SyncState</c> flag. A run that stops before the end of the list changes none of it. The use case is the
/// unit and the Classroom port is substituted (TC-1, TC-4); the columns are proven in
/// <c>CourseListMarksPersistenceTests</c>, the log line in <c>CourseListPresenceSyncTests</c>.
/// </summary>
public sealed class CourseListPresenceTests
{
    private static readonly DateTimeOffset T = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Later = TimeSpan.FromHours(1);

    /// <summary>A course state the program does not know (US-014 VR-002).</summary>
    private const string UnknownState = "COURSE_STATE_FROM_THE_FUTURE";

    private const decimal Grade = 7m;

    private static RosterEntry Person(int ordinal) =>
        new(CourseTestData.UserId(ordinal), CourseTestData.Email($"person{ordinal}"), CourseTestData.Name(ordinal));

    /// <summary>Course <paramref name="n"/> with one student, one graded item and the student's submission.</summary>
    private static SyncWorld.ClassroomReader WithCourse(
        SyncWorld.ClassroomReader reader,
        int n,
        string? name = null,
        string state = CourseTestData.States.Active,
        DateTimeOffset? updateTime = null)
    {
        var id = CourseTestData.CourseId(n);
        return reader
            .WithCourse(id, name ?? CourseTestData.CourseName(n), state, students: [Person(n)], creationTime: T, updateTime: updateTime ?? T)
            .WithCourseWork(id, CourseWorkTestData.ItemId(n), CourseWorkResource.CourseWork, CourseWorkTestData.Title(n), T, maxPoints: 10m, creationTime: T, updateTime: T)
            .WithSubmission(id, CourseWorkTestData.ItemId(n), CourseTestData.UserId(n), CourseWorkTestData.SubmissionId(n), CourseWorkTestData.RawStates.TurnedIn, assignedGrade: Grade, updateTime: T);
    }

    /// <summary>A reader listing the given ordinary courses, in order.</summary>
    private static SyncWorld.ClassroomReader Listing(params int[] courses)
    {
        var reader = new SyncWorld.ClassroomReader();
        foreach (var n in courses)
        {
            WithCourse(reader, n);
        }

        return reader;
    }

    private static async Task<(DateTimeOffset At, SynchronizationRunOutcome Outcome)> RunAsync(
        SyncWorld world,
        SyncWorld.ClassroomReader reader,
        int ordinal,
        CancellationToken ct)
    {
        var at = world.Time.GetUtcNow();
        var outcome = await world.RunUsing(reader).ExecuteAsync(SyncWorld.RunId(ordinal), ct);
        world.Time.Advance(Later);
        return (at, outcome);
    }

    private static Course CourseOf(SyncWorld world, int n) => world.Courses.Stored[CourseTestData.CourseId(n)];

    private static CourseWork ItemOf(SyncWorld world, int n) =>
        world.CourseWork.Stored.Values.Single(w => w.GoogleId == CourseWorkTestData.ItemId(n));

    private static Submission SubmissionOf(SyncWorld world, int n) =>
        world.Submissions.Stored.Values.Single(s => s.GoogleId == CourseWorkTestData.SubmissionId(n));

    /// <summary>The account the connection is changed to: same domain, another address (BR-049).</summary>
    private const string NewAccount = WorkspaceConnectionTestData.OtherTechnicalAccount;

    /// <summary>FR-002: the Admin saves the connection with another technical account, at the clock's instant.</summary>
    private static DateTimeOffset ChangeAccount(SyncWorld world)
    {
        var at = world.Time.GetUtcNow();
        world.Connections.Stored!.ChangeTo(InstallationTestData.Domain, NewAccount, at);
        world.Time.Advance(Later);
        return at;
    }

    private static void AssertNoMark(Course course)
    {
        Assert.Null(course.AbsenceMark);
        Assert.Null(course.AbsentSince);
    }

    private static void AssertMark(Course course, CourseAbsenceMark mark, DateTimeOffset since)
    {
        Assert.Equal(mark, course.AbsenceMark);
        Assert.Equal(since, course.AbsentSince);
    }

    /// <summary>FR-001.3: the absent course's stored data — state, item, submission, grade — is exactly as before.</summary>
    private static void AssertDataKept(SyncWorld world, int n)
    {
        var course = CourseOf(world, n);
        Assert.Equal(CourseTestData.CourseName(n), course.Name);
        Assert.Equal(CourseState.Active, course.State);
        var item = ItemOf(world, n);
        Assert.Equal(CourseWorkTestData.Title(n), item.Title);
        Assert.Null(item.NotInClassroomSince);
        Assert.Equal(Grade, SubmissionOf(world, n).AssignedGrade);
    }

    // ---- FR-001, FR-004: "not in Classroom" -----------------------------------------------------------------------

    /// <summary>
    /// AC-001, FR-001.2, FR-001.3: two stored courses, the list (read to its end) yields only the first. The second is
    /// kept with its state, item, submission and grade, and marked "not in Classroom" with the run's instant; its item
    /// gets no mark of its own; the first has no mark.
    /// </summary>
    [Fact]
    public async Task ACourseGoogleNoLongerLists_IsKeptWithItsData_AndMarkedNotInClassroom()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, Listing(1, 2), 1, ct);

        var (at, outcome) = await RunAsync(world, Listing(1), 2, ct);

        Assert.False(outcome.Failed);
        AssertMark(CourseOf(world, 2), CourseAbsenceMark.NotInClassroom, at);
        AssertDataKept(world, 2);
        AssertNoMark(CourseOf(world, 1));
    }

    /// <summary>FR-001.1, BR-047: an archived course is a stored course too — it is marked when the list no longer yields it.</summary>
    [Fact]
    public async Task AnArchivedCourseNoLongerListed_IsMarkedToo()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, WithCourse(Listing(1), 2, state: CourseTestData.States.Archived), 1, ct);

        var (at, _) = await RunAsync(world, Listing(1), 2, ct);

        AssertMark(CourseOf(world, 2), CourseAbsenceMark.NotInClassroom, at);
        Assert.Equal(CourseState.Archived, CourseOf(world, 2).State);
    }

    /// <summary>AC-002, FR-001.2, FR-006.5: a course marked by an earlier run and still absent keeps the earlier instant.</summary>
    [Fact]
    public async Task AMarkedCourseStillAbsent_KeepsTheFirstInstant()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, Listing(1, 2), 1, ct);
        var (marked, _) = await RunAsync(world, Listing(1), 2, ct);

        var (_, outcome) = await RunAsync(world, Listing(1), 3, ct);

        Assert.False(outcome.Failed);
        AssertMark(CourseOf(world, 2), CourseAbsenceMark.NotInClassroom, marked);
    }

    /// <summary>
    /// AC-003, FR-004: a marked course the list yields again under a changed name loses its mark and carries the new
    /// name; its last sighting is this run's instant.
    /// </summary>
    [Fact]
    public async Task AMarkedCourseThatComesBack_LosesTheMark_AndTakesTheNewName()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, Listing(1, 2), 1, ct);
        await RunAsync(world, Listing(1), 2, ct);
        Assert.NotNull(CourseOf(world, 2).AbsenceMark);

        var (at, outcome) = await RunAsync(world, WithCourse(Listing(1), 2, "Renamed in Classroom"), 3, ct);

        Assert.False(outcome.Failed);
        AssertNoMark(CourseOf(world, 2));
        Assert.Equal("Renamed in Classroom", CourseOf(world, 2).Name);
        Assert.Equal(at, CourseOf(world, 2).LastListedAt);
    }

    /// <summary>
    /// AC-004, FR-001.2, FR-003.1: stored courses the list yields but the run does not read — an unchanged archived
    /// course, a course skipped for out-of-range values, a course in an unknown state — are not marked, and each records
    /// the run's instant as its last sighting. The archive is proved unread and the other two proved skipped.
    /// </summary>
    [Fact]
    public async Task ListedCoursesTheRunDoesNotRead_AreNotMarked_AndAreSighted()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var archiveUpdated = T.AddDays(-30);
        await RunAsync(world, WithCourse(Listing(2, 3, 4), 1, state: CourseTestData.States.Archived, updateTime: archiveUpdated), 1, ct);

        var second = WithCourse(Listing(2, 4), 1, state: CourseTestData.States.Archived, updateTime: archiveUpdated);
        second.WithCourseWorkFailure(CourseTestData.CourseId(2), new GoogleReadFailedException(GoogleReadFailureKind.ValueOutOfRange, null));
        WithCourse(second, 3, state: UnknownState);
        var (at, outcome) = await RunAsync(world, second, 2, ct);

        Assert.False(outcome.Failed);
        Assert.Equal(1, outcome.CoursesSkippedUnchanged);
        Assert.Equal([CourseTestData.CourseId(2)], outcome.CoursesValueOutOfRange);
        Assert.Contains(outcome.SkippedCourses, s => s.GoogleId == CourseTestData.CourseId(3));
        foreach (var n in new[] { 1, 2, 3 })
        {
            AssertNoMark(CourseOf(world, n));
            Assert.Equal(at, CourseOf(world, n).LastListedAt);
        }
    }

    /// <summary>
    /// AC-016, FR-003.1: an unchanged archived course the list yields and the run does not read records the run's instant
    /// as its last sighting, while its last complete read stays the earlier run's.
    /// </summary>
    [Fact]
    public async Task AnUnchangedArchiveOnTheList_RecordsTheRunInstantAsItsLastSighting()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var archiveUpdated = T.AddDays(-30);
        var (first, _) = await RunAsync(world, WithCourse(new(), 1, state: CourseTestData.States.Archived, updateTime: archiveUpdated), 1, ct);

        var reader = WithCourse(new(), 1, state: CourseTestData.States.Archived, updateTime: archiveUpdated);
        var (at, outcome) = await RunAsync(world, reader, 2, ct);

        Assert.Equal(1, outcome.CoursesSkippedUnchanged);
        Assert.Empty(reader.RostersRead);
        Assert.Equal(at, CourseOf(world, 1).LastListedAt);
        Assert.Equal(first, CourseOf(world, 1).LastCompleteReadAt);
    }

    /// <summary>
    /// FR-004.1, I-7: a marked course the list yields again loses its mark even when the run skips it — here for an
    /// out-of-range value. The mark is tied to the list, not to a successful read.
    /// </summary>
    [Fact]
    public async Task AMarkedCourseOnTheListButNotRead_LosesItsMark()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, Listing(1, 2), 1, ct);
        await RunAsync(world, Listing(1), 2, ct);
        Assert.NotNull(CourseOf(world, 2).AbsenceMark);

        var third = Listing(1, 2);
        third.WithCourseWorkFailure(CourseTestData.CourseId(2), new GoogleReadFailedException(GoogleReadFailureKind.ValueOutOfRange, null));
        var (at, outcome) = await RunAsync(world, third, 3, ct);

        Assert.Equal([CourseTestData.CourseId(2)], outcome.CoursesValueOutOfRange);
        AssertNoMark(CourseOf(world, 2));
        Assert.Equal(at, CourseOf(world, 2).LastListedAt);
    }

    /// <summary>
    /// AC-018, FR-001.5, FR-003.1: a course the list yields and Classroom then reports gone (<c>404</c>) during its reads
    /// is not marked in that run — it was listed — and its last sighting is the run's instant.
    /// </summary>
    [Fact]
    public async Task ACourseListedButGoneDuringItsReads_IsNotMarked_AndIsSighted()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, Listing(1, 2), 1, ct);

        var second = Listing(1, 2);
        second.WithRosterFailure(CourseTestData.CourseId(2), new GoogleReadFailedException(GoogleReadFailureKind.CourseGone, null));
        var (at, outcome) = await RunAsync(world, second, 2, ct);

        Assert.Equal([CourseTestData.CourseId(2)], outcome.CoursesGone);
        AssertNoMark(CourseOf(world, 2));
        Assert.Equal(at, CourseOf(world, 2).LastListedAt);
    }

    // ---- FR-001.4: a run that stops before the end of the list -------------------------------------------------------

    /// <summary>
    /// AC-005, FR-001.4, FR-005.4, I-2: a stored unmarked course absent from the list, a marked course the list yields,
    /// and a list whose enumeration then fails. The run fails; the first course gets no mark, the second keeps its mark,
    /// instant and earlier sighting, and the empty-list flag is untouched.
    /// </summary>
    [Fact]
    public async Task ARunWhoseListFailsPartWay_MarksAndUnmarksNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var (firstSeen, _) = await RunAsync(world, Listing(1, 2, 3), 1, ct);
        var (marked, _) = await RunAsync(world, Listing(1, 2), 2, ct);
        AssertMark(CourseOf(world, 3), CourseAbsenceMark.NotInClassroom, marked);

        var third = Listing(3);
        third.FailAfterCourses = new GoogleReadFailedException(GoogleReadFailureKind.Configuration, SyncDiagnosis.TechnicalAccountCannotRead);
        var (_, outcome) = await RunAsync(world, third, 3, ct);

        Assert.True(outcome.Failed);
        AssertNoMark(CourseOf(world, 1));
        AssertNoMark(CourseOf(world, 2));
        AssertMark(CourseOf(world, 3), CourseAbsenceMark.NotInClassroom, marked);
        Assert.Equal(firstSeen, CourseOf(world, 3).LastListedAt);
        Assert.False(world.States.Stored!.LastCourseListEmpty);
    }

    /// <summary>
    /// AC-005, FR-001.4: a run stopped by a course's read (a permission failure) never reaches the end of the list —
    /// the course it no longer lists gets no mark, and a course it already went through records no sighting.
    /// </summary>
    [Fact]
    public async Task ARunStoppedByACourseRead_MarksNothing_AndRecordsNoSighting()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var (firstSeen, _) = await RunAsync(world, Listing(1, 2, 3), 1, ct);

        var second = Listing(1, 2);
        second.WithRosterFailure(
            CourseTestData.CourseId(2),
            new GoogleReadFailedException(GoogleReadFailureKind.Configuration, SyncDiagnosis.TechnicalAccountCannotRead));
        var (_, outcome) = await RunAsync(world, second, 2, ct);

        Assert.True(outcome.Failed);
        AssertNoMark(CourseOf(world, 3));
        Assert.Equal(firstSeen, CourseOf(world, 1).LastListedAt);
    }

    // ---- FR-005: the empty list --------------------------------------------------------------------------------------

    /// <summary>
    /// AC-006, FR-005.2: stored courses — one marked — a skip record, and a list read to its end with no course. No course
    /// gains or loses a mark, no sighting is recorded, the skip record stays, and <c>SyncState</c> holds the flag. The run
    /// completes (I-5).
    /// </summary>
    [Fact]
    public async Task AnEmptyListWhileCoursesAreStored_MarksNothing_KeepsSkipRecords_AndSetsTheFlag()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, WithCourse(Listing(1, 2), 9, state: UnknownState), 1, ct);
        var (marked, _) = await RunAsync(world, WithCourse(Listing(1), 9, state: UnknownState), 2, ct);
        var recorded = world.SkippedCourses.Stored[CourseTestData.CourseId(9)].SkippedSince;
        var sighted = CourseOf(world, 1).LastListedAt;
        Assert.NotNull(sighted);

        var (_, outcome) = await RunAsync(world, new(), 3, ct);

        Assert.False(outcome.Failed);
        AssertNoMark(CourseOf(world, 1));
        AssertMark(CourseOf(world, 2), CourseAbsenceMark.NotInClassroom, marked);
        Assert.Equal(sighted, CourseOf(world, 1).LastListedAt);
        Assert.Equal(recorded, world.SkippedCourses.Stored[CourseTestData.CourseId(9)].SkippedSince);
        Assert.True(world.States.Stored!.LastCourseListEmpty);
    }

    /// <summary>
    /// FR-005.1, I-4: a list whose only entry has no usable id is empty — no stored course is marked and the flag is set.
    /// The entry is of an unknown state, so the run skips it before any write (VR-002 of US-014).
    /// </summary>
    [Fact]
    public async Task AListWithOnlyIdlessEntries_IsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, Listing(1), 1, ct);

        var second = new SyncWorld.ClassroomReader().WithCourse("   ", "No id", UnknownState);
        var (_, outcome) = await RunAsync(world, second, 2, ct);

        Assert.False(outcome.Failed);
        AssertNoMark(CourseOf(world, 1));
        Assert.True(world.States.Stored!.LastCourseListEmpty);
    }

    /// <summary>
    /// AC-007, FR-005.3: <c>SyncState</c> holds the flag; a run reads a non-empty list. The flag is cleared and the stored
    /// course the list lacks is marked by FR-001 with that run's instant.
    /// </summary>
    [Fact]
    public async Task AFlaggedEmptyList_IsClearedByANonEmptyList_AndAbsentCoursesAreMarked()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, Listing(1, 2), 1, ct);
        await RunAsync(world, new(), 2, ct);
        Assert.True(world.States.Stored!.LastCourseListEmpty);

        var (at, _) = await RunAsync(world, Listing(1), 3, ct);

        Assert.False(world.States.Stored.LastCourseListEmpty);
        AssertMark(CourseOf(world, 2), CourseAbsenceMark.NotInClassroom, at);
        AssertNoMark(CourseOf(world, 1));
    }

    /// <summary>
    /// AC-008, FR-005.3: with no course stored an empty list is not a problem — the run completes and the flag is not
    /// set. Made non-vacuous by starting from a set flag: the stored course was then removed (as the retention purge
    /// removes one), and the next empty list clears the flag.
    /// </summary>
    [Fact]
    public async Task AnEmptyListWithNoCourseStored_CompletesAndClearsTheFlag()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, Listing(1), 1, ct);
        await RunAsync(world, new(), 2, ct);
        Assert.True(world.States.Stored!.LastCourseListEmpty);
        world.Courses.Stored.Clear();

        var (_, outcome) = await RunAsync(world, new(), 3, ct);

        Assert.False(outcome.Failed);
        Assert.False(world.States.Stored.LastCourseListEmpty);
    }

    // ---- FR-003, FR-006: the technical account's change ------------------------------------------------------------

    /// <summary>
    /// AC-012, FR-003, FR-006.1: two courses a run listed; the technical account then changes; the list now yields only
    /// the first. The second carries "not visible to the technical account" with this run's instant, its data kept; the
    /// first has no mark and its last sighting is this run's instant.
    /// </summary>
    [Fact]
    public async Task ACourseLostAfterTheAccountChanged_IsMarkedNotVisible()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, Listing(1, 2), 1, ct);
        ChangeAccount(world);

        var (at, outcome) = await RunAsync(world, Listing(1), 2, ct);

        Assert.False(outcome.Failed);
        AssertMark(CourseOf(world, 2), CourseAbsenceMark.NotVisibleToTechnicalAccount, at);
        AssertDataKept(world, 2);
        AssertNoMark(CourseOf(world, 1));
        Assert.Equal(at, CourseOf(world, 1).LastListedAt);
    }

    /// <summary>AC-013, FR-006.2: the account changes, a later run lists the course, the run after that lacks it — "not in Classroom".</summary>
    [Fact]
    public async Task ACourseTheNewAccountSaw_IsNotInClassroomWhenLost()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, Listing(1, 2), 1, ct);
        ChangeAccount(world);
        await RunAsync(world, Listing(1, 2), 2, ct);

        var (at, _) = await RunAsync(world, Listing(1), 3, ct);

        AssertMark(CourseOf(world, 2), CourseAbsenceMark.NotInClassroom, at);
    }

    /// <summary>
    /// AC-014, FR-006.4, FR-004, FR-003: a course marked "not visible" keeps the earlier instant while the list still lacks
    /// it; when a later list yields it, it has no mark and its last sighting is that run's instant.
    /// </summary>
    [Fact]
    public async Task ANotVisibleCourse_KeepsItsInstant_AndLosesTheMarkWhenListedAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, Listing(1, 2), 1, ct);
        ChangeAccount(world);
        var (marked, _) = await RunAsync(world, Listing(1), 2, ct);

        await RunAsync(world, Listing(1), 3, ct);
        AssertMark(CourseOf(world, 2), CourseAbsenceMark.NotVisibleToTechnicalAccount, marked);

        var (back, _) = await RunAsync(world, Listing(1, 2), 4, ct);
        AssertNoMark(CourseOf(world, 2));
        Assert.Equal(back, CourseOf(world, 2).LastListedAt);
    }

    /// <summary>
    /// AC-015, FR-006.5: a course marked "not in Classroom", then the account changes; a run whose list still lacks it
    /// leaves the course with "not in Classroom" and its first instant — a marked course keeps the mark it has.
    /// </summary>
    [Fact]
    public async Task ACourseMarkedNotInClassroom_KeepsThatMarkAfterTheAccountChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, Listing(1, 2), 1, ct);
        var (marked, _) = await RunAsync(world, Listing(1), 2, ct);
        ChangeAccount(world);

        await RunAsync(world, Listing(1), 3, ct);

        AssertMark(CourseOf(world, 2), CourseAbsenceMark.NotInClassroom, marked);
    }

    /// <summary>
    /// AC-017, FR-006.3, I-1: a course stored with no sighting (as after the migration) and a list that lacks it — "not
    /// visible" when a change instant is recorded, "not in Classroom" when none is.
    /// </summary>
    [Theory]
    [InlineData(true, CourseAbsenceMark.NotVisibleToTechnicalAccount)]
    [InlineData(false, CourseAbsenceMark.NotInClassroom)]
    public async Task ACourseNeverSighted_IsMarkedByWhetherAChangeIsRecorded(bool changed, CourseAbsenceMark expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Courses.Add(Course.Import(
            CourseTestData.CourseId(5),
            CourseState.Active,
            new CourseDetails(CourseTestData.CourseName(5), null, null, null, null, null, T, T, null, null, null, null)));
        if (changed)
        {
            ChangeAccount(world);
        }

        var (at, _) = await RunAsync(world, Listing(1), 1, ct);

        AssertMark(CourseOf(world, 5), expected, at);
        Assert.Null(CourseOf(world, 5).LastListedAt);
    }

    // ---- db-design §6: the instants ----------------------------------------------------------------------------------

    /// <summary>
    /// db-design §6 R1, VR-004: the run takes its one instant before it reads the connection. The clock moves while the
    /// connection is read; every instant the run writes as its observation — the sighting and the last complete read —
    /// is the one from before that read.
    /// </summary>
    [Fact]
    public async Task TheRunInstant_IsTakenBeforeTheConnectionIsRead()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var before = world.Time.GetUtcNow();
        var moved = false;
        world.Connections.OnRead = () =>
        {
            if (!moved)
            {
                moved = true;
                world.Time.Advance(TimeSpan.FromSeconds(1));
            }
        };

        var outcome = await world.RunUsing(Listing(1)).ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.False(outcome.Failed);
        Assert.True(moved);
        Assert.Equal(before, CourseOf(world, 1).LastListedAt);
        Assert.Equal(before, CourseOf(world, 1).LastCompleteReadAt);
    }

    /// <summary>
    /// db-design §6 R2: the change instant is read at the end of the list. An Admin saves another technical account while
    /// the run is going through the list (the database now holds a new row object); the course the list lacks, last seen
    /// before that change, is marked "not visible" — the safe side — which only a read at the end can see.
    /// </summary>
    [Fact]
    public async Task AnAccountChangeSavedDuringTheRun_IsSeenAtTheEndOfTheList()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await RunAsync(world, Listing(1, 2), 1, ct);

        var second = Listing(1);
        second.AfterCourses = () =>
        {
            var saved = WorkspaceConnection.Create(InstallationTestData.Domain, WorkspaceConnectionTestData.TechnicalAccount);
            saved.ChangeTo(InstallationTestData.Domain, NewAccount, world.Time.GetUtcNow() + TimeSpan.FromSeconds(1));
            world.Connections.Replace(saved);
        };
        var (at, outcome) = await RunAsync(world, second, 2, ct);

        Assert.False(outcome.Failed);
        AssertMark(CourseOf(world, 2), CourseAbsenceMark.NotVisibleToTechnicalAccount, at);
    }
}
