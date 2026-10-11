namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-044 entity model §2.2: one rejected member of a Meet export request.</summary>
public sealed record MeetExportFieldError(MeetExportField Field, MeetExportMessageKey Key);
