namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>A person: full name and email, either may be null (spec FR-011, OD-012); both null renders the unnamed label.</summary>
public sealed record PersonRef(string? FullName, string? Email);
