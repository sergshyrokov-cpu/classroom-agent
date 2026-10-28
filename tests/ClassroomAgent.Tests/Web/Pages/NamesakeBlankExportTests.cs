using ClassroomAgent.Tests.TestInfrastructure;
using ClosedXML.Excel;
using static ClassroomAgent.Tests.TestInfrastructure.NamesakeHostExtensions;
using N = ClassroomAgent.Tests.TestInfrastructure.NamesakeWorld;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-066 in the «Бланк для групових занять» export over HTTP (TC-2): the student name cells of every grading copy
/// (column B from row 9) hold «name (email)» for namesakes as the program layout does (spec FR-001(4), FR-004.4), the
/// rule judged over the whole report, not one copy (FR-002.1, I-4).
/// </summary>
public sealed class NamesakeBlankExportTests(PostgreSqlFixture database)
{
    private async Task<(XLWorkbook Blank, XLWorkbook Program)> ExportAsync(int? studentsPerCopy, params NamesakeStudent[] students)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, "uk", ct);
        await using var _host = host;
        var courseId = await host.SeedNamesakeCourseAsync(ct, students);

        var blank = await client.ExportAsync(
            GroupLessonsBlank.Body(courseId, studentsPerCopy: studentsPerCopy, template: ReportTemplateTestData.BuiltInKey), ct);
        var program = await client.ExportAsync(
            JournalExportHostExtensions.September(courseId, ReportTemplateTestData.BuiltInKey), ct);

        AssertServed(host, blank.Status);
        AssertServed(host, program.Status);
        return (blank.Workbook(), program.Workbook());
    }

    private static IReadOnlyList<IXLCell> AllBlankNames(XLWorkbook blank) =>
        BlankGradingSheets(blank).SelectMany(BlankNames).ToList();

    /// <summary>AC-004, FR-004.4: the blank's name cells hold the same texts as the program layout's, namesakes with their email.</summary>
    [Fact]
    public async Task TheBlanksNameCells_HoldTheNameAndTheEmail_AsTheProgramLayout()
    {
        var (blank, program) = await ExportAsync(null, N.PetrenkoA, N.PetrenkoB, N.Koval);
        using var _blank = blank;
        using var _program = program;

        var expected = new[]
        {
            "Коваль Олена", N.WithEmail("Петренко Іван", N.PetrenkoA), N.WithEmail("Петренко Іван", N.PetrenkoB),
        };
        Assert.Single(BlankGradingSheets(blank));
        Assert.Equal(expected, AllBlankNames(blank).Select(c => c.GetString()));
        Assert.Equal(expected, ProgramNames(program.Worksheet(1)).Select(c => c.GetString()));
    }

    /// <summary>
    /// FR-002.1, I-4: two students per copy put «Коваль Олена» and the first Petrenko in copy 1 and the second Petrenko
    /// alone in copy 2 — both Petrenkos still show their email.
    /// </summary>
    [Fact]
    public async Task NamesakesInDifferentCopies_BothShowTheirEmail()
    {
        var (blank, program) = await ExportAsync(2, N.PetrenkoA, N.PetrenkoB, N.Koval);
        using var _blank = blank;
        using var _program = program;

        var sheets = BlankGradingSheets(blank);
        Assert.Equal(2, sheets.Count);
        Assert.Equal(
            ["Коваль Олена", N.WithEmail("Петренко Іван", N.PetrenkoA)], BlankNames(sheets[0]).Select(c => c.GetString()));
        Assert.Equal([N.WithEmail("Петренко Іван", N.PetrenkoB)], BlankNames(sheets[1]).Select(c => c.GetString()));
    }

    /// <summary>AC-009, FR-004.4: the leaving namesake's cell holds «name (email)» and keeps the "left" note.</summary>
    [Fact]
    public async Task ALeavingNamesakesCell_HoldsTheEmail_AndKeepsTheLeftNote()
    {
        var texts = DataMarksTexts.Uk;
        var (blank, program) = await ExportAsync(null, N.PetrenkoA, N.PetrenkoB.Leaving(), N.Koval);
        using var _blank = blank;
        using var _program = program;

        var cells = AllBlankNames(blank);
        Assert.Equal(3, cells.Count);
        Assert.Equal(N.WithEmail("Петренко Іван", N.PetrenkoB), cells[2].GetString());
        Assert.Contains(
            texts.With(texts.LeftOn, N.LeftLastSeen), DataMarksHostExtensions.Note(cells[2]), StringComparison.Ordinal);
        Assert.Null(DataMarksHostExtensions.Note(cells[1]));
    }

    /// <summary>AC-006: without namesakes no cell of the blank holds an email.</summary>
    [Fact]
    public async Task NoNamesakes_NoCellOfTheBlankHoldsAnEmail()
    {
        var (blank, program) = await ExportAsync(null, N.PetrenkoA, N.Koval);
        using var _blank = blank;
        using var _program = program;

        Assert.Equal(["Коваль Олена", "Петренко Іван"], AllBlankNames(blank).Select(c => c.GetString()));
        Assert.DoesNotContain(AllTexts(blank), t => t.Contains('@', StringComparison.Ordinal));
    }
}
