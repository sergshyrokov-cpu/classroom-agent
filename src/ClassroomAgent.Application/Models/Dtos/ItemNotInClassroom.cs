namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-062 OpenAPI <c>ItemNotInClassroom</c>: one item of T2.</summary>
public sealed record ItemNotInClassroom(string Title, DateTimeOffset Since);
