using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-062 entity model §1: the domain pieces the confirmation and the empty-list date are built from — the course's
/// confirmed mark (FR-006.2 step 4, FR-006.3), the <c>SyncState</c> date (FR-008.1) and the confirmation audit rows
/// (FR-006.2 step 5, VR-001, api-design §2.5). Pure, no ports (the <c>CourseListPresenceInvariantTests</c> precedent).
/// </summary>
public sealed class DataHealthInvariantTests
{
    private static readonly DateTimeOffset T = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    private static Course NewCourse() =>
        Course.Import(
            CourseTestData.CourseId(1),
            CourseState.Active,
            new CourseDetails("Test Course 1", null, null, null, null, null, T, T, null, null, null, null));

    // ---- Course.ConfirmDeletedInClassroom (entity model §1.2) -------------------------------------------------------

    /// <summary>FR-006.2 step 4, AC-010: a course "not visible" becomes "not in Classroom" dated by the confirmation.</summary>
    [Fact]
    public void ConfirmingANotVisibleCourse_MarksItNotInClassroom_AtTheConfirmation()
    {
        var course = NewCourse();
        course.MarkAbsent(CourseAbsenceMark.NotVisibleToTechnicalAccount, T);

        var changed = course.ConfirmDeletedInClassroom(T.AddDays(2));

        Assert.True(changed);
        Assert.Equal(CourseAbsenceMark.NotInClassroom, course.AbsenceMark);
        Assert.Equal(T.AddDays(2), course.AbsentSince);
        Assert.Equal("Test Course 1", course.Name);
        Assert.Equal(CourseState.Active, course.State);
    }

    /// <summary>Entity model §1.2, VR-002: a course already "not in Classroom" keeps its mark and instant.</summary>
    [Fact]
    public void ConfirmingACourseNotInClassroom_ChangesNothing_AndReturnsFalse()
    {
        var course = NewCourse();
        course.MarkAbsent(CourseAbsenceMark.NotInClassroom, T);

        Assert.False(course.ConfirmDeletedInClassroom(T.AddDays(2)));
        Assert.Equal(CourseAbsenceMark.NotInClassroom, course.AbsenceMark);
        Assert.Equal(T, course.AbsentSince);
    }

    /// <summary>Entity model §1.2, VR-002: an unmarked course stays unmarked.</summary>
    [Fact]
    public void ConfirmingAnUnmarkedCourse_ChangesNothing_AndReturnsFalse()
    {
        var course = NewCourse();

        Assert.False(course.ConfirmDeletedInClassroom(T.AddDays(2)));
        Assert.Null(course.AbsenceMark);
        Assert.Null(course.AbsentSince);
    }

    /// <summary>
    /// FR-006.3, BR-048: after the confirmation the course follows "not in Classroom" — a later absence keeps the
    /// confirmation's mark and date, and the course listed again loses it.
    /// </summary>
    [Fact]
    public void AConfirmedCourse_KeepsItsMarkWhileAbsent_AndLosesItWhenListed()
    {
        var course = NewCourse();
        course.MarkAbsent(CourseAbsenceMark.NotVisibleToTechnicalAccount, T);
        course.ConfirmDeletedInClassroom(T.AddDays(2));

        Assert.False(course.MarkAbsent(CourseAbsenceMark.NotInClassroom, T.AddDays(3)));
        Assert.Equal(T.AddDays(2), course.AbsentSince);

        Assert.True(course.RecordListed(T.AddDays(4)));
        Assert.Null(course.AbsenceMark);
        Assert.Null(course.AbsentSince);
    }

    // ---- SyncState.CourseListEmptySince (entity model §1.1) ---------------------------------------------------------

    /// <summary>FR-008.1: a new state has no date.</summary>
    [Fact]
    public void ANewState_HasNoEmptyListDate()
    {
        Assert.Null(SyncState.BeginFirstRun(Guid.NewGuid(), T).CourseListEmptySince);
    }

    /// <summary>FR-008.1, I-11: the first empty list of a streak sets the flag and dates it with its run's start.</summary>
    [Fact]
    public void AnEmptyList_SetsTheDateToTheRunsStart()
    {
        var state = SyncState.BeginFirstRun(Guid.NewGuid(), T);

        state.RecordCourseListRead(true);

        Assert.True(state.LastCourseListEmpty);
        Assert.Equal(T, state.CourseListEmptySince);
    }

    /// <summary>AC-008, AC-016, FR-008.1: later empty runs of the streak keep the first run's date.</summary>
    [Fact]
    public void LaterEmptyLists_KeepTheFirstDate()
    {
        var state = SyncState.BeginFirstRun(Guid.NewGuid(), T);
        state.RecordCourseListRead(true);
        state.CompleteRun(T.AddMinutes(1), 0, T);

        state.BeginRun(Guid.NewGuid(), T.AddHours(1));
        state.RecordCourseListRead(true);
        state.CompleteRun(T.AddHours(1).AddMinutes(1), 0, T.AddHours(1));
        state.BeginRun(Guid.NewGuid(), T.AddHours(2));
        state.RecordCourseListRead(true);

        Assert.Equal(T, state.CourseListEmptySince);
        Assert.True(state.LastCourseListEmpty);
    }

