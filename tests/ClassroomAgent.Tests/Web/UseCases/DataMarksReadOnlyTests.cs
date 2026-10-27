using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.DataMarksHostExtensions;
using Cl = ClassroomAgent.Tests.TestInfrastructure.CourseListHostExtensions;
using W = ClassroomAgent.Tests.TestInfrastructure.DataMarksWorld;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-060 AC-010, spec §7, BR-026: in read-only mode the course list and the export of a marked course work and show the
/// marks; the marks are read from stored data, so nothing is written to the teaching tables and Google is never called
/// (SC-7, TC-4). The export's own audit event is the only write and is not a teaching table.
/// </summary>
public sealed class DataMarksReadOnlyTests(PostgreSqlFixture database)
{
    public static TheoryData<ReadOnlyModeHost.Cause> ReadOnlyCauses => new(
        ReadOnlyModeHost.Cause.NeverConfirmed,
        ReadOnlyModeHost.Cause.Suspended,
        ReadOnlyModeHost.Cause.GracePeriodExpired);

    [Theory]
    [MemberData(nameof(ReadOnlyCauses))]
    public async Task InReadOnlyMode_TheListAndTheExportShowTheMarks_AndWriteNoTeachingData(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, "uk", ct, cause);
        await using var _host = host;
        var courses = await host.SeedListCoursesAsync(ct);
        var world = await host.SeedMarksCourseAsync(ct, CourseHeaderVariant.NotInClassroom);
        var texts = DataMarksTexts.Uk;
        var before = await host.TeachingFingerprintAsync(ct);

        var list = await client.GetAsync(CourseListTestData.Url(status: "all"), ct);
        var export = await client.ExportAsync(JournalExportHostExtensions.September(world.CourseId, world.TemplateId), ct);

        Assert.Equal(HttpStatusCode.OK, list.Status);
        var index = Cl.RowIds(list.Body).ToList().IndexOf(courses.Absent);
        Assert.True(index >= 0);
        Assert.Contains(texts.With(texts.CourseNotInClassroomSince, W.D1), Cl.RowTexts(list.Body)[index], StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, export.Status);
        using var workbook = export.Workbook();
        Assert.Equal(W.HeaderLine(CourseHeaderVariant.NotInClassroom, texts), workbook.Worksheet(1).Cell(2, 3).GetString());
        Assert.NotNull(Note(ProgramCell(workbook.Worksheet(1), W.Over, W.GradedTitle)));
        Assert.Equal(before, await host.TeachingFingerprintAsync(ct));
        Assert.Equal(0, host.GoogleCallCount());
    }
}
