using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Domain.Entities;

/// <summary>One cell state's mark of a <see cref="ReportTemplate"/> (US-027 entity model §2.2).</summary>
/// <remarks>Created and changed only through <see cref="ReportTemplate"/>.</remarks>
public sealed class ReportTemplateMark
{
    private ReportTemplateMark()
    {
    }

    public long Id { get; private set; }

    public long ReportTemplateId { get; private set; }

    public ReportCellState State { get; private set; }

    public ReportMarkKind Kind { get; private set; }

    public string? Text { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    internal static ReportTemplateMark Create(ReportCellState state, ReportMarkKind kind, string? text) =>
        new() { State = state, Kind = kind, Text = text };

    internal void Set(ReportMarkKind kind, string? text)
    {
        Kind = kind;
        Text = text;
    }
}
