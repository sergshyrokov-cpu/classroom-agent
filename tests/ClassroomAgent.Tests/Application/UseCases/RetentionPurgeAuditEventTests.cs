using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-037 spec FR-010, VR-003, entity model §1: the purge's audit row — actor <c>system</c> with no identifier, no
/// target, succeeded, no request id — and a negative count is refused before it reaches the database.
/// </summary>
public sealed class RetentionPurgeAuditEventTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 17, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ThePurgeRow_IsASystemActionWithNoTargetAndNoRequest()
    {
        var row = AuditEvent.RetentionPurgeRun(new RetentionPurgeCounts(1, 2, 3, 4, 5), At);

        Assert.Equal(AuditActorType.System, row.ActorType);
        Assert.Null(row.ActorId);
        Assert.Null(row.ActorRole);
        Assert.Equal(AuditAction.RetentionPurgeRun, row.Action);
        Assert.Null(row.TargetType);
        Assert.Null(row.TargetId);
        Assert.Equal(AuditOutcome.Succeeded, row.Outcome);
        Assert.Null(row.RefusalCategory);
        Assert.Null(row.RequestId);
        Assert.Equal(At, row.OccurredAt);
    }

    /// <summary>AC-008: a run that removed nothing still gets its row.</summary>
    [Fact]
    public void AZeroRun_IsAValidRow()
    {
        var row = AuditEvent.RetentionPurgeRun(RetentionPurgeCounts.Zero, At);

        Assert.Equal(AuditAction.RetentionPurgeRun, row.Action);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0, 0)]
    [InlineData(0, -1, 0, 0, 0)]
    [InlineData(0, 0, -1, 0, 0)]
    [InlineData(0, 0, 0, -1, 0)]
    [InlineData(0, 0, 0, 0, -1)]
    public void ANegativeCount_IsRejected(int courses, int leavers, int participants, int accounts, int auditRows)
    {
        var counts = new RetentionPurgeCounts(courses, leavers, participants, accounts, auditRows);

        Assert.Throws<ArgumentOutOfRangeException>(() => AuditEvent.RetentionPurgeRun(counts, At));
    }

    /// <summary>SC-11: still no public setter and no mutator on the entity.</summary>
    [Fact]
    public void TheEntity_StaysImmutable()
    {
        Assert.DoesNotContain(
            typeof(AuditEvent).GetProperties(),
            p => p.SetMethod is { IsPublic: true });
        Assert.DoesNotContain(
            typeof(AuditEvent).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly),
            m => !m.IsSpecialName);
    }
}
