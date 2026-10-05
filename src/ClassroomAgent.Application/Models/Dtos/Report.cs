using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// Openapi <c>Report</c> — the HTML-independent report of spec FR-005, with the effective name source and where it came
/// from (US-042 spec FR-004, FR-006), so an export from the page can reproduce the screen.
/// </summary>
public sealed record Report(
    ReportHeader Header,
    ReportView View,
    ReportEmptyStateKey? EmptyStateKey,
    GradingPart? Grading,
    LessonTopicsPart? LessonTopics,
    ReportNameSource NameSource,
    NameSourceOrigin NameSourceOrigin);
