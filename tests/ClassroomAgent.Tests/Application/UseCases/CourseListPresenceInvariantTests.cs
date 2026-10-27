using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-059 entity model §1 … §5, spec VR-002 … VR-006, FR-002, FR-005, FR-006: the domain pieces the run and the save
/// are built from — the course's mark and sighting, the BR-049 choice of mark, the <c>SyncState</c> empty-list flag and
/// the connection's account-change instant. Pure, no ports (the <c>SyncDataHealthInvariantTests</c> precedent).
/// </summary>
public sealed class CourseListPresenceInvariantTests
{
    private static readonly DateTimeOffset T = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    private static Course NewCourse(string name = "Test Course 1") =>
        Course.Import(
            CourseTestData.CourseId(1),
            CourseState.Active,
            new CourseDetails(name, null, null, null, null, null, T, T, null, null, null, null));

    // ---- Course (entity model §2) ------------------------------------------------------------------------------------

    /// <summary>FR-003.3, I-1: a course imported (or stored before the migration) has no mark and no sighting.</summary>
    [Fact]
    public void AnImportedCourse_HasNoMarkAndNoSighting()
    {
        var course = NewCourse();

        Assert.Null(course.AbsenceMark);
        Assert.Null(course.AbsentSince);
        Assert.Null(course.LastListedAt);
    }

    /// <summary>FR-001.2, FR-001.6, VR-003: marking sets the kind and the instant together, says it is new, and leaves the mirror.</summary>
    [Theory]
    [InlineData(CourseAbsenceMark.NotInClassroom)]
    [InlineData(CourseAbsenceMark.NotVisibleToTechnicalAccount)]
    public void MarkingAbsent_SetsKindAndInstant_ReturnsTrue_AndLeavesTheMirror(CourseAbsenceMark mark)
    {
        var course = NewCourse();

        var isNew = course.MarkAbsent(mark, T.AddHours(1));

        Assert.True(isNew);
        Assert.Equal(mark, course.AbsenceMark);
        Assert.Equal(T.AddHours(1), course.AbsentSince);
        Assert.Equal("Test Course 1", course.Name);
        Assert.Equal(CourseState.Active, course.State);
        Assert.Equal(T, course.UpdateTime);
        Assert.Null(course.LastListedAt);
    }

    /// <summary>FR-006.5: a marked course keeps the kind and instant it has — whichever kind is asked for later — and the call says so.</summary>
    [Theory]
    [InlineData(CourseAbsenceMark.NotInClassroom, CourseAbsenceMark.NotVisibleToTechnicalAccount)]
    [InlineData(CourseAbsenceMark.NotVisibleToTechnicalAccount, CourseAbsenceMark.NotInClassroom)]
    [InlineData(CourseAbsenceMark.NotInClassroom, CourseAbsenceMark.NotInClassroom)]
    public void MarkingAgain_KeepsTheFirstKindAndInstant_AndReturnsFalse(CourseAbsenceMark first, CourseAbsenceMark later)
    {
        var course = NewCourse();
        course.MarkAbsent(first, T.AddHours(1));

        var isNew = course.MarkAbsent(later, T.AddHours(2));

        Assert.False(isNew);
        Assert.Equal(first, course.AbsenceMark);
        Assert.Equal(T.AddHours(1), course.AbsentSince);
    }

    /// <summary>VR-003: a kind outside the closed set is refused, and the course stays unmarked.</summary>
    [Fact]
    public void AnUndefinedMark_IsRefused()
    {
        var course = NewCourse();

        Assert.Throws<ArgumentOutOfRangeException>(() => course.MarkAbsent((CourseAbsenceMark)99, T));
        Assert.Null(course.AbsenceMark);
        Assert.Null(course.AbsentSince);
    }

    /// <summary>FR-003.1, FR-004.1: being listed records the sighting, removes the mark — kind and instant — and says a mark went.</summary>
    [Fact]
    public void RecordingListed_StoresTheSighting_RemovesTheMark_AndReturnsTrue()
    {
        var course = NewCourse();
        course.MarkAbsent(CourseAbsenceMark.NotVisibleToTechnicalAccount, T.AddHours(1));

        var unmarked = course.RecordListed(T.AddHours(2));

        Assert.True(unmarked);
        Assert.Null(course.AbsenceMark);
        Assert.Null(course.AbsentSince);
        Assert.Equal(T.AddHours(2), course.LastListedAt);
    }

