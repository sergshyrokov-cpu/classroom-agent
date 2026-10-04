namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi <c>PersonName</c>.</summary>
public sealed record PersonName(
    string? DisplayName,
    JournalNameKind NameKind);
