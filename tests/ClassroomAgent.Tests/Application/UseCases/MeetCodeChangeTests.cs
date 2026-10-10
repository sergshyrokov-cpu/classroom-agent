using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.MeetCodeWorld;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-032 spec FR-008 … FR-015, VR-001 … VR-003, AC-008 … AC-012, AC-014, AC-015; api-design §2.3 … §2.8: the three
/// writes in the Application layer — what each changes, the audit row it writes, the expected-state check, the
/// evaluation order with the read-only guard first, and the refusals that write nothing.
/// </summary>
public sealed class MeetCodeChangeTests
{
    private const string Code = "abc-0001-xyz";

    private const string RequestId = "r-1";

    private static readonly DateTimeOffset Earlier = InstallationTestHost.DefaultStart - TimeSpan.FromDays(3);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Id(long id) => id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static Task<MeetCodeChangeResult> SetAsync(MeetCodeWorld world, string? code, params (string, string?)[] form) =>
        world.SetCourse.ExecuteAsync(DeanId, AppRole.Dean, code, Form(form), RequestId, Ct);

    private static Task<MeetCodeChangeResult> ConfirmAsync(MeetCodeWorld world, string? code, params (string, string?)[] form) =>
        world.Confirm.ExecuteAsync(DeanId, AppRole.Dean, code, Form(form), RequestId, Ct);

    private static Task<MeetCodeChangeResult> MarkAsync(MeetCodeWorld world, string? code, params (string, string?)[] form) =>
        world.Mark.ExecuteAsync(DeanId, AppRole.Dean, code, Form(form), RequestId, Ct);

    private static MeetCodeWorld WithAutomaticLink(long course = CourseA)
    {
        var world = new MeetCodeWorld();
        world.Links.Seed(MeetingCodeLink.LinkAutomatically(Code, course, Earlier));
        return world;
    }

    private static MeetCodeWorld WithUnassigned()
    {
        var world = new MeetCodeWorld();
        world.Links.SeedUnassigned(Code);
        return world;
    }

    private static MeetCodeWorld WithMark()
    {
        var world = new MeetCodeWorld();
        world.Links.Seed(MeetingCodeLink.MarkNotACourse(Code, AdminId, Earlier));
        return world;
    }

    private static void AssertNothingWritten(MeetCodeWorld world)
    {
        Assert.Equal(0, world.Work.Commits);
        Assert.Empty(world.Links.Added);
        Assert.Empty(world.Audit.Written);
    }

    private static AuditEvent SingleAuditRow(MeetCodeWorld world)
    {
        var row = Assert.Single(world.Audit.Written);
        Assert.Equal([1], world.Work.AuditRowsAtCommit);
        return row;
    }

    // ---------------- Set the course (FR-008, FR-010, FR-012) ----------------

    /// <summary>AC-008: picking any stored course for an unassigned code links it as the person's unconfirmed link.</summary>
    [Fact]
    public async Task PickingACourseForAnUnassignedCode_LinksIt_AndAuditsIt()
    {
        var world = WithUnassigned();

        var result = await SetAsync(world, Code, ("courseId", Id(CourseB)), ("expectedState", "unassigned"), ("returnPage", "3"));

        Assert.Equal(new MeetCodeChangeResult(MeetCodeChangeOutcome.CoursePicked, MeetCodeList.Unassigned, 3), result);
        var link = world.Links.Committed[Code];
        Assert.Equal(((long?)CourseB, (bool?)false, (long?)DeanId, (DateTimeOffset?)world.Time.GetUtcNow()), (link.CourseId, link.LinkedAutomatically, link.LinkedByAppUserId, link.LinkedAt));
        Assert.Null(link.ConfirmedAt);
        var row = SingleAuditRow(world);
        Assert.Equal((AuditAction.MeetCodeCoursePicked, Code, (long?)CourseB, (long?)null), (row.Action, row.MeetCode, row.TargetId, row.MeetPreviousCourseId));
        Assert.Equal((DeanId, (AppRole?)AppRole.Dean, RequestId), (row.ActorId!.Value, row.ActorRole, row.RequestId));
        Assert.Equal(1, world.Work.Transactions);
    }

