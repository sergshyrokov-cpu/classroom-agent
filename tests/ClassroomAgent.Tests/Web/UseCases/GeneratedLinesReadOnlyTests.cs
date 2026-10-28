using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.DataMarksHostExtensions;
using W = ClassroomAgent.Tests.TestInfrastructure.DataMarksWorld;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-063 AC-007, spec §7, I-6, BR-026: in read-only mode the journal shows both lines and the export carries them in
/// its note; the lines come from the clock and stored data, so nothing is written to the teaching tables and Google is
/// never called (SC-7, TC-4). The export's own audit event stays its only write.
/// </summary>
public sealed class GeneratedLinesReadOnlyTests(PostgreSqlFixture database)
{
    public static TheoryData<ReadOnlyModeHost.Cause> ReadOnlyCauses => new(
        ReadOnlyModeHost.Cause.NeverConfirmed,
        ReadOnlyModeHost.Cause.Suspended,
        ReadOnlyModeHost.Cause.GracePeriodExpired);

    [Theory]
    [MemberData(nameof(ReadOnlyCauses))]
    public async Task InReadOnlyMode_TheJournalAndTheExportCarryBothLines_AndWriteNoTeachingData(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, "uk", ct, cause);
        await using var _host = host;
        var world = await host.SeedMarksCourseAsync(ct);
        var texts = GeneratedLinesTexts.Uk;
        var b = host.Time.GetUtcNow();
        var before = await host.TeachingFingerprintAsync(ct);

        var journal = await client.GetAsync(JournalTestData.SeptemberUrl(world.CourseId), ct);
        var export = await client.ExportAsync(JournalExportHostExtensions.September(world.CourseId, world.TemplateId), ct);

        Assert.Equal(HttpStatusCode.OK, journal.Status);
        Assert.Equal(texts.GeneratedLine(b), Text(Element(journal.Body, "journal-generated") ?? string.Empty));
        Assert.Equal(texts.DataAsOfLine(W.R2), Text(Element(journal.Body, "journal-data-as-of") ?? string.Empty));
        Assert.Equal(HttpStatusCode.OK, export.Status);
        using var workbook = export.Workbook();
        var note = Note(workbook.Worksheet(1).Cell(2, 2));
        Assert.NotNull(note);
        Assert.Contains(texts.GeneratedLine(b), note, StringComparison.Ordinal);
        Assert.Contains(texts.DataAsOfLine(W.R2), note, StringComparison.Ordinal);
        Assert.Equal(before, await host.TeachingFingerprintAsync(ct));
        Assert.Single(await host.ExportAuditRowsAsync(ct));
        Assert.Equal(0, host.GoogleCallCount());
    }
}
