using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-044 AC-010 (spec FR-013; entity model §1.3; db-design §3.2): the factories of the two Meet export audit rows set
/// exactly the designed columns and refuse impossible values before a row exists.
/// </summary>
public sealed class MeetExportAuditEventTests
{
    private static readonly DateTimeOffset At = InstallationTestHost.DefaultStart;

    private static readonly DateOnly From = new(2026, 10, 1);

    private static readonly DateOnly To = new(2026, 10, 31);

    private static AuditEvent Reports(
        AppRole role = AppRole.Dean,
        long courseId = 7,
        DateOnly? from = null,
        DateOnly? to = null,
        int studentRows = 3,
        int meetingRows = 2,
        ExportFormat format = ExportFormat.Xlsx) =>
        AuditEvent.MeetReportsExported(
            502, role, courseId, from ?? From, to ?? To, studentRows, meetingRows, format, At, "req-1");

    private static AuditEvent Meeting(
        AppRole role = AppRole.Admin,
        long courseId = 7,
        long meetingId = 42,
        int personRows = 4,
        ExportFormat format = ExportFormat.Xlsx) =>
        AuditEvent.MeetMeetingExported(502, role, courseId, meetingId, personRows, format, At, null);

    /// <summary>db-design §3.2: every column of the course-workbook row.</summary>
    [Fact]
    public void TheReportsRow_CarriesTheActorTheCourseThePeriodAndTheCounts()
    {
        var row = Reports();

        Assert.Equal(AuditAction.MeetReportsExported, row.Action);
        Assert.Equal(AuditActorType.AppUser, row.ActorType);
        Assert.Equal(502, row.ActorId);
        Assert.Equal(AppRole.Dean, row.ActorRole);
        Assert.Equal(AuditTargetType.Course, row.TargetType);
        Assert.Equal(7, row.TargetId);
        Assert.Equal(AuditOutcome.Succeeded, row.Outcome);
        Assert.Null(row.RefusalCategory);
        Assert.Equal("req-1", row.RequestId);
        Assert.Equal(At, row.OccurredAt);
        Assert.Equal(From, row.ExportPeriodFrom);
        Assert.Equal(To, row.ExportPeriodTo);
        Assert.Equal(3, row.ExportRows);
        Assert.Equal(2, row.ExportMeetings);
        Assert.Equal(ExportFormat.Xlsx, row.ExportFormat);
        Assert.Null(row.ExportMeetingId);
        Assert.Null(row.ExportTemplateId);
        Assert.Null(row.ExportTemplateBuiltIn);
        Assert.Null(row.MeetCode);
    }

    /// <summary>db-design §3.2: every column of the meeting-workbook row.</summary>
    [Fact]
    public void TheMeetingRow_CarriesTheActorTheCourseTheMeetingAndTheRows()
    {
        var row = Meeting();

        Assert.Equal(AuditAction.MeetMeetingExported, row.Action);
        Assert.Equal(AuditActorType.AppUser, row.ActorType);
        Assert.Equal(502, row.ActorId);
        Assert.Equal(AppRole.Admin, row.ActorRole);
        Assert.Equal(AuditTargetType.Course, row.TargetType);
        Assert.Equal(7, row.TargetId);
        Assert.Equal(AuditOutcome.Succeeded, row.Outcome);
        Assert.Null(row.RefusalCategory);
        Assert.Null(row.RequestId);
        Assert.Equal(At, row.OccurredAt);
        Assert.Equal(42, row.ExportMeetingId);
        Assert.Equal(4, row.ExportRows);
        Assert.Equal(ExportFormat.Xlsx, row.ExportFormat);
        Assert.Null(row.ExportPeriodFrom);
        Assert.Null(row.ExportPeriodTo);
        Assert.Null(row.ExportMeetings);
        Assert.Null(row.ExportTemplateId);
        Assert.Null(row.ExportTemplateBuiltIn);
        Assert.Null(row.MeetCode);
    }

    /// <summary>Entity model §1.3: zero counts are valid (an empty course, nobody identified).</summary>
    [Fact]
    public void ZeroCounts_AreAccepted()
    {
        var reports = Reports(studentRows: 0, meetingRows: 0);
        var meeting = Meeting(personRows: 0);

        Assert.Equal(0, reports.ExportRows);
        Assert.Equal(0, reports.ExportMeetings);
        Assert.Equal(0, meeting.ExportRows);
    }

    /// <summary>Entity model §1.3: a one-day period is valid.</summary>
    [Fact]
    public void AOneDayPeriod_IsAccepted()
    {
        var row = Reports(from: From, to: From);

        Assert.Equal(From, row.ExportPeriodFrom);
        Assert.Equal(From, row.ExportPeriodTo);
    }

    /// <summary>Entity model §1.3: impossible values of the course-workbook row are refused (AD-9).</summary>
    [Fact]
    public void ImpossibleValues_OfTheReportsRow_AreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Reports(courseId: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Reports(courseId: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Reports(studentRows: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Reports(meetingRows: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Reports(from: To, to: From));
        Assert.Throws<ArgumentOutOfRangeException>(() => Reports(role: (AppRole)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => Reports(format: (ExportFormat)99));
    }

    /// <summary>Entity model §1.3: impossible values of the meeting-workbook row are refused (AD-9).</summary>
    [Fact]
    public void ImpossibleValues_OfTheMeetingRow_AreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Meeting(courseId: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Meeting(courseId: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Meeting(meetingId: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Meeting(meetingId: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Meeting(personRows: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Meeting(role: (AppRole)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => Meeting(format: (ExportFormat)99));
    }
}