    /// <summary>AC-010: re-linking A → B moves the code; the audit row names both courses.</summary>
    [Fact]
    public async Task Relinking_MovesTheCode_AndAuditsOldAndNew()
    {
        var world = WithAutomaticLink(CourseA);

        var result = await SetAsync(world, Code, ("courseId", Id(CourseB)), ("expectedState", "linked"), ("expectedCourseId", Id(CourseA)));

        Assert.Equal(new MeetCodeChangeResult(MeetCodeChangeOutcome.Relinked, MeetCodeList.Linked, 0), result);
        Assert.Equal(CourseB, world.Links.Committed[Code].CourseId);
        Assert.Equal(DeanId, world.Links.Committed[Code].LinkedByAppUserId);
        var row = SingleAuditRow(world);
        Assert.Equal((AuditAction.MeetCodeRelinked, (long?)CourseB, (long?)CourseA), (row.Action, row.TargetId, row.MeetPreviousCourseId));
    }

    /// <summary>AC-012: picking a course for a marked code removes the mark.</summary>
    [Fact]
    public async Task PickingACourseForAMarkedCode_RemovesTheMark()
    {
        var world = WithMark();

        var result = await SetAsync(world, Code, ("courseId", Id(CourseC)), ("expectedState", "marked"));

        Assert.Equal(new MeetCodeChangeResult(MeetCodeChangeOutcome.MarkRemoved, MeetCodeList.NotACourse, 0), result);
        var link = world.Links.Committed[Code];
        Assert.Equal(MeetingCodeLinkState.Linked, link.State);
        Assert.Equal(CourseC, link.CourseId);
        var row = SingleAuditRow(world);
        Assert.Equal((AuditAction.MeetCodeMarkRemoved, (long?)CourseC, (long?)null), (row.Action, row.TargetId, row.MeetPreviousCourseId));
    }

    /// <summary>A link whose meetings have all expired is still found by its row.</summary>
    [Fact]
    public async Task ALinkedCodeWithNoMeetingLeft_CanStillBeRelinked()
    {
        var world = new MeetCodeWorld();
        world.Links.Seed(MeetingCodeLink.LinkAutomatically(Code, CourseA, Earlier), hasMeetings: false);

        var result = await SetAsync(world, Code, ("courseId", Id(CourseB)), ("expectedState", "linked"), ("expectedCourseId", Id(CourseA)));

        Assert.Equal(MeetCodeChangeOutcome.Relinked, result.Outcome);
    }

    /// <summary>The Admin acts like the Dean; the role is recorded.</summary>
    [Fact]
    public async Task AnAdmin_CanPick_AndTheRoleIsRecorded()
    {
        var world = WithUnassigned();

        var result = await world.SetCourse.ExecuteAsync(
            AdminId, AppRole.Admin, Code, Form(("courseId", Id(CourseA)), ("expectedState", "unassigned")), RequestId, Ct);

        Assert.Equal(MeetCodeChangeOutcome.CoursePicked, result.Outcome);
        Assert.Equal((AdminId, (AppRole?)AppRole.Admin), (SingleAuditRow(world).ActorId!.Value, SingleAuditRow(world).ActorRole));
    }

    // ---------------- Stale state (FR-013) ----------------

    /// <summary>The run linked the code after the page was shown: nothing changes, no audit row, the code's current list.</summary>
    [Fact]
    public async Task PickingForACodeThatIsNoLongerUnassigned_IsStale()
    {
        var world = WithAutomaticLink(CourseA);

        var result = await SetAsync(world, Code, ("courseId", Id(CourseB)), ("expectedState", "unassigned"));

        Assert.Equal(new MeetCodeChangeResult(MeetCodeChangeOutcome.StateChanged, MeetCodeList.Linked, 0), result);
        AssertNothingWritten(world);
        Assert.Equal(CourseA, world.Links.Committed[Code].CourseId);
    }

