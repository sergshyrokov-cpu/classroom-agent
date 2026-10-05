using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi <c>Report</c> — the HTML-independent report of spec FR-005.</summary>
public sealed record Report(
    ReportHeader Header,
    ReportView View,
    ReportEmptyStateKey? EmptyStateKey,
    GradingPart? Grading,
    LessonTopicsPart? LessonTopics)
{
    /// <summary>US-042 skeleton (OD-001): the effective name source (spec FR-004, FR-006).</summary>
    public ReportNameSource NameSource => throw new NotImplementedException();

    /// <summary>US-042 skeleton (OD-001): where the effective source came from.</summary>
    public NameSourceOrigin NameSourceOrigin => throw new NotImplementedException();
}
