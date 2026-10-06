using ClassroomAgent.Application.Models.Export;

namespace ClassroomAgent.Application.Ports;

/// <summary>US-028 entity model §3.2: the program text of an export, through the installation's translations.</summary>
public interface IReportTexts
{
    string Get(ReportText text);

    string ProgramMark(string programKey);
}
