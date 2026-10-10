using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-032 spec FR-014, FR-016, db-design §3.2, §3.3, entity model §2: the audit rows of link changes — actor, target
/// course, previous course, code — and the purge count of links; no personal data can enter any of them.
/// </summary>
public sealed class MeetCodeAuditEventTests
{
    private const string Code = "abc-0001-xyz";

    private const long A = 101;

    private const long B = 102;

    private const long Person = 502;

    private const string RequestId = "r-1";

    private static readonly DateTimeOffset T0 = InstallationTestHost.DefaultStart;

    [Fact]
    public void TheAutomaticLink_IsTheSystems_TargetsTheCourse_AndCarriesTheCode()
    {
        var row = AuditEvent.MeetCodeAutoLinked(Code, A, T0);

        Assert.Equal(AuditAction.MeetCodeAutoLinked, row.Action);
        Assert.Equal(AuditActorType.System, row.ActorType);
        Assert.Null(row.ActorId);
        Assert.Null(row.ActorRole);
        Assert.Equal((AuditTargetType?)AuditTargetType.Course, row.TargetType);
        Assert.Equal(A, row.TargetId);
        Assert.Equal(Code, row.MeetCode);
        Assert.Null(row.MeetPreviousCourseId);
        Assert.Equal(AuditOutcome.Succeeded, row.Outcome);
        Assert.Null(row.RefusalCategory);
        Assert.Null(row.RequestId);
        Assert.Equal(T0, row.OccurredAt);
    }

    public static TheoryData<AuditAction, long?, long?> SucceededShapes => new()
    {
        { AuditAction.MeetCodeCoursePicked, A, null },
        { AuditAction.MeetCodeLinkConfirmed, A, null },
        { AuditAction.MeetCodeRelinked, B, A },
        { AuditAction.MeetCodeMarkedNotACourse, null, A },
        { AuditAction.MeetCodeMarkedNotACourse, null, null },
        { AuditAction.MeetCodeMarkRemoved, B, null },
    };

    /// <summary>db-design §3.2: the course after the change is the target; the previous course has its own column.</summary>
    [Theory]
    [MemberData(nameof(SucceededShapes))]
    public void APersonsChange_RecordsActorCodeAndCourses(AuditAction action, long? course, long? previous)
    {
        var row = AuditEvent.MeetCodeChanged(action, Person, AppRole.Dean, Code, course, previous, T0, RequestId);

        Assert.Equal(action, row.Action);
        Assert.Equal(AuditActorType.AppUser, row.ActorType);
        Assert.Equal(Person, row.ActorId);
        Assert.Equal(AppRole.Dean, row.ActorRole);
        Assert.Equal(Code, row.MeetCode);
        Assert.Equal(course, row.TargetId);
        Assert.Equal(course is null ? null : AuditTargetType.Course, row.TargetType);
        Assert.Equal(previous, row.MeetPreviousCourseId);
        Assert.Equal(AuditOutcome.Succeeded, row.Outcome);
        Assert.Equal(RequestId, row.RequestId);
    }

    public static TheoryData<AuditAction, long?, long?> ForbiddenShapes => new()
    {
        { AuditAction.MeetCodeRelinked, B, null },
        { AuditAction.MeetCodeCoursePicked, null, null },
        { AuditAction.MeetCodeCoursePicked, A, B },
        { AuditAction.MeetCodeLinkConfirmed, A, B },
        { AuditAction.MeetCodeMarkedNotACourse, A, null },
        { AuditAction.MeetCodeMarkRemoved, null, null },
        { AuditAction.MeetCodeAutoLinked, A, null },
        { AuditAction.JournalExported, A, null },
    };

    /// <summary>The factory rejects every combination <c>ck_audit_event_meet_code_shape</c> forbids (and the system's action).</summary>
    [Theory]
    [MemberData(nameof(ForbiddenShapes))]
    public void AShapeTheConstraintForbids_IsRejected(AuditAction action, long? course, long? previous) =>
        Assert.ThrowsAny<ArgumentException>(
            () => AuditEvent.MeetCodeChanged(action, Person, AppRole.Admin, Code, course, previous, T0, RequestId));

    /// <summary>api-design §2.5: a read-only refusal carries the code when well-formed and never a course.</summary>
    [Theory]
    [InlineData(Code)]
    [InlineData(null)]
    public void ARefusal_HasNoCourses_AndTheCodeOnlyWhenGiven(string? code)
    {
        var row = AuditEvent.MeetCodeChangeRefused(AuditAction.MeetCodeRelinked, Person, AppRole.Admin, code, T0, RequestId);

        Assert.Equal(AuditAction.MeetCodeRelinked, row.Action);
        Assert.Equal(AuditOutcome.Refused, row.Outcome);
        Assert.Equal((AuditRefusalCategory?)AuditRefusalCategory.ReadOnlyMode, row.RefusalCategory);
        Assert.Equal(code, row.MeetCode);
        Assert.Null(row.TargetType);
        Assert.Null(row.TargetId);
        Assert.Null(row.MeetPreviousCourseId);
        Assert.Equal((Person, (AppRole?)AppRole.Admin), (row.ActorId!.Value, row.ActorRole));
    }

    [Fact]
    public void ARefusalOfTheSystemsAction_IsRejected() =>
        Assert.ThrowsAny<ArgumentException>(
            () => AuditEvent.MeetCodeChangeRefused(AuditAction.MeetCodeAutoLinked, Person, AppRole.Admin, Code, T0, RequestId));

    /// <summary>Other actions carry no meet-code column (<c>ck_audit_event_meet_code_absent</c>).</summary>
    [Fact]
    public void APurgeRow_CarriesNoMeetCode_ButCarriesTheLinksCount()
    {
        var row = AuditEvent.RetentionPurgeRun(new RetentionPurgeCounts(0, 0, 0, 0, 0, 0, 0, MeetCodeLinks: 4), T0);

        Assert.Null(row.MeetCode);
        Assert.Null(row.MeetPreviousCourseId);
        Assert.Equal(4, row.PurgedMeetCodeLinks);
    }

    /// <summary>FR-016: a purge row written after this Story always carries the count, zero included.</summary>
    [Fact]
    public void APurgeRowWithNoLinksRemoved_RecordsZero() =>
        Assert.Equal(0, AuditEvent.RetentionPurgeRun(RetentionPurgeCounts.Zero, T0).PurgedMeetCodeLinks);

    [Fact]
    public void ANegativeLinksCount_IsRejected() =>
        Assert.ThrowsAny<ArgumentException>(
            () => AuditEvent.RetentionPurgeRun(new RetentionPurgeCounts(0, 0, 0, 0, 0, 0, 0, MeetCodeLinks: -1), T0));

    /// <summary>A row of any other action carries no links count.</summary>
    [Fact]
    public void ALinkChange_CarriesNoPurgeCount() =>
        Assert.Null(AuditEvent.MeetCodeAutoLinked(Code, A, T0).PurgedMeetCodeLinks);
}
