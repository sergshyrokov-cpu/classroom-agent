using ClassroomAgent.Tests.TestInfrastructure;
using ClosedXML.Excel;
using static ClassroomAgent.Tests.TestInfrastructure.NamesakeHostExtensions;
using N = ClassroomAgent.Tests.TestInfrastructure.NamesakeWorld;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-066 in the program-layout export of the report over HTTP (TC-2; US-043 export contract unchanged), read back with
/// ClosedXML from the response bytes: the student name cells (column A below the heading row) hold «name (email)» for
/// namesakes only (spec FR-004.4), the "left" note stays (US-060), and nothing else of the file changes.
/// </summary>
public sealed class NamesakeReportExportTests(PostgreSqlFixture database)
{
    public static TheoryData<string> Languages => new("uk", "en");

    private async Task<XLWorkbook> ExportAsync(string language, string? names, params NamesakeStudent[] students)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, language, ct);
        await using var _host = host;
        var courseId = await host.SeedNamesakeCourseAsync(ct, students);

        var response = await client.ExportAsync(
            JournalExportHostExtensions.September(courseId, ReportTemplateTestData.BuiltInKey, names), ct);

        AssertServed(host, response.Status);
        return response.Workbook();
    }

    private static IReadOnlyList<string> NameTexts(XLWorkbook workbook) =>
        ProgramNames(workbook.Worksheet(1)).Select(c => c.GetString()).ToList();

    /// <summary>
    /// AC-003, FR-004.1, FR-004.4, OD-001 a: the two namesakes' name cells hold «Петренко Іван (email)», the other holds
    /// the name only, in the order of the report; the same in both languages (I-5).
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task TheNamesakesNameCells_HoldTheNameAndTheEmail(string language)
    {
        using var workbook = await ExportAsync(language, null, N.PetrenkoA, N.PetrenkoB, N.Koval);

        Assert.Equal(
            ["Коваль Олена", N.WithEmail("Петренко Іван", N.PetrenkoA), N.WithEmail("Петренко Іван", N.PetrenkoB)],
            NameTexts(workbook));
    }

    /// <summary>
    /// AC-003, FR-004.4: the file with namesakes (names from the profile) and the file of the same course without them
    /// (names from the email, three different addresses) are the same file apart from the text of the three name
    /// cells: the same sheets, used ranges, merged ranges, row heights, cell values and notes, and name cells styled
    /// alike. Every student has the same grade, so the order of the rows does not matter; the teacher has no profile
    /// name, so the name source labels them alike in both files. Column widths follow the
    /// content as before this Story (the renderer fits them) and are not compared.
    /// </summary>
    [Fact]
    public async Task OnlyTheNameCellsText_Changes_TheLayoutIsTheSame()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, "uk", ct);
        await using var _host = host;
        var courseId = await host.SeedNamesakeCourseAsync(
            ct, [N.PetrenkoA, N.PetrenkoB, N.Koval], TeacherWithoutProfileName);

        var profileResponse = await client.ExportAsync(
            JournalExportHostExtensions.September(courseId, ReportTemplateTestData.BuiltInKey, "profile"), ct);
        var emailResponse = await client.ExportAsync(
            JournalExportHostExtensions.September(courseId, ReportTemplateTestData.BuiltInKey, "email"), ct);
        AssertServed(host, profileResponse.Status);
        AssertServed(host, emailResponse.Status);
        using var profile = profileResponse.Workbook();
        using var email = emailResponse.Workbook();

        var nameCells = ProgramNames(profile.Worksheet(1)).Select(c => c.Address.ToString()).ToHashSet();
        Assert.Equal(3, nameCells.Count);
        Assert.Equal(profile.Worksheets.Select(s => s.Name), email.Worksheets.Select(s => s.Name));

        for (var index = 1; index <= profile.Worksheets.Count; index++)
        {
            var left = profile.Worksheet(index);
            var right = email.Worksheet(index);
            Assert.Equal(left.RangeUsed()?.RangeAddress.ToString(), right.RangeUsed()?.RangeAddress.ToString());
            Assert.Equal(
                left.MergedRanges.Select(r => r.RangeAddress.ToString()).Order(),
                right.MergedRanges.Select(r => r.RangeAddress.ToString()).Order());
            Assert.Equal(
                left.RowsUsed().Select(r => (r.RowNumber(), r.Height)),
                right.RowsUsed().Select(r => (r.RowNumber(), r.Height)));

            foreach (var cell in left.CellsUsed())
            {
                var other = right.Cell(cell.Address.RowNumber, cell.Address.ColumnNumber);
                var address = cell.Address.ToString();
                if (index == 1 && nameCells.Contains(address))
                {
                    Assert.Equal(StyleOf(cell), StyleOf(other));
                    Assert.Equal(DataMarksHostExtensions.Note(cell), DataMarksHostExtensions.Note(other));
                    continue;
                }

                Assert.True(cell.GetString() == other.GetString(), $"{left.Name}!{address}: «{cell.GetString()}» vs «{other.GetString()}»");
                Assert.Equal(DataMarksHostExtensions.Note(cell), DataMarksHostExtensions.Note(other));
            }
        }

        // Last, so the comparison above is proved to run before the Story's behaviour exists: the profile file must
        // really carry the namesakes' emails, or the comparison would compare two files without them.
        Assert.Contains(NameTexts(profile), t => t.Contains(N.PetrenkoA.Email!, StringComparison.Ordinal));
    }

    /// <summary>AC-006, FR-003.2: without namesakes no cell of any sheet holds an email.</summary>
    [Fact]
    public async Task NoNamesakes_NoCellHoldsAnEmail()
    {
        using var workbook = await ExportAsync("uk", null, N.PetrenkoA, N.Koval);

        Assert.Equal(["Коваль Олена", "Петренко Іван"], NameTexts(workbook));
        Assert.DoesNotContain(AllTexts(workbook), t => t.Contains('@', StringComparison.Ordinal));
    }

    /// <summary>AC-008, FR-003.1: the namesake without an email has the name alone in the cell.</summary>
    [Fact]
    public async Task ANamesakeWithoutAnEmail_HasTheNameOnly()
    {
        using var workbook = await ExportAsync("uk", null, N.PetrenkoA, N.PetrenkoNoEmail, N.Koval);

        Assert.Equal(["Коваль Олена", N.WithEmail("Петренко Іван", N.PetrenkoA), "Петренко Іван"], NameTexts(workbook));
    }

    /// <summary>
    /// AC-009, FR-004.4: the leaving namesake's cell holds «name (email)» and its note keeps «вибув(-ла) &lt;дата&gt;»
    /// (US-060 FR-007.4); the other namesake's cell has no note.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task ALeavingNamesakesCell_HoldsTheEmail_AndKeepsTheLeftNote(string language)
    {
        var texts = DataMarksTexts.Of(language);
        using var workbook = await ExportAsync(language, null, N.PetrenkoA, N.PetrenkoB.Leaving(), N.Koval);

        var cells = ProgramNames(workbook.Worksheet(1));
        Assert.Equal(3, cells.Count);
        Assert.Equal(N.WithEmail("Петренко Іван", N.PetrenkoA), cells[1].GetString());
        Assert.Null(DataMarksHostExtensions.Note(cells[1]));
        Assert.Equal(N.WithEmail("Петренко Іван", N.PetrenkoB), cells[2].GetString());
        Assert.Contains(
            texts.With(texts.LeftOn, N.LeftLastSeen), DataMarksHostExtensions.Note(cells[2]), StringComparison.Ordinal);
    }

    private static string StyleOf(IXLCell cell) =>
        string.Join(
            '|',
            cell.Style.Font.Bold,
            cell.Style.Font.Italic,
            cell.Style.Font.FontSize,
            cell.Style.Font.FontName,
            cell.Style.Alignment.Horizontal,
            cell.Style.Alignment.Vertical,
            cell.Style.Alignment.WrapText,
            cell.Style.Fill.PatternType,
            cell.Style.Border.LeftBorder,
            cell.Style.Border.RightBorder,
            cell.Style.Border.TopBorder,
            cell.Style.Border.BottomBorder,
            cell.Style.NumberFormat.Format);
}
