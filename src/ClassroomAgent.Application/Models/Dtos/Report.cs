using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi <c>Report</c> — the HTML-independent report of spec FR-005.</summary>
public sealed record Report(
    ReportHeader Header,
    ReportView View,
    ReportEmptyStateKey? EmptyStateKey,
    GradingPart? Grading,
    LessonTopicsPart? LessonTopics);
