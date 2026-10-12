using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-018 AC-001 … AC-006, AC-009, AC-010: the run skips an archived course whose listed <c>updateTime</c> equals the
/// one remembered at its last complete read, reads every other course in full, and once a week reads everything
/// (spec FR-001 … FR-008, VR-001 … VR-003, OD-001 a, OD-002 a). Every port is in memory and the Classroom reader is
/// substituted, so a skipped read is observed as a call not made (TC-1, TC-4).
/// </summary>
/// <remarks>
/// Most tests run twice over the same stores: run 1 is the installation's first run, which is always the full run
/// (FR-005) and so reads and remembers every course; run 2, an hour later, is not full. Each run gets its own reader
/// so the read lists are per run, and run 2's reader answers with a different roster, item and name, so anything
/// it wrote would show.
/// </remarks>
public sealed class IncrementalSyncRunTests
{
    private static readonly DateTimeOffset T = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset T2 = T.AddHours(5);

    private static readonly TimeSpan Later = TimeSpan.FromHours(1);

    private static string Id => CourseTestData.CourseId(1);

    private static RosterEntry Person(int ordinal) =>
        new(CourseTestData.UserId(ordinal), CourseTestData.Email($"person{ordinal}"), CourseTestData.Name(ordinal));

    /// <summary>One course with one student, one item and one submission — what run 1 reads and stores.</summary>
    private static SyncWorld.ClassroomReader Reader(string state, DateTimeOffset? updateTime, int ordinal = 1) =>
        new SyncWorld.ClassroomReader()
            .WithCourse(Id, CourseTestData.CourseName(ordinal), state, students: [Person(ordinal)], updateTime: updateTime)
            .WithCourseWork(Id, CourseWorkTestData.ItemId(ordinal), CourseWorkResource.CourseWork, CourseWorkTestData.Title(ordinal), T)
            .WithSubmission(Id, CourseWorkTestData.ItemId(ordinal), CourseTestData.UserId(ordinal), CourseWorkTestData.SubmissionId(ordinal), CourseWorkTestData.RawStates.New);

    /// <summary>Run 2's answer: the same course, but another person, item and name — none of which a skip may write.</summary>
    private static SyncWorld.ClassroomReader ChangedReader(string state, DateTimeOffset? updateTime) =>
        Reader(state, updateTime, ordinal: 2);

    private static void AssertReadInFull(SyncWorld.ClassroomReader reader)
    {
        Assert.Contains(Id, reader.RostersRead);
        Assert.Contains(Id, reader.CourseWorkRead);
        Assert.Contains(Id, reader.SubmissionsRead);
    }

    private static void AssertNotRead(SyncWorld.ClassroomReader reader)
    {
        Assert.DoesNotContain(Id, reader.RostersRead);
        Assert.DoesNotContain(Id, reader.CourseWorkRead);
        Assert.DoesNotContain(Id, reader.SubmissionsRead);
    }

    /// <summary>Run 1: the first, full run reads and remembers the course; the clock then moves an hour on.</summary>
    private static async Task<SyncWorld> AfterFirstRunAsync(string state, DateTimeOffset? updateTime, CancellationToken ct)
    {
        var world = new SyncWorld();
        var first = await world.RunUsing(Reader(state, updateTime)).ExecuteAsync(SyncWorld.RunId(1), ct);
        Assert.False(first.Failed);
        world.Time.Advance(Later);
        return world;
    }

    // ---- AC-001 ----------------------------------------------------------------------------------------------------

    /// <summary>AC-001, FR-001: an archived course listed with the remembered updateTime gets no roster, coursework or submission read.</summary>
    [Fact]
    public async Task AnUnchangedArchivedCourse_IsNotRead()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await AfterFirstRunAsync(CourseTestData.States.Archived, T, ct);
        var second = ChangedReader(CourseTestData.States.Archived, T);