    /// <summary>FR-003.1, FR-007 (I-3): an unmarked listed course records the later sighting and counts as no unmark.</summary>
    [Fact]
    public void RecordingListed_OnAnUnmarkedCourse_TakesTheLaterSighting_AndReturnsFalse()
    {
        var course = NewCourse();
        course.RecordListed(T.AddHours(1));

        var unmarked = course.RecordListed(T.AddHours(2));

        Assert.False(unmarked);
        Assert.Equal(T.AddHours(2), course.LastListedAt);
    }

    /// <summary>
    /// Entity model §2, spec I-7: a mirror update from Google touches neither the mark nor the sighting — both belong to
    /// the list, not to a read.
    /// </summary>
    [Fact]
    public void UpdatingFromGoogle_LeavesTheMarkAndTheSighting()
    {
        var course = NewCourse();
        course.RecordListed(T.AddHours(1));
        course.MarkAbsent(CourseAbsenceMark.NotInClassroom, T.AddHours(2));

        course.UpdateFrom(
            CourseState.Archived,
            new CourseDetails("Renamed", null, null, null, null, null, T, T.AddHours(3), null, null, null, null));

        Assert.Equal(CourseAbsenceMark.NotInClassroom, course.AbsenceMark);
        Assert.Equal(T.AddHours(2), course.AbsentSince);
        Assert.Equal(T.AddHours(1), course.LastListedAt);
        Assert.Equal("Renamed", course.Name);
    }

    // ---- CourseAbsenceRule (entity model §3) -------------------------------------------------------------------------

    /// <summary>Last sighting and change instant as offsets in hours from T; null = none.</summary>
    public static TheoryData<int?, int?, CourseAbsenceMark> Marks => new()
    {
        // FR-006.2: no change recorded — always "not in Classroom", seen or not.
        { null, null, CourseAbsenceMark.NotInClassroom },
        { 0, null, CourseAbsenceMark.NotInClassroom },

        // FR-006.3, I-1: never seen, a change recorded — counts as seen before it.
        { null, 5, CourseAbsenceMark.NotVisibleToTechnicalAccount },

        // FR-006.1: seen strictly before the change.
        { 4, 5, CourseAbsenceMark.NotVisibleToTechnicalAccount },

        // FR-006.1 is strict, FR-006.2: seen at or after the change — the new account saw it.
        { 5, 5, CourseAbsenceMark.NotInClassroom },
        { 6, 5, CourseAbsenceMark.NotInClassroom },
    };

    /// <summary>FR-006.1 … FR-006.3, BR-049: the mark a newly absent course gets.</summary>
    [Theory]
    [MemberData(nameof(Marks))]
    public void TheRule_ChoosesTheMark_FromTheSightingAndTheChange(int? listedHour, int? changedHour, CourseAbsenceMark expected)
    {
        DateTimeOffset? listed = listedHour is { } l ? T.AddHours(l) : null;
        DateTimeOffset? changed = changedHour is { } c ? T.AddHours(c) : null;

        Assert.Equal(expected, CourseAbsenceRule.MarkFor(listed, changed));
    }

    /// <summary>FR-006.1: one tick before the change is still before it.</summary>
    [Fact]
    public void TheRule_SeesOneTickBeforeTheChange_AsBefore()
    {
        Assert.Equal(
            CourseAbsenceMark.NotVisibleToTechnicalAccount,
            CourseAbsenceRule.MarkFor(T.AddTicks(-1), T));
    }

    // ---- SyncState (entity model §4) ---------------------------------------------------------------------------------

    /// <summary>VR-006, db-design §4: a new state holds no empty-list flag.</summary>
    [Fact]
    public void ANewState_HasNoEmptyListFlag()
    {
        Assert.False(SyncState.BeginFirstRun(Guid.NewGuid(), T).LastCourseListEmpty);
    }

    /// <summary>FR-005.2, FR-005.3: an empty list while courses are stored sets the flag; the next judged list clears it.</summary>
    [Fact]
    public void RecordingAnEmptyList_SetsTheFlag_AndANonEmptyOne_ClearsIt()
    {
        var state = SyncState.BeginFirstRun(Guid.NewGuid(), T);

        state.RecordCourseListRead(true);
        Assert.True(state.LastCourseListEmpty);

        state.RecordCourseListRead(false);
        Assert.False(state.LastCourseListEmpty);
    }

