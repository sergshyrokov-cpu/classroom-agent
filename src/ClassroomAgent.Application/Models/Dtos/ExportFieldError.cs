namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-028 entity model §3.2: one rejected field of an export request.</summary>
public sealed record ExportFieldError(ExportField Field, ExportMessageKey Key);
