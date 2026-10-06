using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-028 entity model §3.2: a text port that answers every program text with a recognisable marker, so a test can
/// tell a translated program text from Google or school text written as is (spec FR-007).
/// </summary>
public sealed class FakeReportTexts : IReportTexts
{
    private readonly List<string> _asked = [];

    /// <summary>Every text asked for, in order: <c>ReportText</c> member names and <c>mark:</c> program keys.</summary>
    public IReadOnlyList<string> Asked => _asked;

    public static string Of(ReportText text) => "⟦" + text + "⟧";

    public static string Mark(string programKey) => "⟦mark:" + programKey + "⟧";

    public string Get(ReportText text)
    {
        _asked.Add(text.ToString());
        return Of(text);
    }

    public string ProgramMark(string programKey)
    {
        _asked.Add("mark:" + programKey);
        return Mark(programKey);
    }
}
