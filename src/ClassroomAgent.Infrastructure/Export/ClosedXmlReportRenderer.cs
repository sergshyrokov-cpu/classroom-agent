using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Infrastructure.Export;

/// <summary>US-028 OD-001: renders the neutral <see cref="Workbook"/> to an Excel file. Skeleton (OD-005).</summary>
public sealed class ClosedXmlReportRenderer : IReportRenderer
{
    public Task<byte[]> RenderAsync(Workbook workbook, CancellationToken cancellationToken) =>
        throw new NotImplementedException("US-028 IMPLEMENTATION");
}
