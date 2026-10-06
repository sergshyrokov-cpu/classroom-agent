using ClassroomAgent.Application.Models.Export;

namespace ClassroomAgent.Application.Ports;

/// <summary>US-028 OD-001: turns a format-neutral <see cref="Workbook"/> into file bytes. Implemented in Infrastructure.</summary>
public interface IReportRenderer
{
    Task<byte[]> RenderAsync(Workbook workbook, CancellationToken cancellationToken);
}
