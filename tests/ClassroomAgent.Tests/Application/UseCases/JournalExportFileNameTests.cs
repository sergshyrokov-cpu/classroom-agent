using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-028 AC-005 (spec FR-006.1, I-8, S-09; entity model §2.3): the file name is the course name and the period only,
/// made safe for a Windows file name, with a translated fallback when nothing of the name remains.
/// </summary>
public sealed class JournalExportFileNameTests
{
    private const string Fallback = "курс";

    private static string Build(string courseName) =>
        JournalExportFileName.Build(courseName, JournalTestData.Period.From, JournalTestData.Period.To, Fallback);

    /// <summary>FR-006.1: the spec's own example, Cyrillic kept.</summary>
    [Fact]
    public void TheName_IsCourseThenPeriod_WithAnEnDash() =>
        Assert.Equal("Алгебра 7-А 2026-09-01–2026-09-30.xlsx", Build("Алгебра 7-А"));

    /// <summary>FR-006.1: every character a Windows file name cannot hold becomes an underscore.</summary>
    [Fact]
    public void ForbiddenCharacters_BecomeUnderscores() =>
        Assert.Equal("a_b_c_d_e_f_g_h_i_j 2026-09-01–2026-09-30.xlsx", Build("a\\b/c:d*e?f\"g<h>i|j"));

    /// <summary>FR-006.1: control characters become underscores too.</summary>
    [Fact]
    public void ControlCharacters_BecomeUnderscores() =>
        Assert.Equal("a_b 2026-09-01–2026-09-30.xlsx", Build("a\u0001b"));

    /// <summary>FR-006.1: runs of white space become one space; leading and trailing spaces and dots go.</summary>
    [Fact]
    public void WhiteSpace_IsCollapsed_AndEdgesTrimmed() =>
        Assert.Equal("Хімія 9 Б 2026-09-01–2026-09-30.xlsx", Build("  ..Хімія    9   Б.. "));

    /// <summary>FR-006.1: the course part is cut to 100 characters.</summary>
    [Fact]
    public void TheCoursePart_IsCutTo100Characters()
    {
        var name = Build(new string('я', 150));

        Assert.Equal(new string('я', 100) + " 2026-09-01–2026-09-30.xlsx", name);
    }

    /// <summary>FR-006.1: when nothing of the course name remains, the translated word "course" is used.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    [InlineData(" . . ")]
    public void AnEmptyResult_UsesTheFallback(string courseName) =>
        Assert.Equal("курс 2026-09-01–2026-09-30.xlsx", Build(courseName));
}
