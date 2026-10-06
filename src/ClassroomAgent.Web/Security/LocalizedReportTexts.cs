using ClassroomAgent.Application.Localization;
using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Application.Ports;
using Microsoft.Extensions.Localization;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// US-028 entity model §3.2: the program text of an export, through the installation's translations. Skeleton (OD-005).
/// </summary>
public sealed class LocalizedReportTexts(IStringLocalizer<SharedResource> localizer) : IReportTexts
{
    public string Get(ReportText text)
    {
        _ = localizer;
        throw new NotImplementedException("US-028 IMPLEMENTATION");
    }

    public string ProgramMark(string programKey)
    {
        _ = localizer;
        throw new NotImplementedException("US-028 IMPLEMENTATION");
    }
}
