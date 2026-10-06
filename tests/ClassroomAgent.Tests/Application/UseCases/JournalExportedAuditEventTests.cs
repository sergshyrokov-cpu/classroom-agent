using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-028 AC-007 (spec FR-010, S-06; entity model §1.1): the factory of the export audit row — a user's, names the
/// course, succeeded, carries the period, the template form, the row count and the format, and refuses impossible values.
/// </summary>
public sealed class JournalExportedAuditEventTests
{
    private static readonly DateTimeOffset At = InstallationTestHost.DefaultStart;

    private static AuditEvent Row(
        AppRole role = AppRole.Dean,
        long courseId = 42,
        DateOnly? from = null,
        DateOnly? to = null,
        long? templateId = null,
        int rows = 3) =>
        AuditEvent.JournalExported(
            7, role, courseId, from ?? JournalTestData.Period.From, to ?? JournalTestData.Period.To, templateId, rows,
            ExportFormat.Xlsx, At, "req-1");

    /// <summary>FR-010: every field of the row for the built-in template.</summary>
    [Fact]
    public void TheRow_CarriesTheActorTheCourseAndTheDetails()
    {
        var row = Row();

        Assert.Equal(AuditActorType.AppUser, row.ActorType);
        Assert.Equal(7, row.ActorId);
        Assert.Equal(AppRole.Dean, row.ActorRole);
        Assert.Equal(AuditAction.JournalExported, row.Action);
        Assert.Equal(AuditTargetType.Course, row.TargetType);
        Assert.Equal(42, row.TargetId);
        Assert.Equal(AuditOutcome.Succeeded, row.Outcome);
        Assert.Null(row.RefusalCategory);
        Assert.Equal("req-1", row.RequestId);
        Assert.Equal(At, row.OccurredAt);
        Assert.Equal(JournalTestData.Period.From, row.ExportPeriodFrom);
        Assert.Equal(JournalTestData.Period.To, row.ExportPeriodTo);
        Assert.Null(row.ExportTemplateId);
        Assert.True(row.ExportTemplateBuiltIn);
        Assert.Equal(3, row.ExportRows);
        Assert.Equal(ExportFormat.Xlsx, row.ExportFormat);
        Assert.Null(row.PurgedCourses);
    }

    /// <summary>db-design D-3: a created template is named by id and is not the built-in.</summary>
    [Fact]
    public void ACreatedTemplate_IsNamedById()
    {
        var row = Row(role: AppRole.Admin, templateId: 9);

        Assert.Equal(9, row.ExportTemplateId);
        Assert.False(row.ExportTemplateBuiltIn);
        Assert.Equal(AppRole.Admin, row.ActorRole);
    }

    /// <summary>FR-004.6: a one-day period and an empty report are valid.</summary>
    [Fact]
    public void AOneDayPeriod_AndZeroRows_AreValid()
    {
        var row = Row(from: JournalTestData.Period.From, to: JournalTestData.Period.From, rows: 0);

        Assert.Equal(0, row.ExportRows);
    }

    /// <summary>Entity model §1.1: impossible values are refused before a row exists.</summary>
    [Fact]
    public void ImpossibleValues_AreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Row(rows: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Row(courseId: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Row(templateId: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Row(from: JournalTestData.Period.To, to: JournalTestData.Period.From));
        Assert.Throws<ArgumentOutOfRangeException>(() => Row(role: (AppRole)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => AuditEvent.JournalExported(
            7, AppRole.Dean, 42, JournalTestData.Period.From, JournalTestData.Period.To, null, 1, (ExportFormat)99, At, null));
    }
}
