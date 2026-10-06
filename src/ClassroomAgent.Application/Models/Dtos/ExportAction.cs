using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-028 entity model §3.2: what the report page's export button submits.</summary>
public sealed record ExportAction(string Template, long CourseId, DateOnly From, DateOnly To, ReportNameSource Names);
