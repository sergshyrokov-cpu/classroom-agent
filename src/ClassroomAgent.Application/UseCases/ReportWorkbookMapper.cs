using System.Globalization;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>US-028 entity model §3.2: maps a <see cref="Report"/> to a format-neutral <see cref="Workbook"/>. Skeleton (OD-005).</summary>
public static class ReportWorkbookMapper
{
    public static Workbook Map(Report report, IReportTexts texts, CultureInfo uiCulture, PageOrientation orientation) =>
        throw new NotImplementedException("US-028 IMPLEMENTATION");
}
