using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.RetentionPurgeTestData;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-037 AC-009, spec FR-011, BR-026, BR-075: in every read-only cause the purge deletes exactly as in normal mode
/// and writes its audit event — through the real commit backstop of the host — and calls no Google port (TC-4, TC-5).
/// </summary>
public sealed class RetentionPurgeReadOnlyTests(PostgreSqlFixture database)
{
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_ThePurgeDeletesAndRecordsItsRun(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct, cause);
        var course = await CourseRows.InsertCourseAsync(host, ct, updateTime: Old);
        await host.ExecuteAsync("UPDATE course SET created_at = @old, updated_at = @old", ct, ("old", Old));
        var account = await host.InsertAccountAsync("old@school.test", "dean", Old, Old, ct);
        var auditRow = await host.InsertAuditRowAsync(Old, ct);

        var outcome = await host.RunPurgeAsync(ct);

        Assert.Empty(outcome.Failures);
        Assert.Equal(0L, await host.CountAsync("course", ct, "id = @id", ("id", course)));
        Assert.DoesNotContain(account, (await host.AppUsersAsync(ct)).Select(u => u.Id));
        Assert.DoesNotContain(auditRow, (await host.AuditRowsAsync(ct)).Select(r => r.Id));
        var row = Assert.Single(await host.PurgeAuditRowsAsync(ct));
        Assert.Equal((1, 1, 1), (row.Courses, row.Accounts, row.AuditRows));
        Assert.Empty(host.Classroom.ImpersonatedAs);
    }

    /// <summary>US-007 FR-005: the use case is registered as the BR-026 write it performs, and as nothing else.</summary>
    [Fact]
    public void ThePurge_IsDeclaredAsTheRetentionPurgeServiceWrite()
    {
        Assert.True(PermittedServiceWrites.Declarations.TryGetValue(typeof(RunRetentionPurgeUseCase), out var write));
        Assert.Equal(PermittedServiceWrite.RetentionPurge, write);
    }
}