    /// <summary>AC-008, AC-016, FR-008.1: a non-empty list clears flag and date together; the next streak dates anew.</summary>
    [Fact]
    public void ANonEmptyList_ClearsBoth_AndTheNextStreakStartsAgain()
    {
        var state = SyncState.BeginFirstRun(Guid.NewGuid(), T);
        state.RecordCourseListRead(true);

        state.BeginRun(Guid.NewGuid(), T.AddHours(1));
        state.RecordCourseListRead(false);
        Assert.False(state.LastCourseListEmpty);
        Assert.Null(state.CourseListEmptySince);

        state.BeginRun(Guid.NewGuid(), T.AddHours(2));
        state.RecordCourseListRead(true);
        Assert.Equal(T.AddHours(2), state.CourseListEmptySince);
    }

    /// <summary>FR-008.1, db-design §2: beginning, completing and failing a run leave the date (a failed run keeps it).</summary>
    [Fact]
    public void BeginningCompletingAndFailingARun_LeaveTheDate()
    {
        var state = SyncState.BeginFirstRun(Guid.NewGuid(), T);
        state.RecordCourseListRead(true);

        state.CompleteRun(T.AddMinutes(1), 0, T);
        state.BeginRun(Guid.NewGuid(), T.AddHours(1));
        state.FailRun(T.AddHours(1).AddMinutes(1), 0, SyncDiagnosis.GoogleUnavailable, SyncStep.Classroom);

        Assert.Equal(T, state.CourseListEmptySince);
        Assert.True(state.LastCourseListEmpty);
    }

    // ---- AuditEvent (entity model §1.4) -------------------------------------------------------------------------

    /// <summary>FR-006.2 step 5, db-design §3: the succeeded row — the Admin, no target, the ids ascending and distinct.</summary>
    [Fact]
    public void TheSucceededRow_CarriesTheAdmin_NoTarget_AndTheIdsSortedAndDistinct()
    {
        var row = AuditEvent.CoursesConfirmedDeleted(7, [30L, 10L, 30L, 20L], T, "request-1");

        Assert.Equal(AuditAction.CoursesConfirmedDeleted, row.Action);
        Assert.Equal(AuditActorType.AppUser, row.ActorType);
        Assert.Equal(7, row.ActorId);
        Assert.Equal(AppRole.Admin, row.ActorRole);
        Assert.Null(row.TargetType);
        Assert.Null(row.TargetId);
        Assert.Equal(AuditOutcome.Succeeded, row.Outcome);
        Assert.Null(row.RefusalCategory);
        Assert.Equal("request-1", row.RequestId);
        Assert.Equal(T, row.OccurredAt);
        Assert.Equal([10L, 20L, 30L], Assert.IsType<long[]>(row.ConfirmedCourseIds));
    }

    /// <summary>VR-001, I-6: one and exactly 1000 distinct ids are accepted.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    public void TheSucceededRow_AcceptsOneTo1000Ids(int count)
    {
        var ids = Enumerable.Range(1, count).Select(i => (long)i).ToList();

        var row = AuditEvent.CoursesConfirmedDeleted(7, ids, T, null);

        Assert.Equal(count, row.ConfirmedCourseIds!.Length);
    }

    /// <summary>Entity model §1.4: no id or more than 1000 distinct ids is refused.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void TheSucceededRow_RefusesAnEmptyOrOversizedSet(int count)
    {
        var ids = Enumerable.Range(1, count).Select(i => (long)i).ToList();

        Assert.Throws<ArgumentOutOfRangeException>(() => AuditEvent.CoursesConfirmedDeleted(7, ids, T, null));
    }

    /// <summary>Entity model §1.4: a non-positive id is refused.</summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(-5L)]
    public void TheSucceededRow_RefusesANonPositiveId(long id)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AuditEvent.CoursesConfirmedDeleted(7, [3L, id], T, null));
    }

    /// <summary>Api-design §2.5, db-design §3.2: the refused row — the Admin, read-only mode, no target and no ids.</summary>
    [Fact]
    public void TheRefusedRow_CarriesTheAdmin_ReadOnlyMode_AndNoIds()
    {
        var row = AuditEvent.CoursesConfirmedDeletedRefused(7, AuditRefusalCategory.ReadOnlyMode, T, "request-2");

        Assert.Equal(AuditAction.CoursesConfirmedDeleted, row.Action);
        Assert.Equal(7, row.ActorId);
        Assert.Equal(AppRole.Admin, row.ActorRole);
        Assert.Equal(AuditOutcome.Refused, row.Outcome);
        Assert.Equal(AuditRefusalCategory.ReadOnlyMode, row.RefusalCategory);
        Assert.Null(row.TargetType);
        Assert.Null(row.TargetId);
        Assert.Null(row.ConfirmedCourseIds);
    }

    /// <summary>Entity model §1.4: "category ReadOnlyMode only" — any other refusal category is refused.</summary>
    [Fact]
    public void TheRefusedRow_RefusesAnotherCategory()
    {
        Assert.ThrowsAny<ArgumentException>(
            () => AuditEvent.CoursesConfirmedDeletedRefused(7, AuditRefusalCategory.WrongPassword, T, null));
    }
}
