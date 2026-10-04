using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>One column — an item dated in the period (US-025 openapi <c>JournalColumn</c>, spec FR-004).</summary>
public sealed record JournalColumn(string Title, DateOnly Date, CourseWorkKind Kind, decimal? MaxPoints);