    /// <summary>FR-005.4: beginning, completing and failing a run leave the flag of the last list read to its end.</summary>
    [Fact]
    public void BeginningCompletingAndFailingARun_LeaveTheFlag()
    {
        var state = SyncState.BeginFirstRun(Guid.NewGuid(), T);
        state.RecordCourseListRead(true);

        state.CompleteRun(T.AddMinutes(1), 0, T);
        state.BeginRun(Guid.NewGuid(), T.AddHours(1));
        state.FailRun(T.AddHours(1).AddMinutes(1), 0, SyncDiagnosis.TechnicalAccountCannotRead, SyncStep.Classroom);

        Assert.True(state.LastCourseListEmpty);
    }

    // ---- WorkspaceConnection (entity model §5) -----------------------------------------------------------------------

    private static WorkspaceConnection Saved() =>
        WorkspaceConnection.Create(InstallationTestData.Domain, WorkspaceConnectionTestData.TechnicalAccount);

    /// <summary>FR-002.2: the first save records no change.</summary>
    [Fact]
    public void ACreatedConnection_HasNoChangeInstant()
    {
        Assert.Null(Saved().TechnicalAccountChangedAt);
    }

    /// <summary>FR-002.1, VR-002: a change to another address records the save's instant, and the address is changed.</summary>
    [Fact]
    public void ChangingToAnotherAccount_RecordsTheInstant()
    {
        var connection = Saved();

        connection.ChangeTo(InstallationTestData.Domain, WorkspaceConnectionTestData.OtherTechnicalAccount, T);

        Assert.Equal(T, connection.TechnicalAccountChangedAt);
        Assert.Equal(WorkspaceConnectionTestData.OtherTechnicalAccount, connection.ImpersonationUserEmail);
    }

    /// <summary>
    /// FR-002.2, VR-002: the same address in other letter case and with surrounding spaces is no change — the previous
    /// instant stays (here: none, then the one a real change recorded).
    /// </summary>
    [Fact]
    public void ChangingToTheSameAccount_InOtherCaseOrWithSpaces_KeepsThePreviousInstant()
    {
        var connection = Saved();

        connection.ChangeTo(InstallationTestData.Domain, WorkspaceConnectionTestData.TechnicalAccountMixedCase, T);
        Assert.Null(connection.TechnicalAccountChangedAt);

        connection.ChangeTo(InstallationTestData.Domain, WorkspaceConnectionTestData.OtherTechnicalAccount, T.AddHours(1));
        connection.ChangeTo(InstallationTestData.Domain, "  " + WorkspaceConnectionTestData.OtherTechnicalAccount.ToUpperInvariant() + " ", T.AddHours(2));
        Assert.Equal(T.AddHours(1), connection.TechnicalAccountChangedAt);
    }

    /// <summary>FR-002.1: every change of the address moves the instant to that save — it is the last change.</summary>
    [Fact]
    public void ChangingTheAccountTwice_KeepsTheLatestInstant()
    {
        var connection = Saved();
        connection.ChangeTo(InstallationTestData.Domain, WorkspaceConnectionTestData.OtherTechnicalAccount, T);

        connection.ChangeTo(InstallationTestData.Domain, WorkspaceConnectionTestData.TechnicalAccount, T.AddHours(1));

        Assert.Equal(T.AddHours(1), connection.TechnicalAccountChangedAt);
    }

    /// <summary>FR-002.4, US-009 FR-006: the instant-taking change still refuses an address outside the bound domain, and records nothing.</summary>
    [Fact]
    public void ChangingToAnAccountOutsideTheDomain_IsStillRefused()
    {
        var connection = Saved();

        Assert.Throws<ArgumentException>(
            () => connection.ChangeTo(InstallationTestData.Domain, "agent@" + InstallationTestData.OtherDomain, T));
        Assert.Null(connection.TechnicalAccountChangedAt);
        Assert.Equal(WorkspaceConnectionTestData.TechnicalAccount, connection.ImpersonationUserEmail);
    }
}