    [Fact]
    public async Task RelinkingWhenTheLinkNamesAnotherCourse_IsStale()
    {
        var world = WithAutomaticLink(CourseC);

        var result = await SetAsync(world, Code, ("courseId", Id(CourseB)), ("expectedState", "linked"), ("expectedCourseId", Id(CourseA)));

        Assert.Equal(new MeetCodeChangeResult(MeetCodeChangeOutcome.StateChanged, MeetCodeList.Linked, 0), result);
        AssertNothingWritten(world);
    }

    [Fact]
    public async Task RemovingAMarkThatIsGone_IsStale_AndShowsTheUnassignedList()
    {
        var world = WithUnassigned();

        var result = await SetAsync(world, Code, ("courseId", Id(CourseB)), ("expectedState", "marked"));

        Assert.Equal(new MeetCodeChangeResult(MeetCodeChangeOutcome.StateChanged, MeetCodeList.Unassigned, 0), result);
        AssertNothingWritten(world);
    }

    /// <summary>db-design §7: a concurrent change detected at save time is the same stale state — never an error.</summary>
    [Fact]
    public async Task AConcurrentChangeAtSave_IsStale_AndCommitsNothing()
    {
        var world = WithUnassigned();
        world.Work.FailNextWithConflict = true;

        var result = await SetAsync(world, Code, ("courseId", Id(CourseB)), ("expectedState", "unassigned"));

        Assert.Equal(MeetCodeChangeOutcome.StateChanged, result.Outcome);
        Assert.Equal(0, world.Work.Commits);
        Assert.False(world.Links.Committed.ContainsKey(Code));
    }

    // ---------------- Not found, same course (VR-001, VR-002) ----------------

    [Fact]
    public async Task AnUnknownCode_IsNotFound()
    {
        var world = new MeetCodeWorld();

        var result = await SetAsync(world, Code, ("courseId", Id(CourseB)), ("expectedState", "unassigned"));

        Assert.Equal(MeetCodeChangeOutcome.CodeNotFound, result.Outcome);
        AssertNothingWritten(world);
    }

    [Fact]
    public async Task AnUnknownCourse_IsNotFound()
    {
        var world = WithUnassigned();
        world.Links.RemoveCourse(CourseB);

        var result = await SetAsync(world, Code, ("courseId", Id(CourseB)), ("expectedState", "unassigned"));

        Assert.Equal(MeetCodeChangeOutcome.CourseNotFound, result.Outcome);
        AssertNothingWritten(world);
    }

    [Fact]
    public async Task RelinkingToTheCurrentCourse_IsSameCourse()
    {
        var world = WithAutomaticLink(CourseA);

        var result = await SetAsync(world, Code, ("courseId", Id(CourseA)), ("expectedState", "linked"), ("expectedCourseId", Id(CourseA)));

        Assert.Equal(MeetCodeChangeOutcome.SameCourse, result.Outcome);
        AssertNothingWritten(world);
    }

    // ---------------- Malformed input (VR-001, VR-003, api-design §2.8) ----------------

    public static TheoryData<string?, (string, string?)[]> MalformedSets => new()
    {
        { Code, [("expectedState", "unassigned")] },
        { Code, [("courseId", "abc"), ("expectedState", "unassigned")] },
        { Code, [("courseId", "0"), ("expectedState", "unassigned")] },
        { Code, [("courseId", "-5"), ("expectedState", "unassigned")] },
        { Code, [("courseId", " 7"), ("expectedState", "unassigned")] },
        { Code, [("courseId", "99999999999999999999"), ("expectedState", "unassigned")] },
        { Code, [("courseId", "101")] },
        { Code, [("courseId", "101"), ("expectedState", "Unassigned")] },
        { Code, [("courseId", "101"), ("expectedState", "assigned")] },
        { Code, [("courseId", "101"), ("expectedState", "linked")] },
        { Code, [("courseId", "101"), ("expectedState", "unassigned"), ("expectedCourseId", "102")] },
        { Code, [("courseId", "101"), ("expectedState", "marked"), ("expectedCourseId", "102")] },
        { Code, [("courseId", "101"), ("courseId", "102"), ("expectedState", "unassigned")] },
        { Code, [("courseId", "101"), ("expectedState", "unassigned"), ("expectedState", "unassigned")] },
        { null, [("courseId", "101"), ("expectedState", "unassigned")] },
        { "", [("courseId", "101"), ("expectedState", "unassigned")] },
        { "   ", [("courseId", "101"), ("expectedState", "unassigned")] },
        { new string('a', 65), [("courseId", "101"), ("expectedState", "unassigned")] },
        { "abc\u0007def", [("courseId", "101"), ("expectedState", "unassigned")] },
    };

