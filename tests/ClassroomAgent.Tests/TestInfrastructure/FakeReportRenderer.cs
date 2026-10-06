using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>US-028 entity model §3.1: a renderer that keeps the workbook it was given and returns fixed bytes.</summary>
public sealed class FakeReportRenderer : IReportRenderer
{
    public static readonly byte[] Bytes = [0x50, 0x4B, 0x03, 0x04, 0x28];

    private readonly List<Workbook> _rendered = [];

    public IReadOnlyList<Workbook> Rendered => _rendered;

    /// <summary>When set, the next render throws, as a failing ClosedXML write would.</summary>
    public bool Fail { get; set; }

    public Task<byte[]> RenderAsync(Workbook workbook, CancellationToken cancellationToken)
    {
        if (Fail)
        {
            throw new InvalidOperationException("Synthetic renderer failure.");
        }

        _rendered.Add(workbook);
        return Task.FromResult(Bytes);
    }
}
