namespace ClassroomAgent.Domain.Entities;

/// <summary>One grading-scale row of a <see cref="ReportTemplate"/> (US-027 entity model §2.3).</summary>
/// <remarks>Created only through <see cref="ReportTemplate"/>.</remarks>
public sealed class ReportTemplateScaleRow
{
    private ReportTemplateScaleRow()
    {
        Label = string.Empty;
    }

    public long Id { get; private set; }

    public long ReportTemplateId { get; private set; }

    public int FromPercent { get; private set; }

    public int ToPercent { get; private set; }

    public string Label { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    internal static ReportTemplateScaleRow Create(int fromPercent, int toPercent, string label) =>
        new() { FromPercent = fromPercent, ToPercent = toPercent, Label = label };
}