    [Theory]
    [MemberData(nameof(MalformedSets))]
    public async Task AMalformedRequest_IsMalformed_AndWritesNothing(string? code, (string, string?)[] form)
    {
        var world = WithUnassigned();

        var result = await SetAsync(world, code, form);

        Assert.Equal(MeetCodeChangeOutcome.Malformed, result.Outcome);
        AssertNothingWritten(world);
    }

    /// <summary>VR-001: a 64-character code is well-formed (and here unknown).</summary>
    [Fact]
    public async Task ASixtyFourCharacterCode_IsWellFormed()
    {
        var world = new MeetCodeWorld();

        var result = await SetAsync(world, new string('a', 64), ("courseId", "101"), ("expectedState", "unassigned"));

        Assert.Equal(MeetCodeChangeOutcome.CodeNotFound, result.Outcome);
    }

    /// <summary>api-design §2.8: names outside the form are ignored.</summary>
    [Fact]
    public async Task AnUnknownField_IsIgnored()
    {
        var world = WithUnassigned();

        var result = await SetAsync(world, Code, ("courseId", "101"), ("expectedState", "unassigned"), ("note", "x"));

        Assert.Equal(MeetCodeChangeOutcome.CoursePicked, result.Outcome);
    }

    /// <summary>api-design §2.7: returnPage is a navigation aid — malformed becomes 0, never an error.</summary>
    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("abc", 0)]
    [InlineData("-1", 0)]
    [InlineData("2147483648", 0)]
    [InlineData("2147483647", 2147483647)]
    [InlineData("4", 4)]
    public async Task ReturnPage_IsKeptWhenValid_AndZeroOtherwise(string? value, int expected)
    {
        var world = WithUnassigned();

        var result = await SetAsync(world, Code, ("courseId", "101"), ("expectedState", "unassigned"), ("returnPage", value));

        Assert.Equal(MeetCodeChangeOutcome.CoursePicked, result.Outcome);
        Assert.Equal(expected, result.ReturnPage);
    }

    // ---------------- Confirm (FR-009) ----------------

    /// <summary>AC-009: confirming records who and when; audited; nothing else changes.</summary>
    [Fact]
    public async Task ConfirmingAnAutomaticLink_RecordsWhoAndWhen_AndAuditsIt()
    {
        var world = WithAutomaticLink(CourseA);

        var result = await ConfirmAsync(world, Code, ("expectedCourseId", Id(CourseA)), ("returnPage", "2"));

        Assert.Equal(new MeetCodeChangeResult(MeetCodeChangeOutcome.Confirmed, MeetCodeList.Linked, 2), result);
        var link = world.Links.Committed[Code];
        Assert.Equal(((long?)DeanId, (DateTimeOffset?)world.Time.GetUtcNow()), (link.ConfirmedByAppUserId, link.ConfirmedAt));
        Assert.Equal((CourseA, true, Earlier), (link.CourseId!.Value, link.LinkedAutomatically!.Value, link.LinkedAt!.Value));
        var row = SingleAuditRow(world);
        Assert.Equal((AuditAction.MeetCodeLinkConfirmed, (long?)CourseA, (long?)null), (row.Action, row.TargetId, row.MeetPreviousCourseId));
    }

    public static TheoryData<string> NotConfirmable => new() { "person", "confirmed", "other-course", "marked", "unassigned" };

    /// <summary>I-9, FR-013: only an automatic, unconfirmed link on the expected course can be confirmed.</summary>
    [Theory]
    [MemberData(nameof(NotConfirmable))]
    public async Task ConfirmingWhatIsNotAnUnconfirmedAutomaticLinkOnTheCourse_IsStale(string state)
    {
        var world = new MeetCodeWorld();
        switch (state)
        {
            case "person":
                world.Links.Seed(MeetingCodeLink.LinkByPerson(Code, CourseA, AdminId, Earlier));
                break;
            case "confirmed":
                var confirmed = MeetingCodeLink.LinkAutomatically(Code, CourseA, Earlier);
                confirmed.Confirm(AdminId, Earlier);
                world.Links.Seed(confirmed);
                break;
            case "other-course":
                world.Links.Seed(MeetingCodeLink.LinkAutomatically(Code, CourseB, Earlier));
                break;
            case "marked":
                world.Links.Seed(MeetingCodeLink.MarkNotACourse(Code, AdminId, Earlier));
                break;
            default:
                world.Links.SeedUnassigned(Code);
                break;
        }

        var result = await ConfirmAsync(world, Code, ("expectedCourseId", Id(CourseA)));

        Assert.Equal(MeetCodeChangeOutcome.StateChanged, result.Outcome);
        AssertNothingWritten(world);
    }

    [Fact]
    public async Task ConfirmingAnUnknownCode_IsNotFound()
    {
        var world = new MeetCodeWorld();

        Assert.Equal(MeetCodeChangeOutcome.CodeNotFound, (await ConfirmAsync(world, Code, ("expectedCourseId", "101"))).Outcome);
        AssertNothingWritten(world);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("x")]
    [InlineData("0")]
    public async Task ConfirmingWithAMalformedExpectedCourse_IsMalformed(string? value)
    {
        var world = WithAutomaticLink(CourseA);

        Assert.Equal(MeetCodeChangeOutcome.Malformed, (await ConfirmAsync(world, Code, ("expectedCourseId", value))).Outcome);
        AssertNothingWritten(world);
    }

    // ---------------- Mark "not a course" (FR-011) ----------------

    /// <summary>AC-011: an unassigned code is marked; the audit row has no previous course.</summary>
    [Fact]
    public async Task MarkingAnUnassignedCode_MarksIt_AndAuditsIt()
    {
        var world = WithUnassigned();

        var result = await MarkAsync(world, Code, ("expectedState", "unassigned"));

        Assert.Equal(new MeetCodeChangeResult(MeetCodeChangeOutcome.Marked, MeetCodeList.Unassigned, 0), result);
        var link = world.Links.Committed[Code];
        Assert.Equal(MeetingCodeLinkState.Marked, link.State);
        Assert.Equal(((long?)DeanId, (DateTimeOffset?)world.Time.GetUtcNow()), (link.MarkedByAppUserId, link.MarkedAt));
        var row = SingleAuditRow(world);
        Assert.Equal((AuditAction.MeetCodeMarkedNotACourse, (long?)null, (long?)null, Code), (row.Action, row.TargetId, row.MeetPreviousCourseId, row.MeetCode));
    }

    /// <summary>AC-011: a linked code — automatic or by a person — is marked; the audit row names the previous course.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MarkingALinkedCode_ClearsTheCourse_AndAuditsThePreviousOne(bool automatic)
    {
        var world = new MeetCodeWorld();
        world.Links.Seed(automatic
            ? MeetingCodeLink.LinkAutomatically(Code, CourseA, Earlier)
            : MeetingCodeLink.LinkByPerson(Code, CourseA, AdminId, Earlier));

        var result = await MarkAsync(world, Code, ("expectedState", "linked"), ("expectedCourseId", Id(CourseA)));

        Assert.Equal(new MeetCodeChangeResult(MeetCodeChangeOutcome.Marked, MeetCodeList.Linked, 0), result);
        Assert.Null(world.Links.Committed[Code].CourseId);
        var row = SingleAuditRow(world);
        Assert.Equal((AuditAction.MeetCodeMarkedNotACourse, (long?)null, (long?)CourseA), (row.Action, row.TargetId, row.MeetPreviousCourseId));
    }

    /// <summary>api-design: <c>marked</c> is never a valid expected state for the mark form.</summary>
    [Fact]
    public async Task MarkingWithExpectedStateMarked_IsMalformed()
    {
        var world = WithMark();

        Assert.Equal(MeetCodeChangeOutcome.Malformed, (await MarkAsync(world, Code, ("expectedState", "marked"))).Outcome);
        AssertNothingWritten(world);
    }

    [Fact]
    public async Task MarkingAnAlreadyMarkedCode_IsStale()
    {
        var world = WithMark();

        var result = await MarkAsync(world, Code, ("expectedState", "unassigned"));

        Assert.Equal(new MeetCodeChangeResult(MeetCodeChangeOutcome.StateChanged, MeetCodeList.NotACourse, 0), result);
        AssertNothingWritten(world);
    }

    [Fact]
    public async Task MarkingAnUnknownCode_IsNotFound()
    {
        var world = new MeetCodeWorld();

        Assert.Equal(MeetCodeChangeOutcome.CodeNotFound, (await MarkAsync(world, Code, ("expectedState", "unassigned"))).Outcome);
    }

    // ---------------- Read-only mode (FR-015, AC-015, TC-5) ----------------

    public static TheoryData<string, string?, AuditAction, string?> ReadOnlyCases => new()
    {
        { "set", "unassigned", AuditAction.MeetCodeCoursePicked, Code },
        { "set", "linked", AuditAction.MeetCodeRelinked, Code },
        { "set", "marked", AuditAction.MeetCodeMarkRemoved, Code },
        { "set", "bogus", AuditAction.MeetCodeCoursePicked, Code },
        { "set", null, AuditAction.MeetCodeCoursePicked, Code },
        { "confirm", null, AuditAction.MeetCodeLinkConfirmed, Code },
        { "mark", "unassigned", AuditAction.MeetCodeMarkedNotACourse, Code },
        { "mark", "linked", AuditAction.MeetCodeMarkedNotACourse, Code },
    };

    /// <summary>
    /// api-design §2.5: the guard runs first — before the code, the form or the repository are looked at — and leaves one
    /// refused row whose action follows the expected state, with the code and no course.
    /// </summary>
    [Theory]
    [MemberData(nameof(ReadOnlyCases))]
    public async Task InReadOnlyMode_EveryWriteIsRefusedFirst_WithOneRefusedRow(
        string write, string? expectedState, AuditAction expectedAction, string? expectedCode)
    {
        var world = new MeetCodeWorld(readOnly: true);
        world.Links.Seed(MeetingCodeLink.LinkAutomatically(Code, CourseA, Earlier));
        var form = Form(("courseId", "102"), ("expectedState", expectedState), ("expectedCourseId", "101"));

        Task Act() => write switch
        {
            "set" => world.SetCourse.ExecuteAsync(DeanId, AppRole.Dean, Code, form, RequestId, Ct),
            "confirm" => world.Confirm.ExecuteAsync(DeanId, AppRole.Dean, Code, form, RequestId, Ct),
            _ => world.Mark.ExecuteAsync(DeanId, AppRole.Dean, Code, form, RequestId, Ct),
        };

        await Assert.ThrowsAsync<ReadOnlyModeException>(Act);

        Assert.Single(world.ReadOnly.Operations);
        Assert.Equal(0, world.Links.Reads);
        Assert.Empty(world.Links.Added);
        Assert.Equal(CourseA, world.Links.Committed[Code].CourseId);
        var row = Assert.Single(world.Audit.Written);
        Assert.Equal((expectedAction, AuditOutcome.Refused, (AuditRefusalCategory?)AuditRefusalCategory.ReadOnlyMode), (row.Action, row.Outcome, row.RefusalCategory));
        Assert.Equal(expectedCode, row.MeetCode);
        Assert.Null(row.TargetId);
        Assert.Null(row.MeetPreviousCourseId);
        Assert.Equal(1, world.Work.Commits);
    }

    /// <summary>A malformed code in read-only mode: still "read-only", the refused row carries no code.</summary>
    [Fact]
    public async Task InReadOnlyMode_AMalformedCode_IsRefusedWithNoCodeOnTheRow()
    {
        var world = new MeetCodeWorld(readOnly: true);

        await Assert.ThrowsAsync<ReadOnlyModeException>(
            () => world.Mark.ExecuteAsync(DeanId, AppRole.Dean, "   ", Form(("expectedState", "unassigned")), RequestId, Ct));

        Assert.Null(Assert.Single(world.Audit.Written).MeetCode);
    }
}