        var outcome = await world.RunUsing(second).ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.False(outcome.Failed);
        AssertNotRead(second);
        Assert.Equal(1, second.CourseReads);
    }

    /// <summary>
    /// AC-001, FR-001, FR-004: nothing of a skipped course is written — not its row, not a membership (none is marked
    /// off the roster, although run 2's roster does not list the stored student), not an item, not a submission.
    /// </summary>
    [Fact]
    public async Task AnUnchangedArchivedCourse_KeepsItsStoredDataExactly()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await AfterFirstRunAsync(CourseTestData.States.Archived, T, ct);
        var course = world.Courses.Stored[Id];
        var membership = Assert.Single(world.Memberships.OfCourse(course.Id));
        var lastSeen = membership.LastSeenAt;
        var item = Assert.Single(world.CourseWork.Stored.Values);
        var submission = Assert.Single(world.Submissions.Stored.Values);
        var remembered = course.CompleteReadUpdateTime;

        await world.RunUsing(ChangedReader(CourseTestData.States.Archived, T)).ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.Equal(CourseTestData.CourseName(1), course.Name);
        Assert.Equal(remembered, course.CompleteReadUpdateTime);
        Assert.Single(world.Courses.Stored);
        Assert.True(Assert.Single(world.Memberships.Stored).OnRoster);
        Assert.Equal(lastSeen, membership.LastSeenAt);
        Assert.Equal(CourseWorkTestData.Title(1), Assert.Single(world.CourseWork.Stored.Values).Title);
        Assert.Same(item, Assert.Single(world.CourseWork.Stored.Values));
        Assert.Same(submission, Assert.Single(world.Submissions.Stored.Values));
        Assert.Single(world.Participants.Stored);
    }

    /// <summary>AC-001, FR-008, I-1: a skipped course is counted as skipped, and not as processed.</summary>
    [Fact]
    public async Task AnUnchangedArchivedCourse_IsCountedAsSkipped_NotAsProcessed()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await AfterFirstRunAsync(CourseTestData.States.Archived, T, ct);

        var outcome = await world.RunUsing(ChangedReader(CourseTestData.States.Archived, T)).ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.Equal(1, outcome.CoursesSkippedUnchanged);
        Assert.Equal(0, outcome.ProcessedCount);
        Assert.Equal(0, world.States.Stored!.ProcessedCount);
    }

    /// <summary>
    /// AC-001, VR-001, I-4: Google may send a finer fraction than the database keeps; the listed value is compared at
    /// storage precision, so the same instant with sub-microsecond ticks still matches and the course is skipped.
    /// </summary>
    [Fact]
    public async Task AnUnchangedArchivedCourse_WithSubMicrosecondTicks_IsStillSkipped()
    {
        var ct = TestContext.Current.CancellationToken;
        var fine = T.AddTicks(7);
        var world = await AfterFirstRunAsync(CourseTestData.States.Archived, fine, ct);
        var second = ChangedReader(CourseTestData.States.Archived, fine);

        var outcome = await world.RunUsing(second).ExecuteAsync(SyncWorld.RunId(2), ct);

        AssertNotRead(second);
        Assert.Equal(1, outcome.CoursesSkippedUnchanged);
    }

    /// <summary>AC-001, FR-001: only the unchanged archived course is skipped; an active one beside it is read in the same run.</summary>
    [Fact]
    public async Task OnlyTheUnchangedArchivedCourse_IsSkipped_AmongOthers()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var other = CourseTestData.CourseId(2);
        await world.RunUsing(Reader(CourseTestData.States.Archived, T)
                .WithCourse(other, CourseTestData.CourseName(2), CourseTestData.States.Active, updateTime: T))
            .ExecuteAsync(SyncWorld.RunId(1), ct);
        world.Time.Advance(Later);
        var second = ChangedReader(CourseTestData.States.Archived, T)
            .WithCourse(other, CourseTestData.CourseName(2), CourseTestData.States.Active, updateTime: T);

        var outcome = await world.RunUsing(second).ExecuteAsync(SyncWorld.RunId(2), ct);

        AssertNotRead(second);
        Assert.Contains(other, second.RostersRead);
        Assert.Equal(1, outcome.CoursesSkippedUnchanged);
        Assert.Equal(1, outcome.ProcessedCount);
    }

    // ---- AC-002 ----------------------------------------------------------------------------------------------------

    /// <summary>AC-002, FR-001 (d), FR-002: an archived course whose updateTime changed is read in full and remembers the new value.</summary>
    [Fact]
    public async Task AChangedArchivedCourse_IsReadInFull_AndRemembersTheNewUpdateTime()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await AfterFirstRunAsync(CourseTestData.States.Archived, T, ct);
        var second = ChangedReader(CourseTestData.States.Archived, T2);

        var outcome = await world.RunUsing(second).ExecuteAsync(SyncWorld.RunId(2), ct);

        AssertReadInFull(second);
        Assert.Equal(0, outcome.CoursesSkippedUnchanged);
        Assert.Equal(1, outcome.ProcessedCount);
        Assert.Equal(T2, world.Courses.Stored[Id].CompleteReadUpdateTime);
    }

    /// <summary>AC-002, VR-001: a change of one microsecond is a change.</summary>
    [Fact]
    public async Task AnArchivedCourse_ChangedByOneMicrosecond_IsReadInFull()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await AfterFirstRunAsync(CourseTestData.States.Archived, T, ct);
        var second = ChangedReader(CourseTestData.States.Archived, T.AddTicks(10));

        await world.RunUsing(second).ExecuteAsync(SyncWorld.RunId(2), ct);

        AssertReadInFull(second);
    }

    /// <summary>
    /// AC-002, FR-001 (c), db-design §1: a course stored before this Story has no remembered value, so it is read in
    /// full even when archived — and the read then remembers its updateTime.
    /// </summary>
    [Fact]
    public async Task AnArchivedCourse_WithNoRememberedValue_IsReadInFull_AndThenRemembers()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await world.RunUsing(new SyncWorld.ClassroomReader()).ExecuteAsync(SyncWorld.RunId(1), ct);
        world.Courses.Add(Course.Import(
            Id,
            CourseState.Archived,
            new CourseDetails(CourseTestData.CourseName(1), null, null, null, null, null, null, T, null, null, null, null)));
        world.Time.Advance(Later);
        var second = ChangedReader(CourseTestData.States.Archived, T);

        var outcome = await world.RunUsing(second).ExecuteAsync(SyncWorld.RunId(2), ct);

        AssertReadInFull(second);
        Assert.Equal(0, outcome.CoursesSkippedUnchanged);
        Assert.Equal(T, world.Courses.Stored[Id].CompleteReadUpdateTime);
    }

    /// <summary>AC-002, FR-001 (b), FR-002, VR-001: Google sent no updateTime — nothing is remembered and nothing is ever equal to it.</summary>
    [Fact]
    public async Task AnArchivedCourse_ListedWithoutUpdateTime_IsReadInFullEveryRun()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await AfterFirstRunAsync(CourseTestData.States.Archived, null, ct);
        Assert.Null(world.Courses.Stored[Id].CompleteReadUpdateTime);
        var second = ChangedReader(CourseTestData.States.Archived, null);

        var outcome = await world.RunUsing(second).ExecuteAsync(SyncWorld.RunId(2), ct);

        AssertReadInFull(second);
        Assert.Equal(0, outcome.CoursesSkippedUnchanged);
    }

    /// <summary>
    /// AC-002, FR-002, I-4: the remembered value is kept at storage precision (whole microseconds), so the in-memory
    /// value and the one read back from the database agree.
    /// </summary>
    [Fact]
    public async Task TheRememberedValue_IsKeptAtStoragePrecision()
    {
        var ct = TestContext.Current.CancellationToken;
        var fine = T.AddTicks(7);

        var world = await AfterFirstRunAsync(CourseTestData.States.Active, fine, ct);

        Assert.Equal(T, world.Courses.Stored[Id].CompleteReadUpdateTime);
    }

    // ---- AC-003 ----------------------------------------------------------------------------------------------------

    /// <summary>
    /// AC-003, FR-001 (a), VR-003: a course in any state other than ARCHIVED is read in full in every run, even with
    /// an unchanged updateTime (a new grade does not change a course's updateTime).
    /// </summary>
    [Theory]
    [InlineData(CourseTestData.States.Active)]
    [InlineData(CourseTestData.States.Provisioned)]
    [InlineData(CourseTestData.States.Declined)]
    [InlineData(CourseTestData.States.Suspended)]
    public async Task ANonArchivedCourse_WithAnUnchangedUpdateTime_IsReadInFull(string state)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await AfterFirstRunAsync(state, T, ct);
        var second = ChangedReader(state, T);

        var outcome = await world.RunUsing(second).ExecuteAsync(SyncWorld.RunId(2), ct);

        AssertReadInFull(second);
        Assert.Equal(0, outcome.CoursesSkippedUnchanged);
        Assert.Equal(1, outcome.ProcessedCount);
        Assert.Equal(T, world.Courses.Stored[Id].CompleteReadUpdateTime);
    }

    /// <summary>
    /// AC-003, VR-003: the listed state decides, not the stored one — a course stored as archived but listed active
    /// with the remembered updateTime is read.
    /// </summary>
    [Fact]
    public async Task TheListedState_Decides_NotTheStoredOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await AfterFirstRunAsync(CourseTestData.States.Archived, T, ct);
        var second = ChangedReader(CourseTestData.States.Active, T);

        await world.RunUsing(second).ExecuteAsync(SyncWorld.RunId(2), ct);

        AssertReadInFull(second);
    }

    // ---- AC-004 ----------------------------------------------------------------------------------------------------

    /// <summary>AC-004: a skipped archived course restored to ACTIVE in Google is read in full by the next run.</summary>
    [Fact]
    public async Task ASkippedCourse_RestoredToActive_IsReadByTheNextRun()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await AfterFirstRunAsync(CourseTestData.States.Archived, T, ct);
        var skipping = ChangedReader(CourseTestData.States.Archived, T);
        await world.RunUsing(skipping).ExecuteAsync(SyncWorld.RunId(2), ct);
        AssertNotRead(skipping);
        world.Time.Advance(Later);
        var restored = ChangedReader(CourseTestData.States.Active, T2);

        var outcome = await world.RunUsing(restored).ExecuteAsync(SyncWorld.RunId(3), ct);

        AssertReadInFull(restored);
        Assert.Equal(0, outcome.CoursesSkippedUnchanged);
        Assert.Equal(CourseState.Active, world.Courses.Stored[Id].State);
        Assert.Equal(T2, world.Courses.Stored[Id].CompleteReadUpdateTime);
    }

    // ---- AC-005 ----------------------------------------------------------------------------------------------------

    public static TheoryData<string, GoogleReadFailureKind> Failures => new()
    {
        { "roster", GoogleReadFailureKind.CourseGone },
        { "coursework", GoogleReadFailureKind.CourseGone },
        { "submissions", GoogleReadFailureKind.CourseGone },
        { "roster", GoogleReadFailureKind.Transient },
        { "coursework", GoogleReadFailureKind.Transient },
        { "submissions", GoogleReadFailureKind.Transient },
        { "roster", GoogleReadFailureKind.Configuration },
        { "coursework", GoogleReadFailureKind.Configuration },
        { "submissions", GoogleReadFailureKind.Configuration },
    };

    private static GoogleReadFailedException FailureOf(GoogleReadFailureKind kind) =>
        new(
            kind,
            kind switch
            {
                GoogleReadFailureKind.CourseGone => null,
                GoogleReadFailureKind.Transient => SyncDiagnosis.GoogleUnavailable,
                _ => SyncDiagnosis.TechnicalAccountCannotRead,
            });

    private static SyncWorld.ClassroomReader FailingAt(SyncWorld.ClassroomReader reader, string read, Exception failure) =>
        read switch
        {
            "roster" => reader.WithRosterFailure(Id, failure),
            "coursework" => reader.WithCourseWorkFailure(Id, failure),
            _ => reader.WithSubmissionsFailure(Id, failure),
        };

    /// <summary>
    /// AC-005, FR-002, §8: a changed archived course whose read fails — gone mid-run, an exhausted transient failure,
    /// a permission failure, at any of its three reads — does not have its remembered value advanced, and the next
    /// run reads it in full.
    /// </summary>
    [Theory]
    [MemberData(nameof(Failures))]
    public async Task AFailedCourseRead_DoesNotAdvanceTheRememberedValue_AndTheNextRunReadsItInFull(
        string read,
        GoogleReadFailureKind kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await AfterFirstRunAsync(CourseTestData.States.Archived, T, ct);

        await world.RunUsing(FailingAt(ChangedReader(CourseTestData.States.Archived, T2), read, FailureOf(kind)))
            .ExecuteAsync(SyncWorld.RunId(2), ct);
        Assert.Equal(T, world.Courses.Stored[Id].CompleteReadUpdateTime);

        world.Time.Advance(Later);
        var third = ChangedReader(CourseTestData.States.Archived, T2);
        var outcome = await world.RunUsing(third).ExecuteAsync(SyncWorld.RunId(3), ct);

        AssertReadInFull(third);
        Assert.Equal(0, outcome.CoursesSkippedUnchanged);
        Assert.Equal(T2, world.Courses.Stored[Id].CompleteReadUpdateTime);
    }

    /// <summary>
    /// AC-005, FR-002: a course read for the first time whose read fails has nothing remembered, so the next run —
    /// listing it unchanged and archived — reads it in full.
    /// </summary>
    [Fact]
    public async Task ANewCourseWhoseReadFailed_IsReadInFullByTheNextRun()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await world.RunUsing(FailingAt(Reader(CourseTestData.States.Archived, T), "submissions", FailureOf(GoogleReadFailureKind.CourseGone)))
            .ExecuteAsync(SyncWorld.RunId(1), ct);
        world.Time.Advance(Later);
        var second = ChangedReader(CourseTestData.States.Archived, T);

        var outcome = await world.RunUsing(second).ExecuteAsync(SyncWorld.RunId(2), ct);

        AssertReadInFull(second);
        Assert.Equal(0, outcome.CoursesSkippedUnchanged);
        Assert.Equal(T, world.Courses.Stored[Id].CompleteReadUpdateTime);
    }

    // ---- AC-006 ----------------------------------------------------------------------------------------------------

    /// <summary>AC-006, FR-005, FR-006, I-3: the first run ever is the full run, and on success records its own start.</summary>
    [Fact]
    public async Task TheFirstRun_IsFull_AndRecordsItsStart()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var start = world.Time.GetUtcNow();

        await world.RunUsing(Reader(CourseTestData.States.Archived, T)).ExecuteAsync(SyncWorld.RunId(1), ct);

        var state = world.States.Stored!;
        Assert.Equal(start, state.LastFullRunAt);
        Assert.Equal(state.StartedAt, state.LastFullRunAt);
    }

    /// <summary>AC-006, FR-005, OD-001 a: exactly seven days after the last full run, the run is full and reads the unchanged archive.</summary>
    [Fact]
    public async Task SevenDaysAfterTheLastFullRun_TheRunIsFull_AndReadsTheUnchangedArchive()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var firstStart = world.Time.GetUtcNow();
        await world.RunUsing(Reader(CourseTestData.States.Archived, T)).ExecuteAsync(SyncWorld.RunId(1), ct);
        world.Time.Advance(IncrementalSyncRule.FullRunPeriod);
        var weekly = ChangedReader(CourseTestData.States.Archived, T);

        var outcome = await world.RunUsing(weekly).ExecuteAsync(SyncWorld.RunId(2), ct);

        AssertReadInFull(weekly);
        Assert.Equal(0, outcome.CoursesSkippedUnchanged);
        Assert.Equal(firstStart + IncrementalSyncRule.FullRunPeriod, world.States.Stored!.LastFullRunAt);
    }

    /// <summary>AC-006, FR-005: a tick short of seven days the run is not full, the archive is skipped and the full-run time stays.</summary>
    [Fact]
    public async Task JustUnderSevenDays_TheRunIsNotFull_AndLeavesTheFullRunTime()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var firstStart = world.Time.GetUtcNow();
        await world.RunUsing(Reader(CourseTestData.States.Archived, T)).ExecuteAsync(SyncWorld.RunId(1), ct);
        world.Time.Advance(IncrementalSyncRule.FullRunPeriod - TimeSpan.FromTicks(1));
        var second = ChangedReader(CourseTestData.States.Archived, T);

        var outcome = await world.RunUsing(second).ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.False(outcome.Failed);
        AssertNotRead(second);
        Assert.Equal(firstStart, world.States.Stored!.LastFullRunAt);
    }

    /// <summary>
    /// AC-006, FR-006, OD-002 a: a full run that fails records no full-run time, so the next run — an hour later — is
    /// full again and reads the unchanged archive.
    /// </summary>
    [Fact]
    public async Task AFailedFullRun_LeavesTheFullRunTime_AndTheNextRunIsFullAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var firstStart = world.Time.GetUtcNow();
        await world.RunUsing(Reader(CourseTestData.States.Archived, T)).ExecuteAsync(SyncWorld.RunId(1), ct);
        world.Time.Advance(IncrementalSyncRule.FullRunPeriod);
        var failing = ChangedReader(CourseTestData.States.Archived, T);
        failing.FailAfterCourses = FailureOf(GoogleReadFailureKind.Transient);

        var failed = await world.RunUsing(failing).ExecuteAsync(SyncWorld.RunId(2), ct);
        Assert.True(failed.Failed);
        AssertReadInFull(failing);
        Assert.Equal(firstStart, world.States.Stored!.LastFullRunAt);

        world.Time.Advance(Later);
        var retry = ChangedReader(CourseTestData.States.Archived, T);
        var outcome = await world.RunUsing(retry).ExecuteAsync(SyncWorld.RunId(3), ct);

        AssertReadInFull(retry);
        Assert.Equal(0, outcome.CoursesSkippedUnchanged);
        Assert.Equal(world.States.Stored!.StartedAt, world.States.Stored!.LastFullRunAt);
    }

    /// <summary>
    /// AC-006, FR-006: the very first run failing leaves no full-run time, so the next run is still full — and an
    /// archived course the failed run did commit is read again.
    /// </summary>
    [Fact]
    public async Task AFailedFirstRun_LeavesNoFullRunTime_AndTheNextRunIsFull()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var failing = Reader(CourseTestData.States.Archived, T);
        failing.FailAfterCourses = FailureOf(GoogleReadFailureKind.Transient);
        await world.RunUsing(failing).ExecuteAsync(SyncWorld.RunId(1), ct);
        Assert.Null(world.States.Stored!.LastFullRunAt);
        world.Time.Advance(Later);
        var second = ChangedReader(CourseTestData.States.Archived, T);

        await world.RunUsing(second).ExecuteAsync(SyncWorld.RunId(2), ct);

        AssertReadInFull(second);
        Assert.NotNull(world.States.Stored!.LastFullRunAt);
    }

    /// <summary>AC-006, FR-006: a successful run that is not full leaves the full-run time as it was.</summary>
    [Fact]
    public async Task ASuccessfulNonFullRun_LeavesTheFullRunTime()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var firstStart = world.Time.GetUtcNow();
        await world.RunUsing(Reader(CourseTestData.States.Active, T)).ExecuteAsync(SyncWorld.RunId(1), ct);
        world.Time.Advance(Later);

        var outcome = await world.RunUsing(ChangedReader(CourseTestData.States.Active, T)).ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.False(outcome.Failed);
        Assert.Equal(firstStart, world.States.Stored!.LastFullRunAt);
    }

    /// <summary>
    /// AC-006, VR-002, I-5: a stored full-run time later than the run's start (the clock moved back) makes the run full
    /// rather than postponing the safeguard.
    /// </summary>
    /// <remarks>
    /// Whether that run then completes is not US-018's to decide: US-031's watermark never moves back, so a run started
    /// before the stored Meet watermark stops at the Meet step. Either way the stored full-run time is not moved
    /// forward, so every run stays full until the clock catches up (Owner, IMPLEMENTATION, 2026-10-12).
    /// </remarks>
    [Fact]
    public async Task AFullRunTimeInTheFuture_MakesTheRunFull()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await world.RunUsing(Reader(CourseTestData.States.Archived, T)).ExecuteAsync(SyncWorld.RunId(1), ct);
        var storedFullRun = world.States.Stored!.LastFullRunAt;
        world.Time.Advance(-Later);
        var second = ChangedReader(CourseTestData.States.Archived, T);

        var outcome = await world.RunUsing(second).ExecuteAsync(SyncWorld.RunId(2), ct);

        AssertReadInFull(second);
        Assert.Equal(0, outcome.CoursesSkippedUnchanged);
        Assert.True(world.States.Stored!.LastFullRunAt <= storedFullRun);
    }

    /// <summary>AC-006, FR-008: in a full run the skipped count is zero, whatever the archive holds.</summary>
    [Fact]
    public async Task AFullRun_SkipsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await world.RunUsing(Reader(CourseTestData.States.Archived, T)).ExecuteAsync(SyncWorld.RunId(1), ct);
        world.Time.Advance(IncrementalSyncRule.FullRunPeriod + Later);

        var outcome = await world.RunUsing(ChangedReader(CourseTestData.States.Archived, T)).ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.Equal(0, outcome.CoursesSkippedUnchanged);
        Assert.Equal(1, outcome.ProcessedCount);
    }

    // ---- FR-004, FR-003: the checks that precede the skip, and the age rule -------------------------------------

    /// <summary>
    /// FR-004: the blank-name check runs before the skip rule and keeps its report — an unchanged archived course with
    /// a blank name is reported as blank-named, not counted as skipped-as-unchanged.
    /// </summary>
    [Fact]
    public async Task ABlankNamedUnchangedArchive_IsReportedAsBlank_NotAsSkippedUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await AfterFirstRunAsync(CourseTestData.States.Archived, T, ct);
        var second = new SyncWorld.ClassroomReader().WithCourse(Id, " ", CourseTestData.States.Archived, updateTime: T);

        var outcome = await world.RunUsing(second).ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.Contains(Id, outcome.CoursesWithBlankName);
        Assert.Equal(0, outcome.CoursesSkippedUnchanged);
    }

    /// <summary>FR-004: an unrecognised state is reported as before and never counts as skipped-as-unchanged.</summary>
    [Fact]
    public async Task AnUnrecognisedState_IsReportedAsBefore_NotAsSkippedUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await AfterFirstRunAsync(CourseTestData.States.Archived, T, ct);
        var second = new SyncWorld.ClassroomReader().WithCourse(Id, CourseTestData.CourseName(1), "STATE_ABCD", updateTime: T);

        var outcome = await world.RunUsing(second).ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.Contains(outcome.SkippedCourses, s => s.GoogleId == Id);
        Assert.Equal(0, outcome.CoursesSkippedUnchanged);
    }

    /// <summary>
    /// FR-003, §5 v36/v55: in a run that is not full, a new archived course older than the retention period is still
    /// refused by the age rule — it is never imported and not counted as skipped-as-unchanged.
    /// </summary>
    [Fact]
    public async Task ANewArchivedCourseOlderThanRetention_IsStillRefusedByTheAgeRule()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        await world.RunUsing(new SyncWorld.ClassroomReader()).ExecuteAsync(SyncWorld.RunId(1), ct);
        world.Time.Advance(Later);
        var old = world.Time.GetUtcNow().AddYears(-world.Retention.Years - 1);
        var second = new SyncWorld.ClassroomReader()
            .WithCourse(Id, CourseTestData.CourseName(1), CourseTestData.States.Archived, updateTime: old);

        var outcome = await world.RunUsing(second).ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.Contains(Id, outcome.CoursesSkippedByAge);
        Assert.False(world.Courses.Stored.ContainsKey(Id));
        Assert.Equal(0, outcome.CoursesSkippedUnchanged);
    }

    // ---- AC-008 (outcome side), I-2 --------------------------------------------------------------------------------

    /// <summary>
    /// AC-008, FR-009, I-2: a run that stops after skipping a course still reports the skip it reached, for the host's
    /// failure line.
    /// </summary>
    [Fact]
    public async Task AFailedRun_StillCarriesTheSkippedCountReachedBeforeTheStop()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await AfterFirstRunAsync(CourseTestData.States.Archived, T, ct);
        var second = ChangedReader(CourseTestData.States.Archived, T);
        second.FailAfterCourses = FailureOf(GoogleReadFailureKind.Transient);

        var outcome = await world.RunUsing(second).ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.True(outcome.Failed);
        Assert.Equal(1, outcome.CoursesSkippedUnchanged);
    }

    // ---- AC-009 ----------------------------------------------------------------------------------------------------

    /// <summary>
    /// AC-009, FR-010, BR-026: in read-only mode no run starts — the course list is not read, no remembered value is
    /// looked up, and the full-run time stays.
    /// </summary>
    [Fact]
    public async Task InReadOnlyMode_NoRunStarts_AndNothingOfTheRuleIsTouched()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await AfterFirstRunAsync(CourseTestData.States.Archived, T, ct);
        var fullRunAt = world.States.Stored!.LastFullRunAt;
        world.ReadOnly.IsReadOnly = true;
        world.Courses.CompleteReadLookups.Clear();
        var second = ChangedReader(CourseTestData.States.Archived, T2);

        var outcome = await world.RunUsing(second).ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.NotNull(outcome.ReadOnlyReason);
        Assert.Equal(0, second.CourseReads);
        Assert.Empty(world.Courses.CompleteReadLookups);
        Assert.Equal(fullRunAt, world.States.Stored!.LastFullRunAt);
        Assert.Equal(T, world.Courses.Stored[Id].CompleteReadUpdateTime);
    }
}
